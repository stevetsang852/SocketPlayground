# SocketPlayground

SocketPlayground now has one **canonical interoperable command-and-control contract** for C#, Python, and Node.js:

- transport: **raw TCP**
- security: **TLS required**
- framing: **UTF-8 newline-delimited JSON**
- auth: **short-lived HMAC-signed token**

The existing Node.js and Python **Socket.IO** projects are still kept as learning samples, but they are **not wire-compatible** with the canonical raw TCP/TLS contract.

## Compatibility boundary

### Canonical interoperable path

These pieces all speak the same contract:

- `.NET server` → `SocketServerNetCore`
- `.NET test/admin/client helpers` → `SocketServerNetCore.Tests` + `SocketServerNetCore/TcpPlayground`
- `Python canonical client` → `SocketIoServerPython/canonical_client.py`
- `Node canonical client` → `SocketIoNodejs/canonical-client.js`

### Retained learning samples

These remain **Socket.IO-only** examples:

- `SocketIoNodejs/index.js`
- `SocketIoServerPython/server.py`
- `SocketIoServerPython/app.py`

Do **not** connect the raw TCP/TLS server to the Socket.IO samples directly.

## Architecture

```text
admin client (authenticated, role=admin)
    |
    |  admin-command
    v
central TLS raw TCP server
    |
    +--> authenticated client device A (role=client)
    +--> authenticated client device B (role=client)
    +--> authenticated client device C (role=client)
```

Rules:

- a connection is unusable until `authenticate` succeeds
- only authenticated sessions enter the active registry
- exactly one authenticated session is allowed per stable `deviceId`
- default duplicate policy is **reject-new**
- only `admin` clients may send `admin-command`
- only allowlisted commands are relayable/executable
- clients never execute arbitrary shell strings

## Supported allowlisted commands

- `health-check`
- `refresh-config`
- `collect-diagnostics`

The sample clients map these names to safe in-process handlers only.

## Protocol contract

Every frame is one JSON object plus `\n`.

### Shared envelope

```json
{
  "protocolVersion": "1.0",
  "requestId": "4e6f96f6f8fa4ce4bead8fd0d9b6d9df",
  "deviceId": "py-agent-1",
  "clientId": "py-agent-1",
  "role": "client",
  "type": "heartbeat",
  "correlationId": null,
  "timestampUtc": "2026-09-23T08:39:47.4700000+00:00",
  "payload": {
    "sequence": 1
  }
}
```

`clientId` is kept as a compatibility alias for older playground code; `deviceId` is the canonical identifier.

### Message types

| Type | Direction | Purpose |
|---|---|---|
| `authenticate` | client → server | first message only; contains token |
| `authenticated` | server → client | auth success, role, expiry, allowlist |
| `heartbeat` | client → server | keepalive / liveness |
| `heartbeat.ack` | server → client | heartbeat response |
| `admin-command` | admin → server, then server → clients | allowlisted command dispatch |
| `admin-command.accepted` | server → admin | accepted targets + timeout |
| `command-ack` | client → server, then server → admin | command accepted by target |
| `command-result` | client → server, then server → admin | command finished |
| `command-summary` | server → admin | aggregated completion/timeout summary |
| `error` | server → client | structured rejection/error |

### Authentication flow

1. TLS handshake completes
2. client must send `authenticate` before the auth deadline
3. server validates:
   - token signature
   - expiry
   - role
   - `deviceId` match
4. only then is the session added to the authenticated registry

If auth is absent, malformed, expired, invalid, unauthorized, or duplicated, the server returns `error` and closes the connection.

### Duplicate device policy

Default: `reject-new`

- first authenticated session for a `deviceId` stays active
- later authenticated session for the same `deviceId` is rejected safely

Optional server setting:

- `replace-existing`

## Security model

- TLS is required
- application auth is required
- tokens are short-lived HMAC-signed blobs for local/dev use
- **never commit** real secrets or production certificates
- `--allow-untrusted` is only for local development with self-signed certs
- no arbitrary shell/PowerShell/bash execution is implemented
- server validates admin role, target selection, and command allowlist
- logs are audit-friendly but still educational, not production SIEM logging

## Repository layout

