import uuid
class Client:
    sessionIdList = []
    userId = ''
    ip = ''
    def __init__(self, ip, sessionId):
        self.userId = str(uuid.uuid4())
        self.sessionIdList.append(sessionId)
        self.ip = ip

    def addSession(self, sessionId):
        self.sessionIdList.append(sessionId)

    def rmSession(self, sessionId):
        self.sessionIdList.remove(sessionId)


    def info(self):
        info = " ip: {uip} | id: {uid} | sids: {sid}".format(uip=self.ip, uid=self.userId, sid=' , '.join(self.sessionIdList))
        return info