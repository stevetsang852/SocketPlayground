using CommonClassLibrary;
using Payload.Command;
using Payload.Command.Interface;
using Payload.Core.Command.Demo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DLLLibrary.Core.Demo
{
    public class DllDemoFactory : IFactory<DllDemoCommand>
    {
        public override TaskPack CreateTaskPack()
        {
            return new TaskPack(new DllDemoCommand(), Config.EnumTaskPackMode.ONCE);
        }

    }
}