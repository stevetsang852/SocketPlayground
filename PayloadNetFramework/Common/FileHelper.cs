using Payload.Core.Command.SocketClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Payload.Common
{
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
