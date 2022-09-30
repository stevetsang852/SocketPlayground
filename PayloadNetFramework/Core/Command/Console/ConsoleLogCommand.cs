using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Payload.Core.Command
{
    public class ConsoleLogCommand : Payload.Command.Interface.ICommand
    {
        public override Task Execute<String>(String _msg)
        {
            Console.WriteLine($"{DateTime.Now.ToLongTimeString()} :: {_msg}");

            return null;
        }
    }
}
