from __future__ import annotations

import os
import queue
import re
import signal
import subprocess
import sys
import threading
import time
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from canonical_client import CanonicalTcpClient, create_token


AUTH_SECRET = "python-cross-language-secret"
ADMIN_USER = "admin"
ADMIN_PASSWORD = "admin-pass"
REPO_ROOT = Path(__file__).resolve().parents[2]

# Cold CI runners can exceed 20s for first compile+start; default 120s, overridable.
STARTUP_TIMEOUT_S = float(os.environ.get("DOTNET_SERVER_STARTUP_TIMEOUT_S", "120"))

_server_build_done = False


def _ensure_server_built() -> None:
    """Compile SocketServerNetCore once so `dotnet run --no-build` is not paying cold-compile time."""
    global _server_build_done
    if _server_build_done:
        return

    result = subprocess.run(
        [
            "dotnet",
            "build",
            "SocketServerNetCore/SocketServerNetCore.csproj",
            "-nologo",
            "-v",
            "q",
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        raise AssertionError(
            "dotnet build SocketServerNetCore failed "
            f"({result.returncode}).\n{result.stdout}\n{result.stderr}"
        )
    _server_build_done = True


def _kill_server_tree(process: subprocess.Popen[str]) -> None:
    """Terminate the server and its process group so orphans cannot stall CI."""
    if process.poll() is not None:
        return

    pid = process.pid
    try:
        if pid and os.name != "nt":
            try:
                os.killpg(pid, signal.SIGTERM)
            except (ProcessLookupError, PermissionError):
                pass
        process.terminate()
    except Exception:
        pass

    try:
        process.wait(timeout=2)
        return
    except subprocess.TimeoutExpired:
        pass

    try:
        if pid and os.name != "nt":
            try:
                os.killpg(pid, signal.SIGKILL)
            except (ProcessLookupError, PermissionError):
                pass
        process.kill()
    except Exception:
        pass

    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        pass


def _start_stdout_reader(process: subprocess.Popen[str]) -> queue.Queue[str | None]:
    """
    Dedicated daemon thread that reads lines from process.stdout into a queue.

    Avoids the select()+buffered TextIOWrapper.readline() mismatch (port line can
    sit in Python's buffer while select sees an empty OS pipe) and avoids a
    blocking stdout.read() drain after poll() when `dotnet run`'s child still holds
    the pipe open.
    """
    line_queue: queue.Queue[str | None] = queue.Queue()
    assert process.stdout is not None

    def _reader() -> None:
        try:
            for line in process.stdout:
                line_queue.put(line)
        except Exception:
            pass
        finally:
            # Sentinel so the readiness loop can stop if the stream ends early.
            line_queue.put(None)

    thread = threading.Thread(target=_reader, name="dotnet-server-stdout", daemon=True)
    thread.start()
    return line_queue


@pytest.fixture
def dotnet_server():
    _ensure_server_built()

    process = subprocess.Popen(
        [
            "dotnet",
            "run",
            "--project",
            "SocketServerNetCore",
            "--no-build",
            "--",
            "server",
            "--port",
            "0",
            "--auth-secret",
            AUTH_SECRET,
            "--admin-user",
            ADMIN_USER,
            "--admin-password",
            ADMIN_PASSWORD,
        ],
        cwd=REPO_ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        # Own process group so we can killpg the whole tree on timeout/teardown (Linux).
        start_new_session=(os.name != "nt"),
    )

    line_queue = _start_stdout_reader(process)
    output_lines: list[str] = []
    port = None
    deadline = time.time() + STARTUP_TIMEOUT_S

    try:
        # Readiness: consume stdout lines from the reader thread until we see the
        # listen address (scanning stdout — not a TCP port poll).
        while time.time() < deadline:
            remaining = max(0.05, deadline - time.time())
            try:
                line = line_queue.get(timeout=min(1.0, remaining))
            except queue.Empty:
                if process.poll() is not None and line_queue.empty():
                    break
                continue

            if line is None:
                # stdout stream ended
                break

            output_lines.append(line)
            match = re.search(r"127\.0\.0\.1:(\d+)", line)
            if match:
                port = int(match.group(1))
                break

        if port is None:
            _kill_server_tree(process)
            # Drain any remaining queued lines for the error message (non-blocking).
            while True:
                try:
                    extra = line_queue.get_nowait()
                except queue.Empty:
                    break
                if extra is None:
                    break
                output_lines.append(extra)
            raise AssertionError(
                f"Failed to start dotnet server within {STARTUP_TIMEOUT_S:.0f}s.\n"
                f"{''.join(output_lines)}"
            )

        yield port
    finally:
        _kill_server_tree(process)


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


def test_python_admin_login_after_tls(dotnet_server):
    admin = CanonicalTcpClient(
        "127.0.0.1",
        dotnet_server,
        "py-login-admin",
        "admin",
        allow_untrusted_tls=True,
        use_login=True,
        login_username=ADMIN_USER,
        login_password=ADMIN_PASSWORD,
        timeout_seconds=3.0,
    )
    agent = CanonicalTcpClient(
        "127.0.0.1",
        dotnet_server,
        "py-login-agent",
        "client",
        allow_untrusted_tls=True,
        use_login=True,
        login_username="py-login-agent",
        login_password=AUTH_SECRET,
        timeout_seconds=3.0,
    )

    try:
        authenticated_admin = admin.connect()
        authenticated_agent = agent.connect()
        assert authenticated_admin["type"] == "authenticated"
        assert authenticated_admin["payload"]["role"] == "admin"
        assert authenticated_admin["payload"]["method"] == "login"
        assert authenticated_agent["payload"]["role"] == "client"

        accepted = admin.send_admin_command(
            "ping-time",
            command_id="python-login-ping",
            target_mode="devices",
            target_device_ids=["py-login-agent"],
            timeout_ms=1000,
        )
        assert accepted["type"] == "admin-command.accepted"

        command, result_payload = agent.process_next_command()
        assert command["payload"]["commandName"] == "ping-time"
        assert result_payload["result"]["status"] == "pong"

        summary = admin.wait_for(lambda envelope: envelope["type"] == "command-summary" and envelope.get("correlationId") == "python-login-ping")
        assert summary["payload"]["completedDeviceIds"] == ["py-login-agent"]
    finally:
        admin.close()
        agent.close()


def test_python_invalid_admin_login_is_rejected(dotnet_server):
    admin = CanonicalTcpClient(
        "127.0.0.1",
        dotnet_server,
        "py-bad-admin",
        "admin",
        allow_untrusted_tls=True,
        use_login=True,
        login_username=ADMIN_USER,
        login_password="wrong-password",
        timeout_seconds=3.0,
    )

    try:
        with pytest.raises(RuntimeError, match="Login failed"):
            admin.connect()
    finally:
        admin.close()
