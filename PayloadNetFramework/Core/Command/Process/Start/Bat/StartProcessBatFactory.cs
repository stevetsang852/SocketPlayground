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
    public class StartProcessBatFactory : Payload.Command.Interface.IFactory<StartProcessBatCommand>
    {
        public StartProcessBatFactory(StartProcessCommandProps props=null)
        {
            StartProcessBatCommand _cmd = new StartProcessBatCommand();
            _cmd.Props = props;
            base.Command = _cmd;
        }

    }
}