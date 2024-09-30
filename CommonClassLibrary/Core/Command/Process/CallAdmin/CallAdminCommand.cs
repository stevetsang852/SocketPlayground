using CommonClassLibrary;
using System.Diagnostics;

namespace Payload.Core.Command
{
    public class CallAdminCommand : Payload.Command.Interface.ICommand
    {
        public override Task Execute()
        {
            if (!Config.Instance.IsUserAdministrator())
            {
                ProcessStartInfo proc = new ProcessStartInfo();
                proc.UseShellExecute = true;
                proc.WorkingDirectory = Environment.CurrentDirectory;
                //proc.FileName = Assembly.GetEntryAssembly().CodeBase;
                proc.FileName = Config.Instance.ExeFullName;
                List<string> args = new List<string>() { "" };
                foreach (string arg in args)
                {
                    proc.Arguments += String.Format("\"{0}\" ", arg);
                }

                proc.Verb = "runas";

                try
                {
                    Process p = Process.Start(proc);
                }
                catch
                {
#if DEBUG                    
                    Console.WriteLine(proc.WorkingDirectory);
                    Console.WriteLine(proc.FileName);
                    Console.WriteLine("This application requires elevated credentials in order to operate correctly!");
#endif
                }
            }
            return null;
        }
    }
}
