using Payload.Command;
using Payload.Command.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Payload.Core.Command.Key
{
    public class KeyListenerFactory : Payload.Command.Interface.IFactory
    {

        public override TaskPack CreateTaskPack()
        {
            return new TaskPack(new KeyListenerCommand());
        }

    }
}