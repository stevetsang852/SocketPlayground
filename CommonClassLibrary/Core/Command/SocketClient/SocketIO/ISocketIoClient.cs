using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using SocketIOClient;

namespace CommonClassLibrary
{
    public abstract class ISocketIoClient
    {
        public SocketIOClient.SocketIO client { get; private set; }
        protected SocketIOClient.SocketIO BuildClient(string ServerHost="")
        {
            ServerHost = string.IsNullOrEmpty(ServerHost)? Config.SocketIOClientCommandServerHost: ServerHost;
            return new SocketIOClient.SocketIO(ServerHost, new SocketIOOptions
            {
                Reconnection = true,
                Path = "/socket.io",
                EIO = SocketIO.Core.EngineIO.V4,
                AutoUpgrade = false,
            });
        }
        public string ServerHost { get; protected set; }
        public string ServerPort { get; protected set; }
        public ISocketIoClient(string ServerHost) 
        {
            Init(ServerHost);
        }

        public ISocketIoClient(string ServerHost, string ServerPort)
        {
            Init(ServerHost, ServerPort);
        }

        private void Init(string ServerHost, string ServerPort="")
        {
            string port = string.IsNullOrEmpty(ServerPort) ? string.Empty : $":{ServerPort}";
            this.ServerHost = ServerHost;
            this.ServerPort = ServerPort;
            client = BuildClient($"{ServerHost}{port}");
            RegLifeCheckEvent();
        }

        private void RegLifeCheckEvent()
        {
            client.On("status", async response => {
                Console.WriteLine("drop_check");
                Console.WriteLine(response);
                var o = response.GetValue<Status>();
                Console.WriteLine(o.data);
                await client.EmitAsync("get_status", new { data = o.data });
            });
        }

        public async void StartClient()
        {
            if (client.Connected) return;
            await client?.ConnectAsync();
        }

        public async void StopClient()
        {
            if (!client.Connected) return;
            await client?.DisconnectAsync();
        }
    }

    public class Status
    {
        public double data { get; set; }
    }
}
