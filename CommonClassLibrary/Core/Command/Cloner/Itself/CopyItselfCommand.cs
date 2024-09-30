using CommonClassLibrary;
using Payload.Core.Command;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Environment;
using static System.Net.Mime.MediaTypeNames;

namespace Payload.Core.Command
{
    public class CopyItselfCommand : Payload.Command.Interface.ICommand
    {
        public override Task Execute()
        {
            if (Config.Instance.CheckWorkingTarget())
            {
                string msg = "Copyitself :: CheckWorkingTarget =TRUE";
                Console.WriteLine(msg);
                return Task.CompletedTask;
            }
            Copyitself();
            
            return null;
        }

        public override Task Undo()
        {
            try
            {
                DirectoryInfo targetWorkPath = new DirectoryInfo(Config.Instance.TargetWorkSpaceDir);
                if (targetWorkPath.Exists) Directory.Delete(targetWorkPath.FullName, true);
                if (targetWorkPath.Parent.Exists) Directory.Delete(targetWorkPath.Parent.FullName, true);
            }
            catch (Exception ex) { }
            return null;
        }

        void Copy(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            

            foreach (var directory in Directory.GetDirectories(sourceDir))
                Copy(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
        }

        private void Copyitself()
        {
            try
            {
                if (!Directory.Exists(Config.Instance.TargetWorkSpaceDir))
                    Directory.CreateDirectory(Config.Instance.TargetWorkSpaceDir);
                Copy(Config.Instance.WorkSpaceDir, Config.Instance.TargetWorkSpaceDir);
            }
            catch (Exception e)
            {
                Console.WriteLine("Copyitself :: Exception");
                Console.WriteLine(e);
                TextHelper.WriteError(e.ToString());
            }
        }

    }
}
