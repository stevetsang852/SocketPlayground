using Payload.Command.Interface;
using Payload.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Payload.Command.WallPaper
{
    public class WallpaperEngineFactory : Payload.Command.Interface.Creator
    {
        public override TaskPack CreateTaskPack()
        {
            return new TaskPack(new WallpaperEngineCommand(Config.Instance.WallpaperEngineFactoryInitInterval));
        }
    }
}
