using Payload.Common;
using Payload.Core.Command.SocketClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Payload.Command.SocketClient
{
    public class SocketIOClientCommand : Payload.Command.Interface.ICommand
    {
        SocketIoClient sic;
        SocketIOClient.SocketIO client;
        bool ReTry = false;
        bool ReTrying = false;
        private static object locker = new object();

        public SocketIOClientCommand() : base()
        {
            Init();
        }

        private void Init()
        {
            try
            {
                base.Interval = TimeSpan.FromMilliseconds(Config.Instance.SocketIOClientCommandDefaultInterval);
                sic = new SocketIoClient("http://127.0.0.1:55699"); // "http://192.168.88.240:5000"
                client = sic.client;
                client.OnConnected += Client_OnConnected;
                client.OnDisconnected += Client_OnDisconnected;
                client.OnReconnectError += Client_OnReconnectError;
                client.OnReconnectFailed += Client_OnReconnectFailed;                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private void Client_OnReconnectFailed(object sender, EventArgs e)
        {
            startReconnet();
        }

        private void Client_OnReconnectError(object sender, Exception e)
        {
            startReconnet();
        }

        private void Client_OnDisconnected(object sender, string e)
        {            
            startReconnet();
        }
        
        private void startReconnet()
        {
            CommandManager.Instance.AddWallpaperCmd(WallPaper.Mode.AUTO);
            if (ReTry || ReTrying)
                return;
            ReTry = true;
            Console.WriteLine(DateTime.Now.ToLongTimeString() + " :: START RETRY");
            Execute();
        }

        private void Client_OnConnected(object sender, EventArgs e)
        {
            try
            {
                ReTry = false;
                client.EmitAsync("message", "JACK is Online").Wait();
                CommandManager.Instance.AddWallpaperCmd(WallPaper.Mode.RESET);
            }
            catch
            {

            }
        }

        public override Task Execute()
        {
            return base.ExecuteNewTask(async () => { await MainTaskAsync(); });
        }

        private async Task MainTaskAsync()
        {            
            Console.WriteLine(DateTime.Now.ToLongTimeString() + " :: WAIT");

            while (!client.Connected || ReTry)
            {
                ReTrying = true;
                if (client.Connected)
                    break;
                if (!client.Connected)
                    await client.ConnectAsync();                                   
                await Task.Delay(base.Interval, base.Token);
                Console.WriteLine(DateTime.Now.ToLongTimeString() + " :: RETRY");
            }
            Console.WriteLine(DateTime.Now.ToLongTimeString() + " :: DONE");
            ReTrying = false;
        }

    }
}
