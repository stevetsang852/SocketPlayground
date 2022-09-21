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
            new Payload.Command.WallPaper.WallpaperEngineCommand(1000).Execute(); // TEST 1 secord to change wallpaper
            while (true)
            {
                Thread.Sleep(1000 * 60);
                // To-Do ...
            }
        }
    }
}
