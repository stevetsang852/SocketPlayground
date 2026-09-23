from pathlib import Path
import sys

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from client_manager import ClientManager
from server import app, socketio


def _message_payloads(client):
    payloads = []
    for message in client.get_received():
        if message["name"] != "message":
            continue

        args = message["args"]
        payloads.append(args[0] if isinstance(args, (list, tuple)) else args)

    return payloads


@pytest.fixture
def socket_clients():
    sender = socketio.test_client(app)
    receiver = socketio.test_client(app)
    try:
        yield sender, receiver
    finally:
        sender.disconnect()
        receiver.disconnect()


def test_message_broadcasts_to_all_connected_clients(socket_clients):
    sender, receiver = socket_clients

    sender.send("hello from pytest")

    assert _message_payloads(sender) == ["hello from pytest"]
    assert _message_payloads(receiver) == ["hello from pytest"]


def test_client_manager_tracks_and_removes_sessions():
    manager = ClientManager()

    first_client = manager.addClient("127.0.0.1", "sid-1")
    same_ip_client = manager.addClient("127.0.0.1", "sid-2")

    assert first_client is same_ip_client
    assert manager.getClientBySid("sid-1") is first_client
    assert manager.getClientBySid("sid-2") is first_client

    manager.rmSession("127.0.0.1", "sid-1")
    assert manager.getClientBySid("sid-1") is None
    assert manager.getClientBySid("sid-2") is first_client

    manager.rmSession("127.0.0.1", "sid-2")
    assert manager.getClientByIp("127.0.0.1") is None
