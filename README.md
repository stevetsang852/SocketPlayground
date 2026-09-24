# SocketPlayground

Canonical interoperable command contract for C#, Python, and Node.js:

- transport: **raw TCP**
- security: **TLS required**, with certificate inspection on connect
- framing: **UTF-8 newline-delimited JSON**
- auth after TLS: **`authenticate` (HMAC token)** or **`login` (username/password + role)**

The existing Node.js and Python **Socket.IO** projects stay as learning samples. They are **not** wire-compatible with the canonical raw TCP/TLS contract.

## Latest flow

```text
Python / Node / .NET client
        |
        |  1. TCP connect
        v
.NET TLS server
        |
        |  2. TLS handshake + certificate check/log
        |     - server cert subject/thumbprint/expiry
        |     - optional client cert (off by default)
        v
Unusable session until auth
        |
        |  3a. authenticate { deviceId, accessToken }
        |  or
        |  3b. login { deviceId, username, password, role }
        v
Authenticated registry
        |
        +-- role=client  wait for allowlisted admin-command
        +-- role=admin   may dispatch allowlisted admin-command
        |
        |  4. admin-command -> target clients
        |  5. command-ack + command-result back to admin
        |  6. command-summary when complete or timed out
```

Local server console (interactive TTY only):

```text
help
list
send <command> all|<deviceId>
quit
```

CI / redirected stdin does **not** open the console, so the server keeps listening for automated tests.

## Compatibility boundary

Canonical path:

- `.NET server` → `SocketServerNetCore`
- `.NET tests` → `SocketServerNetCore.Tests`
- `Python client` → `SocketIoServerPython/canonical_client.py`
- `Node client` → `SocketIoNodejs/canonical-client.js`

Socket.IO-only samples (not compatible):

- `SocketIoNodejs/index.js`
- `SocketIoServerPython/server.py`
- `SocketIoServerPython/app.py`

## Rules

- TLS handshake happens before any JSON frame
- first JSON message must be `authenticate` or `login`
- after connect, a client session may send `login` again to become `admin` if credentials match
- only authenticated sessions enter the registry
- one authenticated session per `deviceId` (default `reject-new`)
- only `admin` may send `admin-command`
- only allowlisted commands are relayed
- clients never execute arbitrary shell strings

## Allowlisted commands

- `health-check`
- `refresh-config`
- `collect-diagnostics`
- `list-status`
- `ping-time`

## Protocol

Every frame is one JSON object plus `\n`.

```json
{
  "protocolVersion": "1.0",
  "requestId": "4e6f96f6f8fa4ce4bead8fd0d9b6d9df",
  "deviceId": "py-agent-1",
  "clientId": "py-agent-1",
  "role": "client",
  "type": "heartbeat",
  "correlationId": null,
  "timestampUtc": "2026-09-24T07:00:00.0000000+00:00",
  "payload": { "sequence": 1 }
}
```

`deviceId` is canonical. `clientId` is a compatibility alias.

| Type | Direction | Purpose |
|---|---|---|
| `authenticate` | client → server | token auth |
| `login` | client → server | username/password auth or admin upgrade |
| `authenticated` | server → client | success, role, expiry, allowlist |
| `heartbeat` / `heartbeat.ack` | both | keepalive |
| `admin-command` | admin → server → clients | allowlisted dispatch |
| `admin-command.accepted` | server → admin | accepted targets + timeout |
| `command-ack` | client → admin | accepted by target |
| `command-result` | client → admin | finished |
| `command-summary` | server → admin | completion / timeout |
| `error` | server → client | structured rejection |

### Token auth

```json
{ "type": "authenticate", "payload": { "deviceId": "py-agent-1", "accessToken": "..." } }
```

### Login auth

```json
{
  "type": "login",
  "payload": {
    "deviceId": "console-admin-1",
    "username": "admin",
    "password": "change-me",
    "role": "admin"
  }
}
```

- `role=admin` requires `--admin-user` / `--admin-password` (or env vars)
- `role=client` password must match `--auth-secret`

## Security model

- TLS is required
- application auth is required after TLS
- tokens are short-lived HMAC-signed blobs for local/dev use
- never commit real secrets or production certificates
- `--allow-untrusted` is only for local self-signed certs
- `--require-client-cert true` optionally demands a client certificate
- no arbitrary shell execution

