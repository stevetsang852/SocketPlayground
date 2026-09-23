from __future__ import annotations

import re
import subprocess
import sys
import time
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from canonical_client import CanonicalTcpClient, create_token


AUTH_SECRET = "python-cross-language-secret"
REPO_ROOT = Path(__file__).resolve().parents[2]


@pytest.fixture
def dotnet_server():
    process = subprocess.Popen(
        [
            "dotnet",
            "run",
            "--project",
            "SocketServerNetCore",
            "--",
            "server",
            "--port",
            "0",
            "--auth-secret",
            AUTH_SECRET,
        ],
        cwd=REPO_ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
    )

    assert process.stdout is not None
    output_lines: list[str] = []
    port = None
    deadline = time.time() + 20
    while time.time() < deadline:
        line = process.stdout.readline()
        if not line:
            continue
        output_lines.append(line)
        match = re.search(r"127\.0\.0\.1:(\d+)", line)
        if match:
            port = int(match.group(1))
            break

    if port is None:
        process.kill()
        raise AssertionError(f"Failed to start dotnet server.\n{''.join(output_lines)}")

    try:
        yield port
    finally:
        process.terminate()
        process.wait(timeout=10)


def test_python_client_generates_short_lived_token():
    token = create_token(AUTH_SECRET, "py-device", "client", lifetime_seconds=30)
    assert token.count(".") == 1


def test_python_client_interoperates_with_dotnet_server(dotnet_server):
    admin = CanonicalTcpClient("127.0.0.1", dotnet_server, "py-admin", "admin", auth_secret=AUTH_SECRET, allow_untrusted_tls=True)
    agent = CanonicalTcpClient("127.0.0.1", dotnet_server, "py-agent", "client", auth_secret=AUTH_SECRET, allow_untrusted_tls=True)

    try:
        authenticated_admin = admin.connect()
        authenticated_agent = agent.connect()
        assert authenticated_admin["type"] == "authenticated"
        assert authenticated_agent["type"] == "authenticated"

        accepted = admin.send_admin_command(
            "health-check",
            command_id="python-health-check",
            target_mode="devices",
            target_device_ids=["py-agent"],
            timeout_ms=1000,
        )
        assert accepted["type"] == "admin-command.accepted"

        command, result_payload = agent.process_next_command()
        assert command["payload"]["commandName"] == "health-check"
        assert result_payload["result"]["status"] == "ok"

        ack = admin.wait_for(lambda envelope: envelope["type"] == "command-ack" and envelope.get("correlationId") == "python-health-check")
        result = admin.wait_for(lambda envelope: envelope["type"] == "command-result" and envelope.get("correlationId") == "python-health-check")
        summary = admin.wait_for(lambda envelope: envelope["type"] == "command-summary" and envelope.get("correlationId") == "python-health-check")

        assert ack["deviceId"] == "py-agent"
        assert result["payload"]["result"]["status"] == "ok"
        assert summary["payload"]["completedDeviceIds"] == ["py-agent"]
        assert summary["payload"]["timedOut"] is False
    finally:
        admin.close()
        agent.close()
