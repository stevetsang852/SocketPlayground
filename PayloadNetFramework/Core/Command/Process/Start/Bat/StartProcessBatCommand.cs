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
    public class StartProcessBatCommand : Payload.Command.Interface.ICommand
    {
        public StartProcessCommandProps Props { get; set; }
        public override Task Execute()
        {
            string workingDir = Props!= null ? Props.TargetWorkSpaceDir : Config.Instance.TargetWorkSpaceDir;
            string exeName = Props!= null ? Props.ExeName : Config.Instance.ExeName;
            string batPath = workingDir + "\\run.bat";
            if (File.Exists(batPath))
                File.Delete(batPath);
            string batTxt = $"@echo off\npushd {workingDir}\nstart \"Loading ...\" \"{exeName}\"";
            using (StreamWriter writer = new StreamWriter(batPath))
            {
                writer.WriteLine(batTxt);
            }
            var p = new Process();
            p.StartInfo.FileName = batPath;
            p.Start();

            return null;
        }
    }
}
