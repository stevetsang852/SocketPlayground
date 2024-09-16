using Payload.Command;
using Payload.Command.Interface;
using Payload.Core.Command.Demo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Payload.Core.Command
{
    public class DemoFactory : Payload.Command.Interface.IFactory<DemoCommand>
    {
        public override TaskPack CreateTaskPack()
        {
            return new TaskPack(new DemoCommand(), Common.Config.EnumTaskPackMode.ONCE);
        }

    }
}