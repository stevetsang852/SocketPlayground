using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonClassLibrary
{
    public class DesktopUtility
    {
        public void CreateShortcut(string targetPath)
        {
            if (!Config.Instance.WorkSpaceDir.StartsWith(Config.Instance.TargetWorkSpaceDir))
                return;
            if (!OperatingSystem.IsWindows())
                return;

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: false);
            if (shellType == null)
                throw new PlatformNotSupportedException("WScript.Shell is not available on this platform.");
            dynamic wshShell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Unable to create WScript.Shell.");

            string shortcutPath = targetPath + ".lnk";
            if(System.IO.File.Exists(shortcutPath))
                System.IO.File.Delete(shortcutPath);
            // Create the shortcut
            dynamic shortcut = wshShell.CreateShortcut(targetPath + ".lnk");

            shortcut.TargetPath = Config.Instance.ExeFullName;
            shortcut.WorkingDirectory = Config.Instance.WorkSpaceDir;
            shortcut.Description = "Intel";
            // shortcut.IconLocation = Application.StartupPath + @"\App.ico";
            shortcut.Save();

        }
    }
}
