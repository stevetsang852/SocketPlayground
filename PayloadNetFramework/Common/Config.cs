using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Payload.Common
{
    public class Config
    {
        #region Singleton
        private Config() 
        {
            Init();
        }
        private static Config instance = null;
        public static Config Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Config();
                }
                return instance;
            }
        }
        #endregion        

        public void Init()
        {
            WorkSpaceDir = Directory.GetCurrentDirectory();
            ExeName = System.AppDomain.CurrentDomain.FriendlyName;
        }

        #region Enum
        public enum EnumTask
        {
            Demo,
            WallpaperEngine,
            TcpClient,
            KeyListener
        }

        public enum EnumTaskPackMode
        {
            NONE,
            AUTO,
            ONCE
        }
        #endregion

        #region Global
        public string WorkSpaceDir;
        public string ExeName;
        public int MainSleepInterval = 10 * 1000;
        #endregion

        #region Class CommandManager

        #endregion

        #region Class WallpaperEngineCommand
        public long WallpaperEngineCommandDefaultInterval = 60 * 1000;
        public string WallpaperEngineCommandImageDir = @"Resources\Image\Wallpaper";

        #endregion

        #region Class WallpaperEngineFactory
        public long WallpaperEngineFactoryInitInterval = 1000 * 60;
        #endregion

        #region Class SocketIOClientCommand
        public long SocketIOClientCommandDefaultInterval = 30 * 1000;
        public string SocketIOClientCommandServerHost = "http://192.168.88.221:55699"; // "http://192.168.88.240:5000" "http://192.168.88.221:55699" "http://127.0.0.1:55699"
        #endregion
    }
}
