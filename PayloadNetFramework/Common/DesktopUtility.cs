using IWshRuntimeLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Payload.Common
{
    public class DesktopUtility
    {
        public void CreateShortcut(string targetPath)
        {
            if (!Config.Instance.WorkSpaceDir.StartsWith(Config.Instance.TargetWorkSpaceDir))
                return;
            WshShell wshShell = new WshShell();

            IWshRuntimeLibrary.IWshShortcut shortcut;

            string shortcutPath = targetPath + ".lnk";
            if(System.IO.File.Exists(shortcutPath))
                System.IO.File.Delete(shortcutPath);
            // Create the shortcut
            shortcut = (IWshRuntimeLibrary.IWshShortcut)wshShell.CreateShortcut(targetPath+ ".lnk");

            shortcut.TargetPath = Config.Instance.ExeFullName;
            shortcut.WorkingDirectory = Config.Instance.WorkSpaceDir;
            shortcut.Description = "Intel";
            // shortcut.IconLocation = Application.StartupPath + @"\App.ico";
            shortcut.Save();

        }
    }
}
