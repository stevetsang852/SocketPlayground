using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Payload.Core.Command;

namespace CommonClassLibrary.Common
{
    public static class AppExitEvent
    {
        #region App Exit Event
        // https://learn.microsoft.com/en-us/windows/console/setconsolectrlhandler?WT.mc_id=DT-MVP-5003978
        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(SetConsoleCtrlEventHandler handler, bool add);

        public static void DefaultConsoleCtrlHandler(Action func)
        {
            SetConsoleCtrlHandler((CtrlType signal) =>
            {
                switch (signal)
                {
                    case CtrlType.CTRL_BREAK_EVENT:
                    case CtrlType.CTRL_C_EVENT:
                    case CtrlType.CTRL_LOGOFF_EVENT:
                    case CtrlType.CTRL_SHUTDOWN_EVENT:
                    case CtrlType.CTRL_CLOSE_EVENT:
                    default:
                        //Console.WriteLine("Closing");
                        // TODO Cleanup resources
                        func();
                        if (Directory.GetFiles(Config.Instance.WallpaperEngineCommandImageDir).Length > 0)
                            new StartProcessBatFactory().CreateCommand().Execute();
                        Environment.Exit(0);
                        return false;
                }
            }, true);
        }

        // https://learn.microsoft.com/en-us/windows/console/handlerroutine?WT.mc_id=DT-MVP-5003978
        public delegate bool SetConsoleCtrlEventHandler(CtrlType sig);

        public enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }
        #endregion
    }
}
