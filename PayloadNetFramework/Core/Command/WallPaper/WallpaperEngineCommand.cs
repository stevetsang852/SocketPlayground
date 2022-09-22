using Payload.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Payload.Command.WallPaper
{
    public enum Mode
    {
        NONE,
        AUTO,
        MANUAL
    }
    public class WallpaperEngineCommand : Payload.Command.Interface.ICommand
    {
        private readonly static long _defaultInterval = Config.Instance.WallpaperEngineCommandDefaultInterval;

        public Payload.Command.WallPaper.Mode CurrentMode { get; set; } = Payload.Command.WallPaper.Mode.NONE;
        
        public FileInfo[] imgList { get; private set; }
        public string currentPhoto;
        public TimeSpan ChangeInterval;

        public WallpaperEngineCommand()
        {
            ChangeInterval = GetDefaultInterval();
            Init();
        }

        public WallpaperEngineCommand(TimeSpan _interval)
        {
            ChangeInterval = _interval;
            Init();
        }

        public WallpaperEngineCommand(long _interval)
        {
            ChangeInterval = TimeSpan.FromMilliseconds(_interval);
            Init();
        }

        private void Init()
        {
            try
            {
                base.TokenSource = new CancellationTokenSource();
                base.Token = TokenSource.Token;
                CurrentMode = ChangeInterval.Ticks <= 0 ? Payload.Command.WallPaper.Mode.MANUAL : Payload.Command.WallPaper.Mode.AUTO;
                DirectoryInfo imgDir = new DirectoryInfo($"{Config.Instance.WorkSpaceDir}\\{Config.Instance.WallpaperEngineCommandImageDir}");
                imgList = imgDir.GetFiles();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public override Task Execute()
        {
            return base.ExecuteNewTask(async () => { await MainTaskAsync(ChangeInterval); });
        }

        private async Task MainTaskAsync(TimeSpan interval)
        {
            while (!base.Token.IsCancellationRequested)
            {                
                if (base.Token.IsCancellationRequested)
                    base.Token.ThrowIfCancellationRequested();
                Task.Delay(interval, base.Token).Wait();
                if (base.Pause)                    
                    continue;
                switch (CurrentMode)
                {
                    case Payload.Command.WallPaper.Mode.AUTO:
                        DisplayPicture(DrawPhoto());
                        break;
                    case Payload.Command.WallPaper.Mode.MANUAL:
                        interval = GetDefaultInterval(); // Wait For Manual action ...                        
                        ManualCall();
                        break;
                }
                
                //Thread.Sleep(interval);
            }
        }

        public void ManualCall()
        {
            //To-Do ...
        }

        private string DrawPhoto()
        {
            if(imgList.Length == 0)
                return string.Empty;
            while (true)
            {
                string photo = imgList[new Random().Next(0, imgList.Length - 1)].FullName;
                if (!photo.Equals(currentPhoto))
                {
                    currentPhoto = photo;
                    break;
                }
            }
            return currentPhoto;
        }

        private TimeSpan GetDefaultInterval()
        {
            return TimeSpan.FromMilliseconds(_defaultInterval);
        }

        #region Wallpaper System

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, String pvParam, uint fWinIni);

        private const uint SPI_SETDESKWALLPAPER = 0x14;
        private const uint SPIF_UPDATEINIFILE = 0x1;
        private const uint SPIF_SENDWININICHANGE = 0x2;

        private void DisplayPicture(string _fileName)
        {
            if (string.IsNullOrEmpty(_fileName))
                return;
            uint _flags = 0;
            if (!SystemParametersInfo(SPI_SETDESKWALLPAPER,
                    0, _fileName, _flags))
            {
                Console.WriteLine("Error");
            }
        }

        #endregion

    }
}
