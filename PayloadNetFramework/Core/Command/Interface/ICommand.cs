using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Payload.Command.Interface
{
    public abstract class ICommand
    {
        public bool Pause = false;
        public CancellationTokenSource TokenSource { get; protected set; } = null;
        public CancellationToken Token { get; protected set; }
        public Task CurrentTask;

        public TimeSpan Interval;
        public ICommand()
        {
            this.TokenSource = new CancellationTokenSource();
            this.Token = TokenSource.Token;
        }
        public virtual Task Execute()
        {
            throw new NotImplementedException("Please override this method");
        }
        public virtual Task Execute<T>(T _input)
        {
            throw new NotImplementedException("Please override this method");
        }
        protected Task ExecuteNewTask(Func<Task> func)
        {
            this.CurrentTask = Task.Factory.StartNew(async () => {
                try
                {
                    await func();
                }
                catch (Exception e)
                {

                }
            }, this.Token);
            return this.CurrentTask;
        }
    }
}
