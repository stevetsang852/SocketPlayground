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
        }

        #region Enum
        public enum EnumTask
        {
            WallpaperEngine
        }
        #endregion

        #region Global
        public string WorkSpaceDir;

        #endregion

        #region Class WallpaperEngineCommand
        public long WallpaperEngineCommandDefaultInterval = 60 * 1000;
        public string WallpaperEngineCommandImageDir = @"Resources\Image\Wallpaper";

        #endregion

        #region Class WallpaperEngineFactory
        public long WallpaperEngineFactoryInitInterval = 1000;

        #endregion
    }
}
