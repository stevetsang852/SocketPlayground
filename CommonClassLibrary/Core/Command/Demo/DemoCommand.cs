using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Payload.Core.Command
{
    public class DemoCommand : Payload.Command.Interface.ICommand
    {
        public override Task Execute()
        {
            new ConsoleLogCommand().Execute<String>("RUN    DemoCommand.Execute()");

            return base.ExecuteNewTask(async () => { await MainTaskAsync(); });
        }

        public override Task Undo()
        {
            new ConsoleLogCommand().Execute<String>("RUN    DemoCommand.Undo()");

            return base.ExecuteNewTask(async () => { await MainTaskAsync(); });
        }

        private async Task MainTaskAsync()
        {
            new ConsoleLogCommand().Execute<String>("RUN    DemoCommand.MainTaskAsync()");
        }

    }
}
