using System;
using AiSoft.Socket.Models;

namespace AiSoft.Socket.Server.Base
{
    public interface IServerReceiveBase
    {
        SocketServerBase SocketServer { get; set; }

        object ReceiveData(MessageUserToken token, MessageModel msgModel);
    }

    internal class ReceiveModule
    {
        public IServerReceiveBase ReceiveBase { get; set; }

        public byte MainCommand { get; set; }
    }
}