using Payload.Command.Interface;
using Payload.Command.SocketClient;
using Payload.Common;
using Payload.Core.Command;
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
        public HashSet<Payload.Common.Config.EnumTask> DieTask { get; private set; }
        public Dictionary<Payload.Common.Config.EnumTask, bool> FlagMap { get; private set; }

        private void Init() // Task Mapping 
        {
            FlagMap = new Dictionary<EnumTask, bool>();
            TaskMap = new Dictionary<Common.Config.EnumTask, TaskPack>();
            DieTask = new HashSet<EnumTask>();
            //RegisterTask();
        }

        public void RegisterTask()
        {
            if (TaskMap.Count > 0)
                return;
            //TaskMap.Add(Common.Config.EnumTask.Demo, new DemorFactory().CreateTaskPack());

            switch (Config.Instance.AppMode)
            {
                case EnumAppMode.DEBUG:
                    goto case EnumAppMode.NONE;
                case EnumAppMode.JACK:
                    goto case EnumAppMode.NONE;
                case EnumAppMode.NONE:
                    AddTaskPack(Common.Config.EnumTask.SocketIO, new SocketIOClientFactory().CreateTaskPack());
                    break;
            }
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
            EnumTask key = Common.Config.EnumTask.WallpaperEngine;
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
        public void CommandDone(EnumTask task)
        {
            if (FlagMap.ContainsKey(task))
                FlagMap[task] = true;
            else
                FlagMap.Add(task, true);
        }
        private bool TaskDone(HashSet<EnumTask> tasks)
        {
            foreach(var item in tasks)
            {
                bool flag = false;
                FlagMap.TryGetValue(item, out flag);
                if(!flag)
                    return false;
            }
            return true;
        }

        public void Run()
        {
            foreach (var taskPack in TaskMap)
            {
                TaskPack _tp = taskPack.Value;
                if (_tp == null)
                    continue;
                if (!_tp.ShouldCallRun || (_tp.Mode.Equals(EnumTaskPackMode.ONCE) && _tp.Executed) )
                    continue;
                if (_tp.Task!=null && _tp.Task.Status.Equals(TaskStatus.Running))
                    continue;
                if (_tp.Precondition.Count > 0 && !TaskDone(_tp.Precondition))
                    continue;

                if (_tp.Mode.Equals(EnumTaskPackMode.NONE))
                {
                    if(!DieTask.Contains(taskPack.Key))
                        DieTask.Add(taskPack.Key);
                    else
                        DieTask.Remove(taskPack.Key);
                    continue;
                }
                _tp.Start(taskPack.Key);
            }
            RemoveDieTask();
        }

        public void RemoveDieTask()
        {
            foreach (EnumTask dieTask in DieTask)
            {
                TaskMap.Remove(dieTask);
            }
        }
    }
}
