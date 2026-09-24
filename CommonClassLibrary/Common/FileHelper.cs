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
        /// <summary>Optional lowercase/uppercase hex SHA-256 of <see cref="file"/>. When set, verified before apply.</summary>
        public string? sha256 { get; set; }
    }
    public static class FileHelper
    {
        public static bool SaveFile(UploadProps props)
        {
            // Missing content is not a successful save. Explicit empty byte[] is allowed (0-byte file).
            if (props.file is null)
            {
                return false;
            }

            try
            {
                string savePath = string.IsNullOrEmpty(props.path)
                    ? Path.Combine(Config.Instance.WorkSpaceDir, "Resources", "temp", "upload")
                    : props.path!;

                if (!Directory.Exists(savePath))
                    Directory.CreateDirectory(savePath);
                string patchPath = Path.Combine(savePath, props.name ?? "upload.bin");
                return BytesHelper.ByteArrayToFile(patchPath, props.file);
            }
            catch
            {
                return false;
            }
        }
    }
}
