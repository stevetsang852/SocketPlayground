# SocketPlayground

SocketPlayground is an educational, multi-language **socket learning playground**.

It is designed to help you compare:

- **Raw TCP** patterns in .NET (`SocketServerNetCore`)
- **Socket.IO** patterns in Node.js (`SocketIoNodejs`) and Python (`SocketIoServerPython`)

> This repository is for learning and experimentation, not a production-ready single app.

## Why this repo exists

Use this repository to learn and practice:

- socket server/client lifecycle (start, connect, disconnect, stop)
- message framing and protocol contracts
- timeout/retry/cancellation patterns
- TLS over raw TCP in .NET
- Socket.IO event-based communication flows
- testable socket code with CI

## Raw TCP vs Socket.IO (important)

These are different protocols and are intentionally separated in this repo:

- `SocketServerNetCore` = **raw TCP** (+ optional TLS with `SslStream`)
- `SocketIoNodejs` = **Socket.IO** (Node.js)
- `SocketIoServerPython` = **Socket.IO** (Flask-SocketIO)

They are **not wire-compatible** with each other.

## Prerequisites

Install these tools before running examples:

- [.NET SDK 8.x](https://dotnet.microsoft.com/download)
- [Node.js 18+ and npm](https://nodejs.org/)
- [Python 3.12+ and pip](https://www.python.org/)

## Project layout

| Path | Type | Purpose | How to use |
|---|---|---|---|
| `SocketServerNetCore/` | .NET app | Main raw TCP playground server/scenario runner | Run server or scenario commands (see below) |
| `SocketServerNetCore.Tests/` | .NET tests | Raw TCP/TLS protocol and integration tests | `dotnet test` |
| `SocketIoNodejs/` | Node.js app | Simple Socket.IO chat demo | `npm install` + `npm start` |
| `SocketIoServerPython/` | Python app | Flask-SocketIO sample server and related client helpers | `pip install -r requirements.txt`, run `server.py` |
| `CommonClassLibrary/` | .NET library | Shared utilities referenced by other projects | Built via solution |
| `CommonLibTest/` | .NET tests | Legacy/common library tests | Included in solution tests |
| `PayloadNetNode/` | .NET app | Legacy/experimental payload automation code | Advanced/maintainer-oriented |
| `DLLLibrary/` | .NET library | Supporting library for payload experiments | Advanced/maintainer-oriented |
| `WatchDogNetCore/` | .NET app | Minimal watchdog console sample | Advanced/maintainer-oriented |

## How each main component works

### 1) `SocketServerNetCore` (raw TCP)

- Entry point: `SocketServerNetCore/Program.cs`
- Modes:
  - `server`: run raw TCP server on loopback
  - `scenario`: run built-in automated scenario tests and output JSON report
- Protocol:
  - UTF-8 newline-delimited JSON envelope
  - fields: `requestId`, `clientId`, `type`, `timestampUtc`, `payload`
- Optional TLS:
  - enabled with `--tls true`
  - uses `SslStream`
  - can use generated dev certificate or provided `.pfx`

### 2) `SocketIoNodejs` (Socket.IO chat)

- Entry point: `SocketIoNodejs/index.js`
- Serves `index.html`
- Handles `chat message` event and broadcasts to connected clients
- Default port: `55556` (`PORT` env var can override)

### 3) `SocketIoServerPython` (Flask-SocketIO)

- Main files:
  - `SocketIoServerPython/server.py` (full Socket.IO sample server)
  - `SocketIoServerPython/app.py` (minimal Socket.IO message echo sample)
- Includes:
  - room/session patterns
  - event handlers for message routing
  - `tests/test_socketio_server.py` with pytest-based coverage
- Default port: `5556`

## Setup and run

### A. Clone and restore/build .NET solution

From repository root:

```bash
dotnet restore SocketPlayground.sln
dotnet build SocketPlayground.sln --configuration Release
```

### B. Run the .NET raw TCP server

```bash
dotnet run --project SocketServerNetCore -- server --port 11000
```

TLS mode:

```bash
dotnet run --project SocketServerNetCore -- server --port 11000 --tls true
```

Run automated scenario mode (writes JSON report):

```bash
dotnet run --project SocketServerNetCore -- scenario --tls true
```

### C. Run Node.js Socket.IO chat example

```bash
cd SocketIoNodejs
npm install
npm start
```

Open `http://localhost:55556` in two browser tabs and send messages.

### D. Run Python Socket.IO app

```bash
cd SocketIoServerPython
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
python server.py
```

Open `http://localhost:5556`.

If you want the smaller sample instead:

```bash
python app.py
```

## Tests

From repository root:

```bash
dotnet test SocketPlayground.sln --configuration Release
python -m pip install -r SocketIoServerPython/requirements.txt pytest
python -m pytest SocketIoServerPython/tests -q
```

## CI

GitHub Actions workflow: `.github/workflows/dotnet.yml`

Current pipeline validates:

1. .NET restore/build/test for the solution
2. Python dependency install from `SocketIoServerPython/requirements.txt`
3. Python tests from `SocketIoServerPython/tests`

## Caveats and safety notes

- This repo mixes multiple experiments; some projects are legacy and maintainer-oriented.
- Keep raw TCP and Socket.IO expectations separate; do not assume cross-protocol compatibility.
- TLS certificates here are for development/testing unless you explicitly provide production-grade cert management.
- Do not commit secrets, tokens, or private keys.

## Where to start (recommended learning path)

1. Start with `SocketServerNetCore` in `server` mode.
2. Run `scenario` mode to understand automated socket behavior and reports.
3. Run Node.js and Python Socket.IO examples to compare event-driven Socket.IO flow vs raw TCP framing.
4. Read tests in `SocketServerNetCore.Tests` and `SocketIoServerPython/tests` to learn expected behavior.
