# SocketIoNodejs

This folder now contains **two different Node.js examples**:

1. `index.js` → legacy **Socket.IO** chat sample
2. `canonical-client.js` → canonical **raw TCP/TLS client** for interoperating with `SocketServerNetCore`

Do not treat them as wire-compatible.

## Canonical interoperable client

`canonical-client.js` speaks the repository's shared contract:

- raw TCP
- TLS required
- UTF-8 newline-delimited JSON
- HMAC-signed short-lived auth token

### Run as authenticated client/agent

```bash
export SOCKET_PLAYGROUND_AUTH_SECRET="change-this-local-dev-secret"

node canonical-client.js \
  --port 11000 \
  --device-id node-agent-1 \
  --role client \
  --auth-secret "$SOCKET_PLAYGROUND_AUTH_SECRET" \
  --allow-untrusted
```

### Run as admin and send a command

```bash
node canonical-client.js \
  --port 11000 \
  --device-id node-admin-1 \
  --role admin \
  --auth-secret "$SOCKET_PLAYGROUND_AUTH_SECRET" \
  --allow-untrusted \
  --send-command collect-diagnostics \
  --target-mode all
```

Safe handlers implemented by the sample client:

- `health-check`
- `refresh-config`
- `collect-diagnostics`

No arbitrary shell execution is supported.

## Legacy Socket.IO sample

Install dependencies:

```bash
npm install
```

Run the sample:

```bash
npm start
```

Default URL: `http://localhost:55556`

This app is a Socket.IO chat demo only.

## Tests

```bash
npm ci
npm test
```

The Node test suite now validates the canonical Node client against the .NET server.
