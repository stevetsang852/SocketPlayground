using CommonClassLibrary;
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
    public class CSharpExecuteFactory : Payload.Command.Interface.IFactory<CSharpExecuteCommand>
    {
        public override TaskPack CreateTaskPack()
        {
            return new TaskPack(new CSharpExecuteCommand(), Config.EnumTaskPackMode.ONCE);
        }
    }
}