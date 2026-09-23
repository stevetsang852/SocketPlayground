# SocketIoServerPython

Python Socket.IO playground app for this repository.

## What this folder contains

- `server.py`: main Flask-SocketIO sample server with room/session/event flows
- `app.py`: minimal Socket.IO message broadcast sample
- `client.py`, `client_manager.py`: helper classes used by `server.py`
- `templates/index.html`: browser UI template used by Flask app
- `tests/test_socketio_server.py`: pytest tests for Socket.IO behavior and client manager logic

## Prerequisites

- Python 3.12+
- pip

## Install dependencies

From this folder:

```bash
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
```

## Run the main server

```bash
python server.py
```

Default URL: `http://localhost:5556`

## Run the minimal sample

```bash
python app.py
```

## Run tests

From repository root:

```bash
python -m pip install -r SocketIoServerPython/requirements.txt pytest
python -m pytest SocketIoServerPython/tests -q
```

## Notes

- This project uses **Socket.IO** over HTTP/WebSocket transport, not raw TCP.
- It is not wire-compatible with the raw TCP implementation in `SocketServerNetCore`.
- Some handlers in `server.py` are experimental and intended for local learning/testing.
