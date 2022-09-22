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
    public class SocketIOClientCommand : Payload.Command.Interface.ICommand
    {
        SocketIO client;
        public SocketIOClientCommand() : base()
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
                            AddWallpaperCmd(WallPaper.Mode.AUTO);
                            break;
                        case "stwp":
                            CommandManager.Instance.GetTaskPack(Config.EnumTask.WallpaperEngine).Pause();
                            break;
                        case "rewp":
                            AddWallpaperCmd(WallPaper.Mode.RESET);
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
            client.EmitAsync("message", "HI Jack").Wait();
        }

        public override Task Execute()
        {
            return base.ExecuteNewTask(async () => { await MainTaskAsync(); });
        }

        private void AddWallpaperCmd(WallPaper.Mode _mode)
        {
            TaskPack taskPack;
            bool init= false;
            taskPack = CommandManager.Instance.GetTaskPack(Common.Config.EnumTask.WallpaperEngine);
            if (taskPack == null)
            {
                init = true;
                taskPack = new Payload.Command.WallPaper.WallpaperEngineFactory().CreateTaskPack();
            }
            WallPaper.WallpaperEngineCommand weCmd = ((WallPaper.WallpaperEngineCommand)taskPack.Command);
            weCmd.CurrentMode = _mode;
            switch (_mode)
            {
                case WallPaper.Mode.AUTO:
                    weCmd.SetWallpaper();
                    break;
                case WallPaper.Mode.RESET:
                    weCmd.ResetWallpaper();
                    break;
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
