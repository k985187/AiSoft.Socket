using System;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;

namespace AiSoft.Socket.Models
{
    public class MessageUserToken : BaseUserToken, IUserToken
    {
        /// <summary>
        /// 登陆时间
        /// </summary>
        public DateTime Logined { get; set; }

        /// <summary>
        /// 初始化
        /// </summary>
        public MessageUserToken()
        {
        }
    }
}