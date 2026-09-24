import argparse
import base64
import hashlib
import hmac
import json
import os
import platform
import socket
import ssl
import time
import uuid
from datetime import datetime, timedelta, timezone


PROTOCOL_VERSION = "1.0"
ALLOWED_COMMANDS = {"health-check", "refresh-config", "collect-diagnostics", "list-status", "ping-time"}


def _utc_now():
    return datetime.now(timezone.utc)


def _base64url_encode(raw: bytes) -> str:
    return base64.urlsafe_b64encode(raw).rstrip(b"=").decode("ascii")


def create_token(secret: str, device_id: str, role: str, lifetime_seconds: int = 600) -> str:
    payload = {
        "deviceId": device_id,
        "role": role,
        "expiresAtUtc": (_utc_now() + timedelta(seconds=lifetime_seconds)).isoformat(),
        "protocolVersion": PROTOCOL_VERSION,
    }
    payload_bytes = json.dumps(payload, separators=(",", ":"), sort_keys=True).encode("utf-8")
    signature = hmac.new(secret.encode("utf-8"), payload_bytes, hashlib.sha256).digest()
    return f"{_base64url_encode(payload_bytes)}.{_base64url_encode(signature)}"


class CanonicalTcpClient:
    def __init__(
        self,
        host: str,
        port: int,
        device_id: str,
        role: str,
        auth_secret: str | None = None,
        access_token: str | None = None,
        allow_untrusted_tls: bool = False,
        timeout_seconds: float = 2.0,
    ) -> None:
        self.host = host
        self.port = port
        self.device_id = device_id
        self.role = role
        self.auth_secret = auth_secret
        self.access_token = access_token
        self.allow_untrusted_tls = allow_untrusted_tls
        self.timeout_seconds = timeout_seconds
        self._socket: ssl.SSLSocket | None = None
        self._reader = None
        self._writer = None
        self._backlog: list[dict] = []
        self._processed_command_ids: set[str] = set()

    def connect(self) -> dict:
        raw_socket = socket.create_connection((self.host, self.port), timeout=self.timeout_seconds)
        raw_socket.settimeout(self.timeout_seconds)
        if self.allow_untrusted_tls:
            context = ssl._create_unverified_context()
        else:
            context = ssl.create_default_context()

        self._socket = context.wrap_socket(raw_socket, server_hostname="localhost")
        self._reader = self._socket.makefile("r", encoding="utf-8", newline="\n")
        self._writer = self._socket.makefile("w", encoding="utf-8", newline="\n")
        return self.authenticate()

    def close(self) -> None:
        if self._writer is not None:
            self._writer.close()
        if self._reader is not None:
            self._reader.close()
        if self._socket is not None:
            self._socket.close()
        self._writer = None
        self._reader = None
        self._socket = None

    def authenticate(self) -> dict:
        token = self.access_token or create_token(self.auth_secret or "", self.device_id, self.role)
        request_id = self.send_message("authenticate", {"deviceId": self.device_id, "accessToken": token})
        response = self.wait_for(lambda envelope: envelope["requestId"] == request_id and envelope["type"] in {"authenticated", "error"})
        if response["type"] == "error":
            raise RuntimeError(response["payload"]["message"])
        return response

    def send_message(
        self,
        message_type: str,
        payload: dict | None = None,
        *,
        request_id: str | None = None,
        correlation_id: str | None = None,
        device_id: str | None = None,
        role: str | None = None,
    ) -> str:
        if self._writer is None:
            raise RuntimeError("Client is not connected.")

        request_id = request_id or uuid.uuid4().hex
        envelope = {
            "protocolVersion": PROTOCOL_VERSION,
            "requestId": request_id,
            "deviceId": device_id or self.device_id,
            "clientId": device_id or self.device_id,
            "role": role or self.role,
            "type": message_type,
            "correlationId": correlation_id,
            "timestampUtc": _utc_now().isoformat(),
            "payload": payload,
        }
        self._writer.write(json.dumps(envelope, separators=(",", ":")) + "\n")
        self._writer.flush()
        return request_id

    def wait_for(self, predicate, timeout_seconds: float | None = None) -> dict:
        deadline = time.monotonic() + (timeout_seconds or self.timeout_seconds)
        backlog: list[dict] = []
        while time.monotonic() < deadline:
            envelope = self.read_message(max(0.1, deadline - time.monotonic()))
            if predicate(envelope):
                for item in reversed(backlog):
                    self._push_back(item)
                return envelope
            backlog.append(envelope)
        for item in reversed(backlog):
            self._push_back(item)
        raise TimeoutError("Timed out waiting for a matching message.")

    def _push_back(self, envelope: dict) -> None:
        self._backlog.insert(0, envelope)

    def _pop_backlog(self):
        if self._backlog:
            return self._backlog.pop(0)
        return None

    def read_message(self, timeout_seconds: float | None = None) -> dict:
        queued = self._pop_backlog()
        if queued is not None:
            return queued

        if self._socket is None or self._reader is None:
            raise RuntimeError("Client is not connected.")

        previous_timeout = self._socket.gettimeout()
        self._socket.settimeout(timeout_seconds or self.timeout_seconds)
        try:
            line = self._reader.readline()
        finally:
            self._socket.settimeout(previous_timeout)

        if not line:
            raise RuntimeError("Connection closed by server.")
        return json.loads(line)

    def send_admin_command(self, command_name: str, *, command_id: str | None = None, target_mode: str = "all", target_device_ids=None, timeout_ms: int = 1000) -> dict:
        request_id = self.send_message(
            "admin-command",
            {
                "commandId": command_id or uuid.uuid4().hex,
                "commandName": command_name,
                "targetMode": target_mode,
                "targetDeviceIds": target_device_ids or [],
                "timeoutMs": timeout_ms,
            },
        )
        return self.wait_for(lambda envelope: envelope["requestId"] == request_id and envelope["type"] in {"admin-command.accepted", "error"})

    def process_next_command(self) -> tuple[dict, dict]:
        command = self.wait_for(lambda envelope: envelope["type"] == "admin-command", timeout_seconds=5.0)
        payload = command["payload"]
        command_id = payload["commandId"]
        command_name = payload["commandName"]
        self.send_message("command-ack", {"commandId": command_id, "status": "accepted"}, correlation_id=command_id)

        duplicate = command_id in self._processed_command_ids
        if not duplicate:
            self._processed_command_ids.add(command_id)

        result_payload = {
            "commandId": command_id,
            "commandName": command_name,
            "status": "completed",
            "success": True,
            "duplicate": duplicate,
            "result": self._execute_command(command_name),
        }
        self.send_message("command-result", result_payload, correlation_id=command_id)
        return command, result_payload

    def _execute_command(self, command_name: str) -> dict:
        if command_name not in ALLOWED_COMMANDS:
            raise RuntimeError(f"Command '{command_name}' is not allowlisted.")

        if command_name == "health-check":
            return {"status": "ok", "deviceId": self.device_id, "observedAtUtc": _utc_now().isoformat()}
        if command_name == "refresh-config":
            return {"status": "refreshed", "deviceId": self.device_id, "appliedAtUtc": _utc_now().isoformat()}
        if command_name == "collect-diagnostics":
            return {
                "status": "collected",
                "deviceId": self.device_id,
                "diagnostics": {"platform": platform.platform(), "pid": os.getpid()},
            }
        if command_name == "list-status":
            return {
                "status": "ready",
                "deviceId": self.device_id,
                "role": self.role,
                "platform": platform.platform(),
                "observedAtUtc": _utc_now().isoformat(),
            }
        if command_name == "ping-time":
            return {"status": "pong", "deviceId": self.device_id, "observedAtUtc": _utc_now().isoformat()}
        raise RuntimeError(f"Command '{command_name}' is not implemented.")


