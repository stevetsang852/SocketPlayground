using Payload.Command.Interface;
using Payload.Command.SocketClient;
using Payload.Core.Command;
using Payload.Core.Command.Key;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Payload.Common.Config;

namespace Payload.Command
{
    public class CommandManager
    {
        #region Singleton
        private CommandManager() 
        {
            Init();
        }
        private static readonly object locker = new object ();  
        private static CommandManager instance = null;
        public static CommandManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (locker)
                    {
                        if (instance == null)
                        {
                            instance = new CommandManager();
                        }
                    }
                }
                return instance;
            }
        }
        #endregion
        public Dictionary<Payload.Common.Config.EnumTask, TaskPack> TaskMap { get; private set; }

        private void Init() // Task Mapping 
        {
            TaskMap = new Dictionary<Common.Config.EnumTask, TaskPack>();
            TaskMap.Add(Common.Config.EnumTask.Demo, new DemorFactory().CreateTaskPack());

            TaskMap.Add(Common.Config.EnumTask.TcpClient, new SocketIOClientFactory().CreateTaskPack());
            //TaskMap.Add(Common.Config.EnumTask.KeyListener, new KeyListenerFactory().CreateTaskPack());
        }

        public bool AddTaskPack(Common.Config.EnumTask _key, TaskPack _tp)
        {
            if (TaskMap.ContainsKey(_key))
                return false;
            TaskMap.Add(_key, _tp);
            return true;
        }

        public TaskPack GetTaskPack(Common.Config.EnumTask _key)
        {
            TaskPack taskPack = null;
            TaskMap.TryGetValue(_key, out taskPack);
            return taskPack;
        }

        public void SetTaskMap(Dictionary<Payload.Common.Config.EnumTask, TaskPack> _map)
        {
            TaskMap = _map;
        }

        public void AddWallpaperCmd(WallPaper.Mode _mode)
        {
            TaskPack taskPack;
            taskPack = CommandManager.Instance.GetTaskPack(Common.Config.EnumTask.WallpaperEngine);
            if (taskPack == null)
            {
                taskPack = new Payload.Command.WallPaper.WallpaperEngineFactory().CreateTaskPack();
                CommandManager.Instance.AddTaskPack(Common.Config.EnumTask.WallpaperEngine, taskPack);
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
            taskPack.Start();
        }

        public void Run()
        {
            foreach (var taskPack in TaskMap)
            {
                TaskPack _tp = taskPack.Value;
                if (_tp.Mode.Equals(EnumTaskPackMode.ONCE) && _tp.Executed || _tp.Mode.Equals(EnumTaskPackMode.NONE))
                    continue;
                if (_tp.Task!=null)
                if (!_tp.ShouldCallRun || _tp.Task.Status.Equals(TaskStatus.Running))
                    continue;
                _tp.Start();
            }
        }
    }
}
