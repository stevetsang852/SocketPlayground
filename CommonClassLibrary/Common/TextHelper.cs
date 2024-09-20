using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonClassLibrary
{
    public static class TextHelper
    {
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
        public static void WriteError(string msg)
        {
            using (StreamWriter writer = new StreamWriter(Config.Instance.WorkSpaceDir+ "\\error.txt", true))
            {
                writer.WriteLine($"{DateTime.Now.ToString()} :: {msg}");
            }
        }

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
