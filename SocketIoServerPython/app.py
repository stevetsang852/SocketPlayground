from http import client
from threading import Lock, Timer
from unicodedata import name
from flask import Flask, render_template, session, request, \
    copy_current_request_context
from flask_socketio import SocketIO, emit, join_room, leave_room, \
    close_room, rooms, disconnect, call

from client_manager import ClientManager
from client import  Client
import json
import os
import copy
import time
import random

# Set this variable to "threading", "eventlet" or "gevent" to test the
# different async modes, or leave it set to None for the application to choose
# the best option based on installed packages.
async_mode = None

app = Flask(__name__)
app.config['SECRET_KEY'] = 'secret!'

MAX_BUFFER_SIZE = 100 * 1000 * 1000  # 100 MB

upload_temp_path = 'temp/upload'
upgrade_temp_path = 'temp/upgrade'

socketio = SocketIO(app, async_mode=async_mode , max_http_buffer_size=MAX_BUFFER_SIZE, cors_allowed_origins='*', debug=True)
thread = None
thread_lock = Lock()

_clientManager = ClientManager()
status_pool = {}

def with_session_count(data):
    session['receive_count'] = session.get('receive_count', 0) + 1
    data['count'] = session['receive_count']
    return data

def background_thread():
    """Example of how to send server generated events to clients."""
    with app.app_context():
        count = 0
        #socketio.server.manager.rooms.keys()
        #socketio.server.manager.rooms[self.namespace].keys() #with namespace
        while True:        
            count += 1
            allrooms = copy.deepcopy(socketio.server.manager.rooms)
            for room in  allrooms.keys():
                room_txt = "ROOMs: {room_name}".format(room_name = room)
                print(room_txt)
                rooms = allrooms[room]
                for room_list in  rooms.keys():
                    if room_list is None:
                        outdated = list(set(status_pool.keys()) - set(rooms[room_list].keys()))
                        for oid in outdated:
                            del status_pool[oid]
                        for clientSid in rooms[room_list].keys():
                            drop_check(clientSid)
                    #if room_list is not None:
                        #print(room_list_txt)
                    print("ROOM: {room_list} :: {joined_client}".format(room_list = room_list, joined_client=', '.join(rooms[room_list])))
                    #for joined_client in rooms[room_list]:
            socketio.emit('my_response', {'data': 'Server generated event:', 'count': count})
            _clientManager.debug()
            socketio.sleep(5)
        


def save_file(bytes, file_name, path):
    if not os.path.exists(path):
        os.makedirs(path)
    f = open("{path}\\{file}".format(path=path,file=file_name), "wb")
    f.write(bytes)
    f.close()

@app.route('/')
def index():
    return render_template('index.html', async_mode=socketio.async_mode)

@socketio.on('message')
def handleMessage(msg):
    print('Message: ' + msg)
    socketio.emit('my_response',with_session_count({'data': msg}))
    #socketio.send(msg, broadcast=True)

@socketio.event
def my_info():
    print(_clientManager.debug())
    print('sid: ', request.sid)
    _client = _clientManager.getClientBySid(request.sid)
    #print(_client)
    if _client is not None:
        emit('my_response', with_session_count({'data': _client.info()}))

@socketio.on('ls_all_users')
def handleListAllUsers(msg):
    for id in _clientManager.client_list:
        print(_clientManager.client_list[id].jsonInfo())
    socketio.emit('my_response', with_session_count({'data': msg}))

@socketio.on('wallpaper')
def handleWallpaper(msg):
    #print('Wallpaper :: ' + data)
    socketio.emit('wallpaper', msg, broadcast=True)

@socketio.on('csharp')
def handleCSharp(msg):
    socketio.emit('csharp', { 'data':msg['data'] }, room=msg['sId'])

@socketio.on('upgrade')
def handleUpgrade(msg):
    dist_ip = msg['dist_ip']
    print('upgrade')
    #print(msg)
    _client = _clientManager.getClientByIp(dist_ip)
    if _client is None:
        print('dist_ip not exist')
        return
    save_file(msg['data'], msg['name'], upgrade_temp_path)
    emit('upload', {'data': {'file':msg['data'], 'name':msg['name'], 'action':'upgrade', 'path':'_default_'} }, room=_client.userId)

@socketio.on('upload')
def handleUpload(msg):
    save_file(msg['data'], msg['name'], upload_temp_path)
    dist_ip = msg.get('dist_ip', None)
    upload_action = msg.get('action', 'upload')
    target_path = msg.get('path', '.\\')
    if dist_ip is not None:
        _client = _clientManager.getClientByIp(dist_ip)
        if _client is not None:
            emit('upload', {'data': {'file':msg['data'], 'name':msg['name'], 'action':upload_action, 'path':target_path} }, room=_client.userId)
    txt = "{file} upload success".format(file=msg['name'])
    emit('my_response', with_session_count({'data': txt }))

@socketio.event
def my_event(message):
    emit('my_response', with_session_count({'data': message['data']}))


@socketio.event
def my_broadcast_event(message):
    session['receive_count'] = session.get('receive_count', 0) + 1
    emit('my_response',
         {'data': message['data'], 'count': session['receive_count']},
         broadcast=True)


