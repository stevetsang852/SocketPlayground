
using CommonClassLibrary;
using Payload.Command;

if (Config.IsDebug()) Console.WriteLine("Safe Lock");


while (true)
{
    CommandManager.Instance.Run();
    Thread.Sleep(Config.Instance.MainSleepInterval);
}