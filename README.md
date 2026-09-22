# SocketPlayground

This repository now includes a raw TCP automation playground in `SocketServerNetCore` alongside the existing Socket.IO examples.

## Raw TCP playground architecture

- `SocketServerNetCore/TcpPlayground/TcpPlaygroundServer.cs`
  - async loopback TCP server with start/stop lifecycle controls, cancellation support, clean shutdown, and concurrent client handling
- `SocketServerNetCore/TcpPlayground/TcpTestClient.cs`
  - reusable test client with connect/send/receive helpers, configurable timeouts/retries, reconnect support, and per-client event transcripts
- `SocketServerNetCore/TcpPlayground/SocketScenarioRunner.cs`
  - executable harness that drives concurrent client scenarios and emits both terminal output and a JSON report

## Framed protocol

The raw TCP playground uses **UTF-8 newline-delimited JSON (JSONL)**. Each line is a complete message.

Example envelope:

```json
{
  "requestId": "8bc1eb2f9fef4a25a0d6700b489f50f4",
  "clientId": "alpha",
  "type": "echo",
  "timestampUtc": "2026-09-22T14:30:45.624+00:00",
  "payload": {
    "message": "hello tcp playground"
  }
}
```

Envelope fields:

- `requestId`: correlation ID for requests and responses
- `clientId`: logical client name
- `type`: event or command name
- `timestampUtc`: UTC message timestamp
- `payload`: arbitrary JSON payload

## Run commands

From the repository root:

```bash
dotnet run --project /home/runner/work/SocketPlayground/SocketPlayground/SocketServerNetCore -- server --port 11000
```

```bash
dotnet run --project /home/runner/work/SocketPlayground/SocketPlayground/SocketServerNetCore -- scenario
```

Optional scenario arguments:

- `--port 0` for an ephemeral loopback port (default)
- `--report <path>` to change the JSON report destination
- `--connect-timeout-ms <ms>`
- `--response-timeout-ms <ms>`
- `--retry-count <count>`
- `--retry-delay-ms <ms>`

## Automated scenarios

The scenario runner includes:

1. echo/round-trip validation
2. concurrent broadcast/fan-out validation
3. disconnect/reconnect behavior
4. malformed input rejection/isolation
5. timeout/error reporting

The JSON report includes pass/fail status, duration, failures, diagnostics, and captured client transcripts.

## Tests and build

Targeted TCP playground tests:

```bash
dotnet test /home/runner/work/SocketPlayground/SocketPlayground/CommonLibTest/CommonLibTest.csproj --filter TestCategory=TcpPlayground
```

Solution build:

```bash
dotnet build /home/runner/work/SocketPlayground/SocketPlayground/SocketPlayground.sln
```

## Raw TCP vs Socket.IO

`SocketServerNetCore` is a **raw TCP** playground. It does **not** implement the Socket.IO protocol.

The repository keeps separate Socket.IO experiments in:

- `SocketIoNodejs`
- `SocketIoServerPython`

Those examples use the Socket.IO protocol stack, which is distinct from the newline-delimited raw TCP protocol used here.