@socketio.event
def join(message):
    join_room(message['room'])
    session['receive_count'] = session.get('receive_count', 0) + 1
    emit('my_response',
         {'data': 'In rooms: ' + ', '.join(rooms()),
          'count': session['receive_count']})


@socketio.event
def leave(message):
    leave_room(message['room'])
    session['receive_count'] = session.get('receive_count', 0) + 1
    emit('my_response',
         {'data': 'In rooms: ' + ', '.join(rooms()),
          'count': session['receive_count']})


@socketio.on('close_room')
def on_close_room(message):
    session['receive_count'] = session.get('receive_count', 0) + 1
    emit('my_response', {'data': 'Room ' + message['room'] + ' is closing.',
                         'count': session['receive_count']},
         to=message['room'])
    close_room(message['room'])

@socketio.event
def my_room_event(message):
    session['receive_count'] = session.get('receive_count', 0) + 1
    emit('my_response',
         {'data': message['data'], 'count': session['receive_count']},
         to=message['room'])
        
def execute_disconnect_by_session_id(sid):
    if sid is None:
        return True
    _client = _clientManager.getClientBySid(sid)
    if _client is None:
        return True
    room_list = copy.deepcopy(socketio.server.manager.rooms)
    for room in room_list.keys():
        socketio.server.leave_room(sid, room)
    socketio.server.close_room(sid)
    socketio.server.disconnect(sid)
    _clientManager.rmSession(_client.ip, sid)
    sIdListCount = len(_client.sessionIdList)
    if sIdListCount == 0 :
        socketio.server.close_room(_client.userId)
    return True

# def can_disconnect(sid):
#     print('callback=can_disconnect')
#     if sid is None:
#         return True
#     _client = _clientManager.getClientBySid(sid)
#     if _client is None:
#         socketio.server.disconnect(sid)
#         return True
#     _clientManager.rmSession(_client.ip, sid)
#     room_list = copy.deepcopy(socketio.server.manager.rooms)
#     for room in room_list.keys():
#         socketio.server.leave_room(sid, room)        
#     socketio.server.close_room(sid)

#     inner_join = list(set(_client.sessionIdList) & set(room_list))
#     for sid in _client.sessionIdList:
#         if sid not in inner_join:
#             _client.rmSession(sid)
#     if _client.userId in room_list:
#         print('room_list')
#         print(room_list)
#         if len(_client.sessionIdList) == 0:            
#             socketio.server.close_room(_client.userId)
#             for room in room_list.keys():
#                 socketio.server.leave_room(_client.userId, room)
#     socketio.server.disconnect(sid)
#     print('Disconnected ::{sid}'.format(sid=sid))
#     return True

@socketio.event
def disconnect_request():
    print('who disconnect request ::{sid}'.format(sid=request.sid))
    # for this emit we use a callback function
    # when the callback function is invoked we know that the message has beenl
    # received and it is safe to disconnectl
    emit('my_response',
         with_session_count({'data': 'Disconnected!'}),
         callback=execute_disconnect_by_session_id(request.sid))

def foo(count):
    print('{count}, {ctime}'.format(count=count, ctime=time.ctime()))

@socketio.event
def get_status(message):
    data = message['data']
    num = status_pool.get(request.sid)
    flag = (num == data)
    if not flag:
        print(data)
        print(num)
    if flag :
        status_pool[request.sid] = True
    print('get_status {num} {flag}::{sid}'.format(num=data, flag=flag,sid=request.sid))

def drop_check(sid):
    global status_pool
    #print('drop_check ::{sid}'.format(sid=sid))
    if sid is None:
        return    
    try:
        num = random.random()
        status_pool[sid] = num
        print('check {num}::{sid}'.format(num=num, sid=sid))
        print(status_pool)
        socketio.call('status', {'data': num}, to=sid, timeout=5.0)
    except Exception as ex:
        if type(ex).__name__ == 'TimeoutError':
            if status_pool.get(sid) == True:
                #print('kill status OK')
                socketio.sleep(num*5)
                return
            del status_pool[sid]
            execute_disconnect_by_session_id(sid)
            print('{sid} :: fail status'.format(sid=sid))

@socketio.event
def my_ping():
    emit('my_pong')

@socketio.event
def send_to_session(message):
    _client = _clientManager.getClientBySid(message['session'])
    if _client is not None:
        emit('my_response', with_session_count({'data': message['data'] }), to=message['session'])

@socketio.event
def send_to_ip(message):
    _client = _clientManager.getClientByIp(message['ip'])
    if _client is not None:
        emit('my_response', with_session_count({'data': message['data'] }), room=_client.userId)

@socketio.event
def connect():
    global thread
    _client = _clientManager.addClient(request.remote_addr, request.sid)
    join_room(_client.userId)
    txt = "sid={sid} | {info}".format(sid=request.sid,info=_client.info())
    emit('my_response', {'data': txt, 'count': 0})    
    with thread_lock:        
        if thread is None:
            pass
            thread = socketio.start_background_task(target=background_thread)


if __name__ == '__main__':
    socketio.run(app, '0.0.0.0', 5556)
