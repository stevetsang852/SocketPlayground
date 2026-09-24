# Docker CI and multi-client playground

Images:
- `SocketServerNetCore/Dockerfile` — TLS command server
- `SocketIoServerPython/Dockerfile` — Python canonical client
- `SocketIoNodejs/Dockerfile` — Node canonical client
- `docker/Dockerfile.ci` — runs dotnet + pytest + node tests inside one image

Run the existing automated suites in Docker:

```bash
docker build -f docker/Dockerfile.ci -t socketplayground-ci .
docker run --rm socketplayground-ci
```

Run mixed Python + Node clients against one server:

```bash
docker compose up --build --abort-on-container-exit python-admin
```

The server must bind `0.0.0.0` so other containers can connect. Local loopback-only mode remains the default outside Docker.
