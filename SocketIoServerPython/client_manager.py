from client import Client
class ClientManager:
    def __init__(self):
        self.client_list = {}

    def debug(self):
        print('ClientManager debug:')
        for id in self.client_list:
            print(id,'::', self.client_list[id].info())

    def addClient(self, ip, sid):
        print('addClient')
        if ip in self.client_list:
            _client = self.client_list[ip]
            print('found c::', _client.info())
            _client.addSession(sid)
        else:
            _client = Client(ip, sid)

        self.client_list[ip] = _client
        print(self.client_list[ip].info())
        return self.client_list[ip]

    def rmSession(self, ip, sid):
        if ip in self.client_list:
            _client = self.client_list[ip]
            _client.rmSession(sid)
            if len(_client.sessionIdList) == 0:
                self.rmClientByIp(ip)

    def rmClientBySid(self, sid):
        client = self.getClientBySid(sid)
        self.rmClientByIp(client.ip)

    def rmClientByIp(self, ip):
        del self.client_list[ip]


    def getClientBySid(self, sid):
        client = [_client for _client in self.client_list.values() if sid in _client.sessionIdList]
        if len(client) == 1:
            return client[0]
        return None

    def getClientByUid(self, uid):
        client = [_client for _client in self.client_list.values() if _client.userId == uid]
        if len(client) == 1:
            return client[0]
        return None

    def getClientByIp(self, ip):
        if ip not in self.client_list:
            return None
        return self.client_list[ip]

    