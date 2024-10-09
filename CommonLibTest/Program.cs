
using CommonClassLibrary;
using CommonClassLibrary.Common;
using Payload.Command;
using Payload.Command.SocketClient;
using Payload.Core.Command;
using SocketIOClient;
using static CommonClassLibrary.Common.AppExitEvent;
//await DemoTest();

//var sc = new Payload.SocketIoClient(Config.SocketIOClientCommandServerHost);

//SetConsoleCtrlHandler((CtrlType signal) =>
//{
//    switch (signal)
//    {
//        case CtrlType.CTRL_BREAK_EVENT:
//        case CtrlType.CTRL_C_EVENT:
//        case CtrlType.CTRL_LOGOFF_EVENT:
//        case CtrlType.CTRL_SHUTDOWN_EVENT:
//        case CtrlType.CTRL_CLOSE_EVENT:
//            Console.WriteLine("Closing");
//            // TODO Cleanup resources
//            sc.StopClient();
//            Environment.Exit(0);
//            return false;

//        default:
//            return false;
//    }
//}, true);



while (true)
{
    CommandManager.Instance.Run();
    Thread.Sleep(Config.Instance.MainSleepInterval);
}

#region DemoTest Example
//await DemoTest();
static async Task DemoTest()
{
    //DemoCommand DemoCommand = new DemoFactory().CreateCommand();
    //DemoCommand.Execute();
    //DemoCommand.Undo();

    //CSharpExecuteCommand CSharpExecuteCommand = new CSharpExecuteCommand();
    //CSharpExecuteCommand.TargetCSharpCode = @"new DemoCommand().Execute();";
    //CSharpExecuteCommand.Execute();

    //DllExecuteCommand DllExecuteCommand = new DllExecuteCommand();
    //DllExecuteCommand.TargetDLLPath = @"C:\Users\Andrew Tsang\Documents\GitHub\SocketPlayground\DLLLibrary\bin\Debug\net8.0\DLLLibrary.dll";
    //DllExecuteCommand.TargetDllExecuteMode = DllExecuteMode.Command;
    //DllExecuteCommand.Execute();
}
#endregion

#region SocketIO Test Example
//await SocketTest();
static async Task SocketTest()
{
    var client = new SocketIOClient.SocketIO(Config.SocketIOClientCommandServerHost, new SocketIOOptions
    {
        Reconnection = true,
        Path = "/socket.io",
        EIO = SocketIO.Core.EngineIO.V4,
        AutoUpgrade = false,
    });

    client.On("my_response", async response => {
        Console.WriteLine(response);
    });

    client.On("status", async response => {
        Console.WriteLine("drop_check");
        Console.WriteLine(response);
        var o = response.GetValue<status>();
        Console.WriteLine(o.data);
        await client.EmitAsync("get_status", new { data = o.data });
    });


    client.OnConnected += async (sender, e) =>
    {
        Console.WriteLine("OnConnected");
        await client.EmitAsync("message", "socket.io");
    };

    client.OnDisconnected += async (sender, e) =>
    {
        Console.WriteLine("OnDisconnected");
    };

    await Task.Factory.StartNew(async () => { await client.ConnectAsync(); });
    Console.WriteLine($"ConnectAsync :: {client.Connected}");

    Console.ReadLine();
}
public class status
{
    public double data {  get; set; }
}
#endregion 