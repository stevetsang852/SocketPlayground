using Payload.Command.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Payload.Command
{
    public class TaskPack
    {
        public ICommand Command { get; private set; }
        public Task Task { get; private set; }
        public bool ShouldRunning { get; private set; }
        public TaskPack(ICommand command)
        {
            Command = command;
            Task = null;
            ShouldRunning = true;
        }

        public bool SetCurrentTask(Task task)
        {
            if (Task != null)
                return false;
            Task = task;
            return true;
        }

        public void Start()
        {
            ShouldRunning = true;
            Command.Pause = false;
        }

        public void Pause()
        {
            ShouldRunning = false;
            Command.Pause = true;
        }

        public void Stop()
        {
            ShouldRunning = false;
            Command.Pause = true;
            Command.TokenSource.Cancel();
        }
    }

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

        private void Init()
        {
            TaskMap = new Dictionary<Common.Config.EnumTask, TaskPack>();
            TaskMap.Add(Common.Config.EnumTask.WallpaperEngine, new Payload.Command.WallPaper.WallpaperEngineFactory().CreateTaskPack());
        }

        public bool AddTaskPack(Common.Config.EnumTask _key, TaskPack _tp)
        {
            if (TaskMap.ContainsKey(_key))
                return false;
            TaskMap.Add(_key, _tp);
            return true;
        }

        public void SetTaskMap(Dictionary<Payload.Common.Config.EnumTask, TaskPack> _map)
        {
            TaskMap = _map;
        }

        public void Run()
        {
            foreach (var taskPack in TaskMap)
            {
                TaskPack _tp = taskPack.Value;
                if ( _tp.Task==null || 
                    (_tp.ShouldRunning && !_tp.Task.Status.Equals(TaskStatus.Running))
                    )
                {
                    Task _t = _tp.Command.Execute();
                    _tp.SetCurrentTask(_t);
                }
            }
        }
    }
}
