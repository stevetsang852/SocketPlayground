from flask import Flask 
from flask_socketio import SocketIO

app = Flask(__name__)
app.config['SECRET_KEY'] = 'mysecret'
socketio = SocketIO(app, cors_allowed_origins='*')

@socketio.on('message')
def handleMessage(msg):
    print('Message: ' + msg)
    socketio.send(msg, broadcast=True)

@socketio.on('wallpaper')
def handleWallpaper(msg):
    #print('Wallpaper :: ' + data)
    socketio.emit('wallpaper', msg, broadcast=True)

if __name__ == '__main__':
    socketio.run(app, '0.0.0.0', 5555)