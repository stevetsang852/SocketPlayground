using Payload.Common;
using Payload.Core.Command;
using System;
using System.Threading;

namespace WatchDogNetCore
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Run();
            }
            catch (Exception e)
            {
                TextHelper.WriteError(e.Message);
                TextHelper.WriteError(e.StackTrace);
            }
        }
        static void Run()
        {
            new KillOtherMeCommand().Execute();

            while (true)
            {
                Payload.Command.CommandManager.Instance.Run();
                Thread.Sleep(Config.Instance.MainSleepInterval);
            }
        }
    }
}
