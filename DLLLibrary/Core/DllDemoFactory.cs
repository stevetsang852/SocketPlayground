using CommonClassLibrary;
using DllLibrany;
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
    public class DllDemoFactory : Payload.Command.Interface.IFactory<DllDemoCommand>
    {
        public override TaskPack CreateTaskPack()
        {
            return new TaskPack(new DllDemoCommand(), Config.EnumTaskPackMode.ONCE);
        }

    }
}