using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Payload.Common
{
    public static class ZipHelper
    {
        public static void CreateZipFile(string ZipedFileFolder, string SourceFolder)
        {
            // Create and open a new ZIP file
            string zipFileName = string.Format("zipfile-{0:yyyy-MM-dd_hh-mm-ss-tt}.zip", DateTime.Now);
            string zipFilepath = Path.Combine(ZipedFileFolder, zipFileName);
            var zip = ZipFile.Open(zipFilepath, ZipArchiveMode.Create);
            string[] filesToZip = Directory.GetFiles(SourceFolder, "*.txt", SearchOption.AllDirectories);
            foreach (var file in filesToZip)
            {
                // Add the entry for each file
                zip.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Optimal);
            }
            // Dispose of the object when we are done
            zip.Dispose();
        }
    }
}
