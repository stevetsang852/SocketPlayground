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
    public class WallpaperEngineCommand : ICommand
    {
        private readonly static long _defaultInterval = 60;

        public EnumWallpaperEngine.Mode CurrentMode { get; set; } = EnumWallpaperEngine.Mode.NONE;

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
                CurrentMode = ChangeInterval.Ticks > 0 ? EnumWallpaperEngine.Mode.AUTO : EnumWallpaperEngine.Mode.MANUAL;
                string workSpaceDir = Directory.GetCurrentDirectory();
                DirectoryInfo imgDir = new DirectoryInfo($"{workSpaceDir}\\Resources\\Image\\Wallpaper");
                imgList = imgDir.GetFiles();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public void Execute()
        {
            Task.Factory.StartNew(async () => {
                await MainTaskAsync(ChangeInterval, new CancellationToken());
            });
        }

        private async Task MainTaskAsync(TimeSpan interval, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                switch (CurrentMode)
                {
                    case EnumWallpaperEngine.Mode.AUTO:
                        DisplayPicture(DrawPhoto());
                        break;
                    case EnumWallpaperEngine.Mode.MANUAL:
                        interval = GetDefaultInterval(); // Wait For Manual action ...
                        //To-Do ...
                        break;
                }
                Task.Delay(interval, cancellationToken).Wait();
                //Thread.Sleep(interval);
            }
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
            return new TimeSpan(_defaultInterval);
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
