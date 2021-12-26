using System;
using AiSoft.Socket.Models;

namespace AiSoft.Socket.Client.Base
{
    public interface IClientReceiveBase
    {
        SocketClientBase SocketClient { get; set; }

        void ReceiveData(MessageModel msgModel);
    }

    internal class ReceiveModule
    {
        public IClientReceiveBase ReceiveBase { get; set; }

        public byte MainCommand { get; set; }
    }
}