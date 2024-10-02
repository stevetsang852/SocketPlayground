using Payload.Command.Interface;
using Payload.Command.SocketClient;
using CommonClassLibrary;
using Payload.Core.Command;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CommonClassLibrary.Config;
using CommonClassLibrary.Core.Command;

namespace Payload.Command
{
    public class CommandManager : BaseCommandManager<CommandManager>
    {
        public CommandManager() { base.Init(); }
        public void AddWallpaperCmd(WallPaper.Mode _mode)
        {
            TaskPack taskPack;
            EnumTask key = Config.EnumTask.WallpaperEngine;
            taskPack = CommandManager.Instance.GetTaskPack(key);
            if (taskPack == null)
            {
                taskPack = new Payload.Command.WallPaper.WallpaperEngineFactory().CreateTaskPack();
                CommandManager.Instance.AddTaskPack(key, taskPack);
            }
            WallPaper.WallpaperEngineCommand wpeCmd = ((WallPaper.WallpaperEngineCommand)taskPack.Command);
            wpeCmd.CurrentMode = _mode;
            switch (_mode)
            {
                case WallPaper.Mode.AUTO:
                    wpeCmd.SetWallpaper();
                    break;
                case WallPaper.Mode.RESET:
                    wpeCmd.ResetWallpaper();
                    break;
            }
            taskPack.Start(key);
        }
    }
}
