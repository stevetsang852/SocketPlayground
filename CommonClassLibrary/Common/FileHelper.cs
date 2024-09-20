using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonClassLibrary
{
    public class UploadProps
    {
        public byte[]? file { get; set; }
        public string? name { get; set; }
        public string? path { get; set; }
        public string? action { get; set; }
    }
    public static class FileHelper
    {
        public static bool SaveFile(UploadProps props)
        {
            try
            {
                string savePath = string.IsNullOrEmpty(props.path) ? $"{Config.Instance.WorkSpaceDir}\\Resources\\temp\\upload" : props.path;

                if (!Directory.Exists(savePath))
                    Directory.CreateDirectory(savePath);
                string patchPath = $"{savePath}\\{props.name}";
                BytesHelper.ByteArrayToFile(patchPath, props.file);
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}
