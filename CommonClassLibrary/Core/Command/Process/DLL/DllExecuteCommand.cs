using CommonClassLibrary;
using Payload.Command.Interface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Payload.Core.Command
{
    public enum DllExecuteMode
    {
        NONE,
        Factory,
        Command
    }
    public class DllExecuteCommand : Payload.Command.Interface.ICommand
    {
        public string TargetDLLPath { get; set; }
        public DllExecuteMode TargetDllExecuteMode { get; set; } = DllExecuteMode.NONE;

        private void _ExecuteDll(Type classType)
        {
            var c = Activator.CreateInstance(classType);
            if (classType.Name.EndsWith(DllExecuteMode.Command.ToString()) && TargetDllExecuteMode.Equals(DllExecuteMode.Command))
                classType.InvokeMember("Execute", BindingFlags.InvokeMethod, null, c, null); //type.InvokeMember("Execute", BindingFlags.InvokeMethod, null, c, new object[] { @"Hello" });
            if (classType.Name.EndsWith(DllExecuteMode.Factory.ToString()) && TargetDllExecuteMode.Equals(DllExecuteMode.Factory))
            {
                var cmd = classType.InvokeMember("CreateCommand", BindingFlags.InvokeMethod, null, c, null);
                ((ICommand)cmd).Execute();
            }
        }
        
        public override Task Execute()
        {
            if (string.IsNullOrEmpty(TargetDLLPath) || TargetDllExecuteMode.Equals(DllExecuteMode.NONE)) return null;
            try
            {
                Assembly DLL = Assembly.LoadFile(TargetDLLPath);
                foreach (Type type in DLL.GetExportedTypes())
                {
#if DEBUG
                    Console.WriteLine(type.Name);
#endif
                    _ExecuteDll(type);
                }
            }
            catch
            (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex.Message);
#endif
            }


            return null;
        }
    }
}