def _parse_args():
    parser = argparse.ArgumentParser(description="Canonical raw TCP/TLS client for SocketPlayground.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--device-id", required=True)
    parser.add_argument("--role", choices=["admin", "client"], required=True)
    parser.add_argument("--auth-secret", default=os.getenv("SOCKET_PLAYGROUND_AUTH_SECRET"))
    parser.add_argument("--access-token")
    parser.add_argument("--allow-untrusted", action="store_true")
    parser.add_argument("--send-command")
    parser.add_argument("--target-mode", choices=["all", "devices"], default="all")
    parser.add_argument("--target-device-id", action="append", default=[])
    parser.add_argument("--timeout-ms", type=int, default=1000)
    return parser.parse_args()


def main():
    args = _parse_args()
    client = CanonicalTcpClient(
        args.host,
        args.port,
        args.device_id,
        args.role,
        auth_secret=args.auth_secret,
        access_token=args.access_token,
        allow_untrusted_tls=args.allow_untrusted,
        timeout_seconds=max(2.0, args.timeout_ms / 1000),
    )

    try:
        authenticated = client.connect()
        print(json.dumps(authenticated))

        if args.send_command:
            accepted = client.send_admin_command(
                args.send_command,
                target_mode=args.target_mode,
                target_device_ids=args.target_device_id,
                timeout_ms=args.timeout_ms,
            )
            print(json.dumps(accepted))
            command_id = accepted["payload"]["commandId"]
            while True:
                envelope = client.read_message(timeout_seconds=max(2.0, args.timeout_ms / 1000))
                print(json.dumps(envelope))
                if envelope["type"] == "command-summary" and envelope.get("correlationId") == command_id:
                    break
        else:
            print("Connected; waiting for allowlisted admin-command messages. Press Ctrl+C to exit.")
            while True:
                command, result = client.process_next_command()
                print(json.dumps(command))
                print(json.dumps(result))
    except KeyboardInterrupt:
        pass
    finally:
        client.close()


if __name__ == "__main__":
    main()
