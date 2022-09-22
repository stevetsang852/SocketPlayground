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
        public abstract Task Execute();
    }
}
