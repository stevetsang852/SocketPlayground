using Payload.Command.Interface;
using CommonClassLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CommonClassLibrary.Config;

namespace Payload.Command.SocketClient
{
    //Deprecated
    //public class SocketIOClientFactory : Payload.Command.Interface.IFactory<SocketIOClientCommand>
    //{
    //    private ISocketIoClient? SocketIoClient { get; set; }
    //    public void SetSocketIoClient(ISocketIoClient SocketIoClient=null) 
    //    { 
    //        this.SocketIoClient = SocketIoClient; 
    //    }
    //    public override TaskPack CreateTaskPack()
    //    {
    //        var sci_cmd = new SocketIOClientCommand();
    //        sci_cmd.Init(SocketIoClient);
    //        return new TaskPack(sci_cmd, Config.EnumTaskPackMode.ONCE);
    //    }
    //}
}
