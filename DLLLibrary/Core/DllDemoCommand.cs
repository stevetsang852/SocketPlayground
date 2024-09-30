using Payload.Core.Command;

namespace DllLibrany
{
    public class DllDemoCommand : Payload.Command.Interface.ICommand
    {
        public override Task Execute()
        {
            new ConsoleLogCommand().Execute<String>("RUN    DllDemoCommand.Execute()");

            return base.ExecuteNewTask(async () => { await MainTaskAsync(); });
        }

        public override Task Undo()
        {
            new ConsoleLogCommand().Execute<String>("RUN    DllDemoCommand.Undo()");

            return base.ExecuteNewTask(async () => { await MainTaskAsync(); });
        }

        private async Task MainTaskAsync()
        {
            new ConsoleLogCommand().Execute<String>("RUN    DllDemoCommand.MainTaskAsync()");
        }

    }
}
