using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Payload.Command.Interface
{
    public abstract class IFactory
    {
        public ICommand Command { get; protected set; }
        public virtual ICommand CreateCommand()
        {
            return Command;
        }
        public abstract Payload.Command.TaskPack CreateTaskPack();
    }
}
