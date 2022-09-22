using Payload.Command.Interface;
using Payload.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Payload.Command.SocketClient
{
    public class TcpClientFactory : Payload.Command.Interface.IFactory
    {
        public override TaskPack CreateTaskPack()
        {
            return new TaskPack(new TcpClientCommand());
        }
    }
}
