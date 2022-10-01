const app = require('express')();
const http = require('http').Server(app);
const io = require('socket.io')(http);
const port = process.env.PORT || 55556;

app.get('/', (req, res) => {
  res.sendFile(__dirname + '/index.html');
});

io.on('connection', (socket) => {
  socket.on('chat message', msg => {
    io.emit('chat message', msg);
  });
});

io.on('message', (socket) => {
  socket.on('message', msg => {
    socket.broadcast.emit("message", msg);
  });
});

io.on('wallpaper', (socket) => {
  socket.on('wallpaper', msg => {
    socket.broadcast.emit("wallpaper", msg);
  });
});

http.listen(port, () => {
  console.log(`Socket.IO server running at http://localhost:${port}/`);
});
