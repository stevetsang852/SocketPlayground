# SocketPlayground

SocketPlayground is a multi-language learning repository for comparing raw TCP and Socket.IO experiments without mixing the protocols.

## Recommended roadmap: B → C → D, but implement C first

The long-term roadmap is:

1. **Phase C first (recommended): secure raw TCP with TLS**
2. **Phase B next: UDP rendezvous / hole punching preparation**
3. **Phase D later: Linux TUN-based VPN prototyping**

Phase C is the best first milestone because it is deterministic on loopback, works in CI, and teaches transport security without requiring public IPs, NAT behavior, root privileges, or routing changes.

## Raw TCP vs Socket.IO

- `SocketServerNetCore` is a **raw TCP** playground.
- `SocketIoNodejs` and `SocketIoServerPython` are **Socket.IO** samples.
- They are intentionally separate and are **not protocol-compatible**.
- Because the Python sample is Socket.IO-only, this repository does **not** claim raw TCP ↔ Python Socket.IO wire compatibility and does not add misleading cross-language raw TCP tests.

## Raw TCP architecture

### Production/testable components

- `SocketServerNetCore/TcpPlayground/TcpPlaygroundServer.cs`
  - async loopback server with start/stop lifecycle, cancellation, concurrent clients, optional TLS via `SslStream`
- `SocketServerNetCore/TcpPlayground/TcpTestClient.cs`
  - reusable client with connect/disconnect, retries, timeouts, reconnect support, transcript capture, and optional TLS validation hooks
- `SocketServerNetCore/TcpPlayground/SocketScenarioRunner.cs`
  - terminal-oriented scenario harness that writes JSON reports
- `SocketServerNetCore/TcpPlayground/DevelopmentCertificateLoader.cs`
  - creates or loads self-signed **development-only** certificates for loopback testing

### Message protocol

Raw TCP messages use **UTF-8 newline-delimited JSON**.

```json
{
  "requestId": "8bc1eb2f9fef4a25a0d6700b489f50f4",
  "clientId": "alpha",
  "type": "echo",
  "timestampUtc": "2026-09-23T07:30:00Z",
  "payload": {
    "message": "hello tcp playground"
  }
}
```

Envelope fields:

- `requestId`: correlation ID
- `clientId`: logical client ID
- `type`: event/command type
- `timestampUtc`: UTC timestamp
- `payload`: arbitrary JSON payload

## TLS model

- TLS is implemented with **.NET `SslStream`**
- default manual/test usage is **loopback-only**
- if `--tls true` is used without a certificate path, the server creates an **ephemeral self-signed development certificate**
- tests trust the exact in-memory development certificate; they do not disable TLS globally
- you may also supply a development `.pfx` at runtime with:
  - `--tls-cert-path <path>`
  - `--tls-cert-password <password>`

### Security warnings

- self-signed certificates in this repo are for **development/testing only**
- **never** commit production private keys or secrets
- **never** invent a custom AES/key-exchange protocol when TLS already solves the problem
- **never** log secrets, credentials, or sensitive payloads

## Automated tests

### Test projects

- `CommonLibTest`
  - legacy MSTest project, with platform/network-dependent tests now skipped automatically when CI cannot support them
- `SocketServerNetCore.Tests`
  - dedicated raw TCP/TLS MSTest project added to `SocketPlayground.sln`
- `SocketIoServerPython/tests`
  - deterministic `pytest` coverage using Flask-SocketIO's in-process test client for the Python Socket.IO sample

### TLS integration coverage

`SocketServerNetCore.Tests` covers:

- protocol metadata/defaulting and newline framing
- successful TLS handshake
- encrypted echo round trip
- bidirectional broadcast/message flow
- concurrent clients
- untrusted certificate rejection
- malformed message handling with structured errors
- reconnect behavior
- timeout and cancellation behavior
- retry behavior when a loopback server starts after an initial connect failure

The scenario runner also emits concise terminal diagnostics plus a JSON report in `artifacts/socket-playground-report.json` by default.

## Phase B / Phase D preparation

- **Phase B (NAT traversal)** is deliberately documented as a future local/manual effort. GitHub-hosted CI cannot honestly validate UDP hole punching across real NAT boundaries.
- **Phase D (Linux TUN/VPN)** is kept as a platform-guarded placeholder only. CI does not require root, `/dev/net/tun`, or routing changes.
- Placeholder tests in `SocketServerNetCore.Tests/RoadmapPlaceholderTests.cs` make those limits explicit instead of over-claiming coverage.

## Local commands

From the repository root:

```bash
dotnet build SocketPlayground.sln --configuration Release
dotnet test SocketPlayground.sln --configuration Release
python -m pip install -r SocketIoServerPython/requirements.txt pytest
python -m pytest SocketIoServerPython/tests -q
```

Run the raw TCP server:

```bash
dotnet run --project SocketServerNetCore -- server --port 11000
```

Run the raw TCP TLS scenario harness:

```bash
dotnet run --project SocketServerNetCore -- scenario --tls true
```

Optional flags:

- `--port 0`
- `--report <path>`
- `--connect-timeout-ms <ms>`
- `--response-timeout-ms <ms>`
- `--retry-count <count>`
- `--retry-delay-ms <ms>`
- `--allow-untrusted true` for manual development clients only
- `--tls-cert-path <path>`
- `--tls-cert-password <password>`

## CI

GitHub Actions workflow: `.github/workflows/dotnet.yml`

It performs:

1. `dotnet restore SocketPlayground.sln`
2. `dotnet build SocketPlayground.sln --configuration Release`
3. `dotnet test SocketPlayground.sln --configuration Release`
4. `python -m pip install -r SocketIoServerPython/requirements.txt pytest`
5. `python -m pytest SocketIoServerPython/tests -q`

### CI limitations

- CI can validate loopback TCP and TLS
- CI cannot prove NAT traversal through real consumer NATs
- CI does not create Linux TUN devices or modify routes

## Safe manual validation ideas

- run the TLS scenario locally and inspect the JSON report
- connect with a manual client using the development certificate
- inspect loopback traffic with Wireshark to confirm application payloads are wrapped inside TLS records
- test certificate rejection paths with a client that does **not** trust the dev certificate
