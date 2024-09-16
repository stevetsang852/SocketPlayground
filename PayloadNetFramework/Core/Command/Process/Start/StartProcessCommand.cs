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
    public class StartProcessCommandProps
    {
        public string ExeName { get; set; }
        public string TargetWorkSpaceDir { get; set; }
    }

    public class StartProcessCommand : Payload.Command.Interface.ICommand
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

            //string exe = @"C:\Project\Test\InstallUtil.exe";
            //string args = @"C:\Project\Test\ROServerService\Server\bin\Debug\myservices.exe";
            //var psi = new ProcessStartInfo();
            //psi.CreateNoWindow = true; //This hides the dos-style black window that the command prompt usually shows
            //psi.FileName = batPath;//@"cmd.exe";
            //psi.Verb = "runas"; //This is what actually runs the command as administrator
            //psi.Arguments = "/C " + exe + " " + args;
            //try
            //{
            //    var process = new Process();
            //    process.StartInfo = psi;
            //    process.Start();
            //    process.WaitForExit();
            //}
            //catch (Exception e)
            //{
            //    //If you are here the user clicked decline to grant admin privileges (or he's not administrator)
            //    throw e;
            //}


            return null;
        }
    }
}
