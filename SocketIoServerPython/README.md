# SocketIoServerPython

This folder now contains **two different Python learning surfaces**:

1. `server.py` / `app.py` → **Socket.IO** examples
2. `canonical_client.py` → **canonical raw TCP/TLS client** for interoperating with `SocketServerNetCore`

They are not the same protocol.

## Canonical interoperable client

`canonical_client.py` speaks the repository's shared contract:

- raw TCP
- TLS required
- UTF-8 newline-delimited JSON
- HMAC-signed short-lived auth token

### Run as authenticated client/agent

```bash
export SOCKET_PLAYGROUND_AUTH_SECRET="change-this-local-dev-secret"

python canonical_client.py \
  --port 11000 \
  --device-id py-agent-1 \
  --role client \
  --auth-secret "$SOCKET_PLAYGROUND_AUTH_SECRET" \
  --allow-untrusted
```

### Run as admin and send a command

```bash
python canonical_client.py \
  --port 11000 \
  --device-id py-admin-1 \
  --role admin \
  --auth-secret "$SOCKET_PLAYGROUND_AUTH_SECRET" \
  --allow-untrusted \
  --send-command health-check \
  --target-mode devices \
  --target-device-id py-agent-1
```

Safe handlers implemented by the sample client:

- `health-check`
- `refresh-config`
- `collect-diagnostics`

No arbitrary shell execution is supported.

## Socket.IO learning samples

### Full sample

```bash
python server.py
```

### Minimal sample

```bash
python app.py
```

These are **Socket.IO** examples only and do not connect directly to the raw TCP server.

## Install dependencies

From this folder:

```bash
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
```

## Run tests

From repository root:

```bash
python -m pip install -r SocketIoServerPython/requirements.txt pytest
python -m pytest SocketIoServerPython/tests -q
```

This now includes:

- existing Socket.IO tests
- canonical Python client interoperability tests against the .NET server
