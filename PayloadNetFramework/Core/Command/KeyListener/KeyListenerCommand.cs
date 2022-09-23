using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Payload.Core.Command.Key
{
    public class KeyListenerCommand : Payload.Command.Interface.ICommand
    {



        public override Task Execute()
        {
            return base.ExecuteNewTask(async () => { await MainTaskAsync(); });
        }

        private async Task MainTaskAsync()
        {
            Console.WriteLine("Press A to simulate a button click");
            while(true)
            {
                SendKeys.SendWait("{Enter}");
            }
        }

    }
}
