using System;
using System.IO;

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
                string savePath = string.IsNullOrEmpty(props.path)
                    ? Path.Combine(Config.Instance.WorkSpaceDir, "Resources", "temp", "upload")
                    : props.path!;

                if (!Directory.Exists(savePath))
                    Directory.CreateDirectory(savePath);
                string patchPath = Path.Combine(savePath, props.name ?? "upload.bin");
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