## Repository layout

| Path | Purpose |
|---|---|
| `SocketServerNetCore/` | TLS raw TCP server, token issuer, scenario runner |
| `SocketServerNetCore.Tests/` | .NET protocol tests including login + cert checks |
| `SocketIoServerPython/canonical_client.py` | Python client (`authenticate` or `--login`) |
| `SocketIoNodejs/canonical-client.js` | Node client (`authenticate` or `--login`) |
| `docker/` | Docker CI image and helper scripts |
| `docker-compose.yml` | one server + Python/Node agents + Python admin |
| `SocketIoServerPython/server.py` | legacy Socket.IO sample |
| `SocketIoNodejs/index.js` | legacy Socket.IO sample |

## Setup

- .NET SDK 8.x
- Python 3.12+
- Node.js 20 (CI)
- Docker (optional CI / matrix)

```bash
export SOCKET_PLAYGROUND_AUTH_SECRET="change-this-local-dev-secret"
export SOCKET_PLAYGROUND_ADMIN_USER="admin"
export SOCKET_PLAYGROUND_ADMIN_PASSWORD="change-me"
```

## Build and test

```bash
dotnet test SocketPlayground.sln --configuration Release
python -m pip install -r SocketIoServerPython/requirements.txt pytest
python -m pytest SocketIoServerPython/tests -q
cd SocketIoNodejs && npm ci && npm test
```

## Run the server

```bash
dotnet run --project SocketServerNetCore -- server \
  --port 11000 \
  --auth-secret "$SOCKET_PLAYGROUND_AUTH_SECRET" \
  --admin-user "$SOCKET_PLAYGROUND_ADMIN_USER" \
  --admin-password "$SOCKET_PLAYGROUND_ADMIN_PASSWORD"
```

Docker / multi-container bind:

```bash
--bind 0.0.0.0
# or SOCKET_PLAYGROUND_BIND=0.0.0.0
```

Default bind remains `127.0.0.1`.

If no `--tls-cert-path` is given, the server creates a loopback development certificate in memory.

## Issue a token

```bash
dotnet run --project SocketServerNetCore -- issue-token \
  --device-id py-agent-1 \
  --role client \
  --auth-secret "$SOCKET_PLAYGROUND_AUTH_SECRET"
```

## Run clients

Python agent with token:

```bash
python SocketIoServerPython/canonical_client.py \
  --port 11000 --device-id py-agent-1 --role client \
  --auth-secret "$SOCKET_PLAYGROUND_AUTH_SECRET" --allow-untrusted
```

Python admin with login:

```bash
python SocketIoServerPython/canonical_client.py \
  --port 11000 --device-id py-admin-1 --role admin \
  --allow-untrusted --login \
  --login-user admin --login-password change-me \
  --send-command ping-time --target-mode all
```

Node agent with login:

```bash
node SocketIoNodejs/canonical-client.js \
  --port 11000 --device-id node-agent-1 --role client \
  --allow-untrusted --login \
  --login-user node-agent-1 --login-password "$SOCKET_PLAYGROUND_AUTH_SECRET"
```

## Docker

All automated suites inside one image:

```bash
docker build -f docker/Dockerfile.ci -t socketplayground-ci .
docker run --rm socketplayground-ci
```

Mixed Python + Node clients against one server:

```bash
docker compose up --build --abort-on-container-exit python-admin
```

More detail: `docker/README.md`.

CI workflows:

- `.github/workflows/dotnet.yml` — host runner tests
- `.github/workflows/docker.yml` — Docker image build + in-container tests

## Coverage

- unauthenticated / invalid token / bad login rejection
- TLS handshake before login
- certificate inspection
- duplicate `deviceId` rejection
- non-admin command denial
- allowlist rejection
- admin broadcast and targeted relay
- ACK / result / timeout summary
- reconnect + command-id dedup
- Python ↔ .NET and Node ↔ .NET interoperability
- login-as-admin then `ping-time` / `list-status`

## Legacy Socket.IO samples

```bash
cd SocketIoNodejs && npm install && npm start
cd SocketIoServerPython && python -m pip install -r requirements.txt && python server.py
```

These are **not** part of the canonical raw TCP/TLS contract.
