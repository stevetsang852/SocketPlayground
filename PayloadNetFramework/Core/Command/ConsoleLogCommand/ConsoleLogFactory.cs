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
    public class ConsoleLogFactory : Payload.Command.Interface.IFactory
    {
        public override TaskPack CreateTaskPack()
        {
            TaskPack _tp = new TaskPack(new ConsoleLogCommand());
            _tp.Mode = Common.Config.EnumTaskPackMode.NONE; // Not Call at CommandManager
            return _tp;
        }

    }
}