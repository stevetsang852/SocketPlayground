using Payload.Command.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CommonClassLibrary.Config;

namespace Payload.Command
{
    public class TaskPack
    {
        public ICommand Command { get; private set; }
        public Task Task { get; private set; } = null;
        public bool ShouldCallRun { get; private set; } = true;
        public EnumTaskPackMode Mode { get; set; }
        public bool Executed { get; private set; } = false;
        public HashSet<EnumTask> Precondition { get; private set; }
        public TaskPack(ICommand command, EnumTaskPackMode mode = EnumTaskPackMode.AUTO, EnumTask[] precondition = null)
        {
            Command = command;
            Mode = mode;
            Precondition = new HashSet<EnumTask>();
            if(precondition!=null)
                foreach(EnumTask task in precondition)
                    Precondition.Add(task);            
        }

        public void SetPrecondition(EnumTask tasks)
        {
            Precondition.Add(tasks);
        }

        public bool SetCurrentTask(Task task)
        {
            if (Task != null)
                return false;
            Task = task;
            return true;
        }

        public bool Start(EnumTask enumTask)
        {
            if (Command == null)
                return false;
            ShouldCallRun = true;
            Command.Pause = false;
            if (Task == null || !Task.Status.Equals(TaskStatus.Running))
            {
                Command.PreExecute(enumTask);
                Task _t = Command.Execute();
                SetCurrentTask(_t);
                if(Precondition.Count>0)
                    Precondition.Clear();
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
                Command?.TokenSource?.Cancel();
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
