using Payload.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Payload.Command.SocketClient
{
    public class TcpClientCommand : Payload.Command.Interface.ICommand
    {
        public TcpClientCommand() : base()
        {
            Init();
        }

        private void Init()
        {
            try
            {
                
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public override Task Execute()
        {
            return base.ExecuteNewTask(async () => { await MainTaskAsync(); });
        }

        private void AddWallpaperCmd()
        {
            CommandManager.Instance.AddTaskPack(Common.Config.EnumTask.WallpaperEngine, new Payload.Command.WallPaper.WallpaperEngineFactory().CreateTaskPack());
        }

        private async Task MainTaskAsync()
        {
        }

    }
}
