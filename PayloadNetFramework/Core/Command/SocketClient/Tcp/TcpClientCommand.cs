using Payload.Common;
using SocketIOClient;
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
    public class TcpClientCommand : Payload.Command.Interface.ICommand
    {
        SocketIO client;
        public TcpClientCommand() : base()
        {
            Init();
        }

        private void Init()
        {
            try
            {
                client = new SocketIO("http://192.168.88.240:5000");
                client.OnConnected += Client_OnConnected;
                client.On("message", async response =>
                {
                    // You can print the returned data first to decide what to do next.
                    // output: ["hi client"]                    
                    Console.WriteLine(response);

                    string text = response.GetValue<string>();

                    // The socket.io server code looks like this:
                    //await client.EmitAsync("message", "HI");

                    switch (text)
                    {
                        case "wp":
                            AddWallpaperCmd();
                            break;
                        case "wp stop":
                            CommandManager.Instance.GetTaskPack(Config.EnumTask.WallpaperEngine).Pause();
                            break;
                    }

                });
                client.ConnectAsync().Wait();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private void Client_OnConnected(object sender, EventArgs e)
        {
            client.EmitAsync("message", "HI").Wait();
        }

        public override Task Execute()
        {
            return base.ExecuteNewTask(async () => { await MainTaskAsync(); });
        }

        private void AddWallpaperCmd()
        {
            TaskPack taskPack;
            bool init= false;
            taskPack = CommandManager.Instance.GetTaskPack(Common.Config.EnumTask.WallpaperEngine);
            if (taskPack == null)
            {
                init = true;
                taskPack = new Payload.Command.WallPaper.WallpaperEngineFactory().CreateTaskPack();
            }
            taskPack.Start();
            if(init)
                CommandManager.Instance.AddTaskPack(Common.Config.EnumTask.WallpaperEngine, taskPack);
        }

        private async Task MainTaskAsync()
        {            
            
        }

    }
}
