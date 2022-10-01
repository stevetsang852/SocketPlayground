using Microsoft.Win32;
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
        MANUAL,
        RESET
    }
    public class WallpaperEngineCommand : Payload.Command.Interface.ICommand
    {
        private readonly static long _defaultInterval = Config.Instance.WallpaperEngineCommandDefaultInterval;
        private readonly static long _factoryInterval = Config.Instance.WallpaperEngineFactoryInitInterval;

        public Payload.Command.WallPaper.Mode CurrentMode { get; set; } = Payload.Command.WallPaper.Mode.NONE;
        
        public FileInfo[] imgList { get; private set; }
        public string currentPhoto;
        private string _defaultWallpaperPath;
        private RegistryKey regKey;

        public WallpaperEngineCommand() : base()
        {
            base.Interval = GetDefaultInterval();
            Init();
        }

        public WallpaperEngineCommand(TimeSpan _interval)
        {
            base.Interval = _interval;
            Init();
        }

        public WallpaperEngineCommand(long _interval)
        {
            base.Interval = TimeSpan.FromMilliseconds(_interval);
            Init();
        }

        public string GetCurrentWallpaper()
        {
            regKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", false);
            if (regKey != null)
            {
                _defaultWallpaperPath = regKey.GetValue("WallPaper").ToString();
                regKey.Close();
            }
            return _defaultWallpaperPath;
        } 

        private void Init()
        {
            try
            {
                GetCurrentWallpaper();
                CurrentMode = base.Interval.Ticks <= 0 ? Payload.Command.WallPaper.Mode.MANUAL : Payload.Command.WallPaper.Mode.AUTO;
                ReflashImgList();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public void ReflashImgList()
        {
            DirectoryInfo imgDir = new DirectoryInfo($"{Config.Instance.WorkSpaceDir}\\{Config.Instance.WallpaperEngineCommandImageDir}");
            imgList = imgDir.GetFiles();
        }

        public override Task Execute()
        {
            return base.ExecuteNewTask(async () => { await MainTaskAsync(); });
        }

        private async Task MainTaskAsync()
        {
            base.InfinityLoopInToken(async () => new Task(()=>
            {
                switch (CurrentMode)
                {
                    case Payload.Command.WallPaper.Mode.AUTO:
                        base.Interval = GetDefaultInterval(_factoryInterval);
                        SetWallpaper();
                        break;
                    case Payload.Command.WallPaper.Mode.MANUAL:
                        base.Interval = GetDefaultInterval();
                        ManualCall();
                        break;
                    case Payload.Command.WallPaper.Mode.RESET:
                        base.Interval = GetDefaultInterval(_factoryInterval);
                        ResetWallpaper();
                        break;
                }
            }).Start());
        }

        public void ManualCall()
        {
            //To-Do ...
        }

        public void ResetWallpaper()
        {
            DisplayPicture(_defaultWallpaperPath);
        }

        public void SetWallpaper()
        {
            DisplayPicture(DrawPhoto());
        }

        private string DrawPhoto()
        {
            if(imgList.Length == 0)
                return string.Empty;
            ReflashImgList();
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

        private TimeSpan GetDefaultInterval(long _l = long.MinValue)
        {
            return TimeSpan.FromMilliseconds(_l!=long.MinValue ? _l:_defaultInterval);
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
