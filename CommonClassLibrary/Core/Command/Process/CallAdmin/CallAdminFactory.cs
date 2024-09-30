using Payload.Command;
using Payload.Command.Interface;
using Payload.Core.Command.Demo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CommonClassLibrary.Config;

namespace Payload.Core.Command
{
    public class CallAdminFactory : Payload.Command.Interface.IFactory<CallAdminCommand>
    {
        public override TaskPack CreateTaskPack()
        {
            return new TaskPack(new CallAdminCommand(), mode: EnumTaskPackMode.ONCE);
        }

    }
}