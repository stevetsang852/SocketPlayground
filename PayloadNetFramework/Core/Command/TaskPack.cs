using Payload.Command.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Payload.Common.Config;

namespace Payload.Command
{
    public class TaskPack
    {
        public ICommand Command { get; private set; }
        public Task Task { get; private set; } = null;
        public bool ShouldCallRun { get; private set; } = true;
        public EnumTaskPackMode Mode { get; set; } = EnumTaskPackMode.AUTO;
        public bool Executed { get; private set; } = false;
        public TaskPack(ICommand command)
        {
            Command = command;
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
            ShouldCallRun = true;
            Command.Pause = false;
            if (Task == null || !Task.Status.Equals(TaskStatus.Running))
            {
                Task _t = Command.Execute();
                SetCurrentTask(_t);
                Executed = true;
            }
            return true;
        }

        public bool Pause()
        {
            ShouldCallRun = false;
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
}
