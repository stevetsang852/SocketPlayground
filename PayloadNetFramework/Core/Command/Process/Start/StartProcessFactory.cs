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
    public class StartProcessFactory : Payload.Command.Interface.IFactory<StartProcessCommand>
    {
        public StartProcessFactory(StartProcessCommandProps props=null)
        {
            StartProcessCommand _cmd = new StartProcessCommand();
            _cmd.Props = props;
            base.Command = _cmd;
        }

    }
}