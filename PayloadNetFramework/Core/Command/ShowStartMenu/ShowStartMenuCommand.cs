using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Payload.Core.Command.Demo
{
    public class ShowStartMenuCommand : Payload.Command.Interface.ICommand
    {
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        public override Task Execute()
        {
            ShowStartMenu();
            return null;
        }
        private static void ShowStartMenu()
        {
            // key down event:
            const byte keyControl = 0x11;
            const byte keyEscape = 0x1B;
            keybd_event(keyControl, 0, 0, UIntPtr.Zero);
            keybd_event(keyEscape, 0, 0, UIntPtr.Zero);

            // key up event:
            const uint KEYEVENTF_KEYUP = 0x02;
            keybd_event(keyControl, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(keyEscape, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}
