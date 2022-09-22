using Payload.Command.Interface;
using Payload.Command.SocketClient;
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
        public bool ShouldCallRun { get; private set; }
        public TaskPack(ICommand command)
        {
            Command = command;
            Task = null;
            ShouldCallRun = true;
        }

        public bool SetCurrentTask(Task task)
        {
            if (Task != null)
                return false;
            Task = task;
            return true;
        }

        public bool Start()
        {
            if (Command == null)
                return false;
            ShouldCallRun = false;
            Command.Pause = false;
            Task _t = Command.Execute();
            SetCurrentTask(_t);
            return true;
        }

        public bool Pause()
        {
            ShouldCallRun = true;
            Command.Pause = true;
            return true;
        }

        public bool Stop()
        {
            ShouldCallRun = false;
            Command.Pause = true;
            try
            {
                Command.TokenSource.Cancel();
            }
            catch
            {
                return false;
            }
            Task = null;
            return true;
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
            TaskMap.Add(Common.Config.EnumTask.TcpClient, new TcpClientFactory().CreateTaskPack());
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
                if(_tp.Task!=null)
                    if (!_tp.ShouldCallRun || _tp.Task.Status.Equals(TaskStatus.Running))
                        continue;
                _tp.Start();
            }
        }
    }
}
