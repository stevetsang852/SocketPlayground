using CommonClassLibrary;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Payload.Core.Command
{
    public class ClearOtherPatchCommand : Payload.Command.Interface.ICommand
    {
        public override Task Execute()
        {
            var workingDir = new DirectoryInfo(Config.Instance.WorkSpaceDir);
            var parentDir = workingDir.Parent;
            if (parentDir is null)
            {
                return null!;
            }

            var upgradeRoot = new DirectoryInfo(Config.Instance.TargetUpgradeDir).FullName;

            // P4: the previous `|| true` made this guard always succeed and could delete
            // unrelated siblings. Only run retention when we are actually under TargetUpgradeDir.
            if (!string.Equals(parentDir.FullName, upgradeRoot, StringComparison.OrdinalIgnoreCase))
            {
                return null!;
            }

            PatchRetention.Apply(
                upgradeRoot,
                protectFullPath: workingDir.FullName,
                keepCount: Config.Instance.PatchRetentionCount);

            return null!;
        }
    }
}
