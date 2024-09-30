using CommonClassLibrary;
using Payload.Command;
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
        Command,
        TaskPack
    }
    public class DllExecuteCommand : Payload.Command.Interface.ICommand
    {
        public string TargetDLLPath { get; set; }
        public DllExecuteMode TargetDllExecuteMode { get; set; } = DllExecuteMode.NONE;

        private object? ExecuteDll(Type classType)
        {
            switch (TargetDllExecuteMode)
            {
                case DllExecuteMode.Factory:
                    if (classType.Name.EndsWith(TargetDllExecuteMode.ToString()))
                    {
                        ICommand cmd = ((ICommand)classType.InvokeMember("CreateCommand", BindingFlags.InvokeMethod, null, Activator.CreateInstance(classType), null));
                        cmd.Execute();
                        return cmd;
                    }
                    break;
                case DllExecuteMode.Command:
                    if (classType.Name.EndsWith(TargetDllExecuteMode.ToString()))
                    {
                        Task t = (Task)classType.InvokeMember("Execute", BindingFlags.InvokeMethod, null, Activator.CreateInstance(classType), null);
                        return t;
                    }
                    break;
                case DllExecuteMode.TaskPack:
                    if (classType.Name.EndsWith(TargetDllExecuteMode.ToString()))
                    {
                        TaskPack tp = (TaskPack)classType.InvokeMember("CreateTaskPack", BindingFlags.InvokeMethod, null, Activator.CreateInstance(classType), null);
                        return tp;
                    }
                    break;
            }
            return null;
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
                    ExecuteDll(type);
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
