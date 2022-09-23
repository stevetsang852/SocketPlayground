using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Payload.Common
{
    public static class TextHelper
    {
        public static string GenTimeSpanFromMillisec(Double millisec)
        //https://learn.microsoft.com/en-us/dotnet/api/system.timespan.frommilliseconds?view=net-6.0
        {
            // Create a TimeSpan object and TimeSpan string from 
            // a number of milliseconds.
            TimeSpan interval = TimeSpan.FromMilliseconds(millisec);
            string timeInterval = interval.ToString();

            // Pad the end of the TimeSpan string with spaces if it 
            // does not contain milliseconds.
            int pIndex = timeInterval.IndexOf(':');
            pIndex = timeInterval.IndexOf('.', pIndex);
            if (pIndex < 0) timeInterval += "        ";

            return String.Format("{0}", timeInterval);
        }
    }
}
