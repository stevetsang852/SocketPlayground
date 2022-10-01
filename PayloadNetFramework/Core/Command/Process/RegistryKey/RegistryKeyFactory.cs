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
    public class RegistryKeyFactory : Payload.Command.Interface.IFactory
    {
        public override TaskPack CreateTaskPack()
        {
            return null;
        }

    }
}