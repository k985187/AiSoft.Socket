using System;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;

namespace AiSoft.Socket.Models
{
    internal class MessageContext : BaseContext<BaseUnpacker>
    {
        public override IUserToken UserToken { get; set; }

        public override IUnpacker Unpacker { get; set; }

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