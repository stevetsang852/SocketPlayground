using Payload.Core.Command;

namespace DLLLibrary.Core.Demo
{
    public class DllDemoCommand : Payload.Command.Interface.ICommand
    {
        public override Task Execute()
        {
            new ConsoleLogCommand().Execute("RUN    DllDemoCommand.Execute()");

            return ExecuteNewTask(async () => { await MainTaskAsync(); });
        }

        public override Task Undo()
        {
            new ConsoleLogCommand().Execute("RUN    DllDemoCommand.Undo()");

            return ExecuteNewTask(async () => { await MainTaskAsync(); });
        }

        private async Task MainTaskAsync()
        {
            new ConsoleLogCommand().Execute("RUN    DllDemoCommand.MainTaskAsync()");
        }

    }
}
