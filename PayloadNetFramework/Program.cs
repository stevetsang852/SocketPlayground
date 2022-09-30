using Microsoft.Win32;
using Payload.Common;
using Payload.Core.Command;
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
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            new ConsoleLogCommand().Execute<String>($"Task Interval : {TextHelper.GenTimeSpanFromMillisec(Config.Instance.MainSleepInterval)}");
            while (true)
            {
                Command.CommandManager.Instance.Run();
                Thread.Sleep(Config.Instance.MainSleepInterval);
            }
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
        }
    }
}
