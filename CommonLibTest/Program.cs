
using CommonClassLibrary;
using Payload.Command;
using Payload.Command.SocketClient;
using Payload.Core.Command.SocketClient;
using SocketIOClient;

//var sc_f = new SocketIOClientFactory();
//sc_f.SetSocketIoClient(new SocketIoClient(Config.Instance.SocketIOClientCommandServerHost));
//CommandManager.Instance.AddTaskPack(Config.EnumTask.SocketIO, sc_f.CreateTaskPack());

//while (true)
//{
//    CommandManager.Instance.Run();
//    Thread.Sleep(Config.Instance.MainSleepInterval);
//}


var client = new SocketIOClient.SocketIO(Config.Instance.SocketIOClientCommandServerHost, new SocketIOOptions
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
    await client.EmitAsync("get_status", new { data=o.data });
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

public class status
{
    public double data {  get; set; }
}