using Payload.Command;
using Payload.Common;
using SocketIOClient;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Payload.Core.Command.SocketClient
{
    public class Props
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
            RenameAllImage();
        }

        private void Init()
        {
            client.On("wallpaper", async response =>
            { 
                Console.WriteLine(response);
                //string text = response.GetValue<string>();
                var _d = response.GetValue<Props>();   
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
                    case "rname":
                        RenameAllImage();
                        break;
                    default:
                        Handle("wallpaper", msg);
                        break;
                }
            });
        }

        private void RenameAllImage()
        {
            FileInfo[] imgs = new DirectoryInfo(Config.Instance.WallpaperEngineCommandImageDir).GetFiles();
            foreach (FileInfo file in imgs)
            {
                if (string.IsNullOrEmpty(file.Extension))
                    continue;
                File.Move(file.FullName, $"{file.DirectoryName}\\{DateTime.Now.ToString("yyyyy_MM_dd_HH_mm_ss_fff")}");
                Thread.Sleep(1);
                File.Delete(file.FullName);
            }
        }

        private void Handle(string channel, string cmd)
        {
            if (channel.Equals("wallpaper") && cmd.StartsWith("dl"))
            {
                string url = cmd.Split(' ')[1];
                new ImageHelper().SaveImage(url, $"{Config.Instance.WallpaperEngineCommandImageDir}\\{DateTime.Now.ToString("yyyyy_MM_dd_HH_mm_ss_fff")}", ImageFormat.Png);
            }
        }
    }
}
