using System;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;

namespace AiSoft.Socket.Models
{
    // BaseContext<BaseUnpacker> IContext
    internal class MessageContext : IContext
    {
        public IUserToken UserToken { get; set; }

        public IUnpacker Unpacker { get; set; }

        /// <summary>
        /// 上下文
        /// </summary>
        public MessageContext()
        {
            UserToken = new MessageUserToken();
            Unpacker = new BaseUnpacker();
            UserToken.Unpacker = Unpacker;
        }
    }
}