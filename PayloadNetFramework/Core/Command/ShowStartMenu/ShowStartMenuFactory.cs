using Payload.Command;
using Payload.Command.Interface;
using Payload.Common;
using Payload.Core.Command.Demo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Payload.Core.Command
{
    public class ShowStartMenuFactory : Payload.Command.Interface.IFactory<ShowStartMenuCommand>
    {
        public override TaskPack CreateTaskPack()
        {
            return new TaskPack(new ShowStartMenuCommand(), precondition: new Config.EnumTask[] { Config.EnumTask.CopyItself });
        }

    }
}