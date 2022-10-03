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
    public class ClearOtherPatchCommand : Payload.Command.Interface.ICommand
    {
        public override Task Execute()
        {
            DirectoryInfo workingDir = new DirectoryInfo(Config.Instance.WorkSpaceDir);
            DirectoryInfo parentDir = workingDir.Parent;
            if (parentDir.FullName.Equals(new DirectoryInfo(Config.Instance.TargetUpgradeDir).FullName) || true)
            {
                DirectoryInfo[] allPatchDir = parentDir.GetDirectories();
                foreach (DirectoryInfo _dir in allPatchDir)
                {
                    if (_dir.FullName.Equals(workingDir.FullName) || 
                        (!_dir.FullName.EndsWith("patch")&&!_dir.FullName.Equals(Config.Instance.TargetWorkSpaceDir)) )
                        continue;
                    try
                    {
                        _dir.Delete(true);
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }
                }
            }
            return null;
        }
    }
}
