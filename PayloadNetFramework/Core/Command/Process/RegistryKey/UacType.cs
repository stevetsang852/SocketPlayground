using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
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
    public static class UacConifg
    {
        public static readonly string ConsentPromptBehaviorAdmin_Name = "ConsentPromptBehaviorAdmin";
        public static readonly string EnableLUA_Name = "EnableLUA";
        public static readonly string PromptOnSecureDesktop_Name = "PromptOnSecureDesktop";
        public enum UAC_TYPE
        {
            CLOSE = 0,
            LOW,
            MEDIUM,
            HIGH
        }

        private static readonly Dictionary<UAC_TYPE, Dictionary<string, int>> UAC_TYEP_MAPPING = new Dictionary<UAC_TYPE, Dictionary<string, int>>()
        {
            { UAC_TYPE.HIGH, new Dictionary<string, int>(){ { ConsentPromptBehaviorAdmin_Name, 2 }, { EnableLUA_Name, 1 },{ PromptOnSecureDesktop_Name, 1 } } },
            { UAC_TYPE.MEDIUM, new Dictionary<string, int>(){ { ConsentPromptBehaviorAdmin_Name, 5 }, { EnableLUA_Name, 1 },{ PromptOnSecureDesktop_Name, 1 } } },
            { UAC_TYPE.LOW, new Dictionary<string, int>(){ { ConsentPromptBehaviorAdmin_Name, 5 }, { EnableLUA_Name, 1 },{ PromptOnSecureDesktop_Name, 0 } } },
            { UAC_TYPE.CLOSE, new Dictionary<string, int>(){ { ConsentPromptBehaviorAdmin_Name, 0 }, { EnableLUA_Name, 1 },{ PromptOnSecureDesktop_Name, 0 } } },
        };

        public static int GetUacConfig(UAC_TYPE uacLevel, string TagKeyName)
        {
            int value = 0;
            UAC_TYEP_MAPPING[uacLevel].TryGetValue(TagKeyName, out value);
            return value;
        }
    }
}
