from http import client
from threading import Lock
from unicodedata import name
from flask import Flask, render_template, session, request, \
    copy_current_request_context
from flask_socketio import SocketIO, emit, join_room, leave_room, \
    close_room, rooms, disconnect

from client_manager import ClientManager
from client import  Client

import os


# Set this variable to "threading", "eventlet" or "gevent" to test the
# different async modes, or leave it set to None for the application to choose
# the best option based on installed packages.
async_mode = None

app = Flask(__name__)
app.config['SECRET_KEY'] = 'secret!'

MAX_BUFFER_SIZE = 100 * 1000 * 1000  # 100 MB

upload_temp_path = 'temp/upload'
upgrade_temp_path = 'temp/upgrade'

socketio = SocketIO(app, async_mode=async_mode , max_http_buffer_size=MAX_BUFFER_SIZE)
thread = None
thread_lock = Lock()

_clientManager = ClientManager()

def with_session_count(data):
    session['receive_count'] = session.get('receive_count', 0) + 1
    data['count'] = session['receive_count']
    return data

def background_thread():
    """Example of how to send server generated events to clients."""
    count = 0
    #socketio.server.manager.rooms.keys()
    #socketio.server.manager.rooms[self.namespace].keys() #with namespace
    while True:
        socketio.sleep(5)
        count += 1

        for room in  socketio.server.manager.rooms.keys():
            room_txt = "ROOMs: {room_name}".format(room_name = room)
            print(room_txt)
            for room_list in  socketio.server.manager.rooms[room].keys():
                room_list_txt = "ROOM: {room_list}".format(room_list = room_list)
                print(room_list_txt)

        socketio.emit('my_response',
                      {'data': 'Server generated event:', 'count': count})
        


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

@socketio.on('wallpaper')
def handleWallpaper(msg):
    #print('Wallpaper :: ' + data)
    socketio.emit('wallpaper', msg, broadcast=True)

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


@socketio.event
def disconnect_request():
    print('who disconnect request')
    print(request.sid)
    @copy_current_request_context
    def can_disconnect():
        leave_room(request.sid)
        close_room(request.sid)
        _client = _clientManager.getClientBySid(request.sid)
        _clientManager.rmSession(request.remote_addr, request.sid)
        if _client is None:
            disconnect()
            return
        leave_room(_client.userId)
        room_list = socketio.server.manager.rooms['/'].keys()
        inner_join = list(set(_client.sessionIdList) & set(room_list))
        for sid in _client.sessionIdList:
            if sid not in inner_join:
                _client.rmSession(sid)
        if _client.userId in room_list:
            print('room_list')
            print(room_list)
            if len(_client.sessionIdList) == 0:
                close_room(_client.userId)
        disconnect()

    # for this emit we use a callback function
    # when the callback function is invoked we know that the message has beenl
    # received and it is safe to disconnectl
    emit('my_response',
         with_session_count({'data': 'Disconnected!'}),
         callback=can_disconnect)


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

    with thread_lock:
        if thread is None:
            pass
            #thread = socketio.start_background_task(background_thread)
    print('connect ::', request.remote_addr)
    _client = _clientManager.addClient(request.remote_addr, request.sid)

    join_room(_client.userId)
    txt = "sid={sid} | {info}".format(sid=request.sid,info=_client.info())
    emit('my_response', {'data': txt, 'count': 0})


@socketio.on('disconnect')
def test_disconnect():
    print('Client disconnected', request.sid)


if __name__ == '__main__':
    socketio.run(app, '0.0.0.0', 5556)
