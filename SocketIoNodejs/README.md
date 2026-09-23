# SocketIoNodejs

Node.js + Socket.IO chat example used in this repository's learning playground.

## What this app does

- starts an Express HTTP server
- mounts Socket.IO on top of that server
- serves `index.html`
- broadcasts `chat message` events to all connected clients

Entry point: `index.js`

## Prerequisites

- Node.js 18+
- npm

## Install

```bash
npm install
```

## Run

```bash
npm start
```

Default URL: `http://localhost:55556`

You can override the port:

```bash
PORT=3000 npm start
```

## Quick manual test

1. Open the app URL in two browser tabs.
2. Send a message in one tab.
3. Confirm both tabs receive the message.

## Notes

- This is a **Socket.IO** example, not raw TCP.
- It is intentionally separate from the .NET raw TCP server in `SocketServerNetCore`.
