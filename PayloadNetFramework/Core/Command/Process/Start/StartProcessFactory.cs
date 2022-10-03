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
    public class StartProcessFactory : Payload.Command.Interface.IFactory
    {
        public StartProcessFactory(StartProcessCommandProps props=null)
        {
            base.Command = new StartProcessCommand(props);
        }
        public override TaskPack CreateTaskPack()
        {
            return null;
        }

    }
}