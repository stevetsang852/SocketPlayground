using Payload.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Payload
{
    internal class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                Command.CommandManager.Instance.Run();
                Thread.Sleep(Config.Instance.MainSleepInterval);
            }
        }
    }
}
