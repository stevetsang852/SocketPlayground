using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonClassLibrary
{
    public abstract class ISocketIoClient
    {
        public SocketIOClient.SocketIO client { get; protected set; }
        public string ServerHost { get; protected set; }
        public string ServerPort { get; protected set; }
    }
}