| Path | Purpose |
|---|---|
| `SocketServerNetCore/` | canonical TLS raw TCP server, token issuer, scenario runner |
| `SocketServerNetCore.Tests/` | .NET protocol/integration tests |
| `SocketIoServerPython/canonical_client.py` | canonical Python raw TCP/TLS client |
| `SocketIoNodejs/canonical-client.js` | canonical Node raw TCP/TLS client |
| `SocketIoServerPython/server.py` | legacy Socket.IO sample |
| `SocketIoNodejs/index.js` | legacy Socket.IO sample |

## Setup

Prerequisites:

- .NET SDK 8.x
- Python 3.12+
- Node.js 18+ (`20` used in CI)

## Build and test

From repo root:

```bash
dotnet restore SocketPlayground.sln
dotnet build SocketPlayground.sln --configuration Release
dotnet test SocketServerNetCore.Tests/SocketServerNetCore.Tests.csproj --configuration Release
python -m pip install -r SocketIoServerPython/requirements.txt pytest
python -m pytest SocketIoServerPython/tests -q
cd SocketIoNodejs && npm ci && npm test
```

## Running the canonical server

Set a dev secret first:

```bash
export SOCKET_PLAYGROUND_AUTH_SECRET="change-this-local-dev-secret"
```

Start the server:

```bash
dotnet run --project SocketServerNetCore -- server --port 11000 --auth-secret "$SOCKET_PLAYGROUND_AUTH_SECRET"
```

The server always uses TLS. If you do not provide `--tls-cert-path`, it generates a loopback development certificate in memory.

## Issuing a token manually

```bash
dotnet run --project SocketServerNetCore -- issue-token \
  --device-id py-agent-1 \
  --role client \
  --auth-secret "$SOCKET_PLAYGROUND_AUTH_SECRET"
```

## Running canonical clients

### Python agent

```bash
python SocketIoServerPython/canonical_client.py \
  --port 11000 \
  --device-id py-agent-1 \
  --role client \
  --auth-secret "$SOCKET_PLAYGROUND_AUTH_SECRET" \
  --allow-untrusted
```

### Node agent

```bash
node SocketIoNodejs/canonical-client.js \
  --port 11000 \
  --device-id node-agent-1 \
  --role client \
  --auth-secret "$SOCKET_PLAYGROUND_AUTH_SECRET" \
  --allow-untrusted
```

### Node admin sending a targeted command

```bash
node SocketIoNodejs/canonical-client.js \
  --port 11000 \
  --device-id node-admin-1 \
  --role admin \
  --auth-secret "$SOCKET_PLAYGROUND_AUTH_SECRET" \
  --allow-untrusted \
  --send-command health-check \
  --target-mode devices \
  --target-device-id py-agent-1
```

### Python admin broadcasting a command

```bash
python SocketIoServerPython/canonical_client.py \
  --port 11000 \
  --device-id py-admin-1 \
  --role admin \
  --auth-secret "$SOCKET_PLAYGROUND_AUTH_SECRET" \
  --allow-untrusted \
  --send-command collect-diagnostics \
  --target-mode all
```

## Scenario runner

```bash
dotnet run --project SocketServerNetCore -- scenario --auth-secret "$SOCKET_PLAYGROUND_AUTH_SECRET"
```

This runs local authenticated integration scenarios and writes a JSON report.

## Testing coverage

The automated suite now covers:

- unauthenticated rejection
- invalid token rejection
- duplicate `deviceId` rejection
- non-admin command denial
- allowlist rejection
- admin broadcast/targeted command relay
- ACK/result aggregation
- malformed frame isolation
- timeout summaries
- reconnect + command-id dedup guidance
- Python ↔ .NET interoperability
- Node ↔ .NET interoperability

## Reconnect, timeout, and dedup guidance

- reconnect is allowed after the prior authenticated session closes
- command dispatch includes a timeout and produces `command-summary`
- sample clients keep an in-memory processed `commandId` set and mark repeated execution as `duplicate=true`
- for real production use, dedup state should be persisted durably per device

## Legacy Socket.IO samples

These are still useful for learning Socket.IO patterns:

- `cd SocketIoNodejs && npm install && npm start`
- `cd SocketIoServerPython && python -m pip install -r requirements.txt && python server.py`

Again: they are **not** part of the canonical raw TCP/TLS contract.
