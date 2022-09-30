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
        public void CreateShortcut(string targetPath, string shortcutLinkPath)
        {
            if (!shortcutLinkPath.EndsWith(".lnk"))
                shortcutLinkPath += ".lnk";

            CreateShortcutVBS(targetPath, shortcutLinkPath);
            if (!File.Exists(shortcutLinkPath))
                CreateShortcutPS(targetPath, shortcutLinkPath);
        }

        public void CreateShortcutTo(string targetPath, string shortcutTargetLinkPath)
        {
            if (!shortcutTargetLinkPath.EndsWith(".lnk"))
                shortcutTargetLinkPath += ".lnk";

            CreateShortcutVBS(targetPath, shortcutTargetLinkPath);
            if (!File.Exists(shortcutTargetLinkPath))
                CreateShortcutPS(targetPath, shortcutTargetLinkPath);
        }

        public void CreateShortcutVBS(string targetPath, string shortcutLinkPath)
        {
            if (!shortcutLinkPath.EndsWith(".lnk"))
                shortcutLinkPath += ".lnk";

            var workingDirectory = Path.GetDirectoryName(targetPath);
            var vbShortcutScript = "Set oWS = WScript.CreateObject(\"WScript.Shell\")\n" +
                $"sLinkFile = \"{shortcutLinkPath}\"\n" +
                "Set oLink = oWS.CreateShortcut(sLinkFile) \n" +
                $"oLink.TargetPath = \"{targetPath}\"\n" +
                $"oLink.WorkingDirectory = \"{workingDirectory}\"\n" +
                "oLink.Save";
            var fileName = Path.GetFileNameWithoutExtension(targetPath);
            var scriptFilePath = Path.Combine(workingDirectory, $"{fileName}.vbs");
            //var wscriptPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\wscript.exe");
            try
            {
                using (var file = File.CreateText(scriptFilePath))
                    file.Write(vbShortcutScript);
                var psi = new ProcessStartInfo
                {
                    FileName = "wscript.exe",
                    UseShellExecute = false,
                    Arguments = $"/b \"{scriptFilePath}\""
                };
                Process.Start(psi).WaitForExit();
            }
            finally
            {
                if (File.Exists(scriptFilePath))
                    File.Delete(scriptFilePath);
            }
        }

        public void CreateShortcutPS(string targetPath, string shortcutLinkPath)
        {
            if (!shortcutLinkPath.EndsWith(".lnk"))
                shortcutLinkPath += ".lnk";

            var workingDirectory = Path.GetDirectoryName(targetPath);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            psi.Arguments =
                "-windowstyle hidden  " +
                "$WshShell=New-Object -comObject WScript.Shell; " +
                $"$LinkPath = \\\"{shortcutLinkPath}\\\"; " +
                $"$Shortcut = $WshShell.CreateShortcut($LinkPath); " +
                $"$Shortcut.TargetPath = \\\"{targetPath}\\\"; " +
                $"$Shortcut.WorkingDirectory = \\\"{workingDirectory}\\\"; " +
                "$Shortcut.Save();";
            Process.Start(psi).WaitForExit();
        }
    }
}
