using System;
using System.IO;

namespace CommonClassLibrary
{
    public static class BytesHelper
    {
        public static bool ByteArrayToFile(string fileName, byte[]? byteArray)
        {
            if (byteArray is null)
            {
                return false;
            }

            try
            {
                using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(byteArray, 0, byteArray.Length);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught in process: {0}", ex);
                try
                {
                    if (File.Exists(fileName))
                    {
                        File.Delete(fileName);
                    }
                }
                catch
                {
                    // Best-effort cleanup of a partial write.
                }

                return false;
            }
        }
    }
}
