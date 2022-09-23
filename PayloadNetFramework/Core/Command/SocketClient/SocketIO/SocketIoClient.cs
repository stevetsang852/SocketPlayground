using Payload.Command;
using Payload.Common;
using SocketIOClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Payload.Core.Command.SocketClient
{
    public class WallpaperProps
    {
        public string data { get; set; }
    }
    public class SocketIoClient
    {
        public SocketIO client { get; private set; }
        public string ServerHost { get; private set; }
        public SocketIoClient(string serverHost)
        {
            ServerHost = serverHost;
            client = new SocketIO(serverHost);
            Init();
        }

        private void Init()
        {
            client.On("wallpaper", async response =>
            { 
                Console.WriteLine(response);
                //string text = response.GetValue<string>();
                var _d = response.GetValue<WallpaperProps>();   
                string msg = _d.data;

                switch (msg)
                {
                    case "wp":
                        CommandManager.Instance.AddWallpaperCmd(Payload.Command.WallPaper.Mode.AUTO);
                        break;
                    case "stwp":
                        CommandManager.Instance.GetTaskPack(Config.EnumTask.WallpaperEngine).Pause();
                        break;
                    case "rewp":
                        CommandManager.Instance.AddWallpaperCmd(Payload.Command.WallPaper.Mode.RESET);
                        break;
                }
            });
        }
    }
}
