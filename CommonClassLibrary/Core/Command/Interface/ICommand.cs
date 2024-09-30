using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommonClassLibrary;
using static CommonClassLibrary.Config;

namespace Payload.Command.Interface
{
    public abstract class ICommand
    {
        public bool Pause = false;
        public CancellationTokenSource TokenSource { get; protected set; } = null;
        public CancellationToken Token { get; protected set; }
        protected Task CurrentTask;
        public EnumTask EnumTaskName { get; private set; }
        public TimeSpan Interval;

        protected readonly static long _defaultInterval = Config.Instance.WallpaperEngineCommandDefaultInterval;
        protected readonly static long _factoryInterval = Config.Instance.WallpaperEngineFactoryInitInterval;
        public ICommand()
        {
            this.TokenSource = new CancellationTokenSource();
            this.Token = TokenSource.Token;
            this.Interval = GetDefaultInterval();
        }
        public virtual void PreExecute(EnumTask enumTask)
        {
            EnumTaskName = enumTask;
        }
        public virtual Task Execute()
        {
            //throw new NotImplementedException("Please override this method");
            return CurrentTask;
        }
        public virtual Task Undo()
        {
            throw new NotImplementedException("Please override this method");
        }
        public virtual Task Execute<T>(T _input)
        {
            throw new NotImplementedException("Please override this method");
        }
        protected Task ExecuteNewTask(Func<Task> func)
        {
            this.CurrentTask = Task.Factory.StartNew(async () => 
            {
                try
                {
                    await func();
                }
                catch(Exception ex)
                {
#if DEBUG
                    Console.WriteLine(ex.Message);
#endif
                }
            }, this.Token);
            return this.CurrentTask;
        }

        protected void SetCommandDone(EnumTaskPackMode mode = EnumTaskPackMode.NONE)
        {
            if (!mode.Equals(EnumTaskPackMode.NONE)) { }
            //    CommandManager.Instance.GetTaskPack(EnumTaskName).Mode = mode;
            //CommandManager.Instance.CommandDone(EnumTaskName);
        }

        protected void Cancel()
        {
            TokenSource?.Cancel();
        }

        protected TimeSpan GetDefaultInterval(long _l = long.MinValue)
        {
            return TimeSpan.FromMilliseconds(_l != long.MinValue ? _l : _defaultInterval);
        }

        protected void InfinityLoopInToken(Func<Task> func)
        {
            try
            {
                while (!this.Token.IsCancellationRequested)
                {
                    if (this.Token.IsCancellationRequested)
                        this.Token.ThrowIfCancellationRequested();
                    Task.Delay(this.Interval, this.Token).Wait();
                    if (this.Pause)
                        continue;
                    func().Wait();
                }
            }
            catch
            {

            }
        }
    }
}
