using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Payload.Command.Interface
{
    public abstract class IFactory<T> where T : ICommand, new()
    {
        public T Command { get; protected set; }
        public virtual T CreateCommand()
        {
            return Command !=null ? Command : new T();
        }
        public virtual Payload.Command.TaskPack CreateTaskPack()
        {
            return null;
        }
    }
}
