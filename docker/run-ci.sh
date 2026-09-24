#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

echo "== dotnet unit/integration tests =="
dotnet test SocketPlayground.sln --configuration Release

echo "== python protocol tests =="
python3 -m pytest SocketIoServerPython/tests -q

echo "== node protocol tests =="
(cd SocketIoNodejs && npm test)

echo "All Docker CI suites passed."
