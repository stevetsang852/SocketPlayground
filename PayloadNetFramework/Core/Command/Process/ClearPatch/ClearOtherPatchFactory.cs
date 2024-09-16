using Payload.Command;
using Payload.Command.Interface;
using Payload.Core.Command.Demo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Payload.Common.Config;

namespace Payload.Core.Command
{
    public class ClearOtherPatchFactory : Payload.Command.Interface.IFactory<ClearOtherPatchCommand>
    {
        public override TaskPack CreateTaskPack()
        {
            return new TaskPack(new ClearOtherPatchCommand(), mode: EnumTaskPackMode.ONCE, precondition: new EnumTask[] { EnumTask.SocketIO });
        }

    }
}