using System;
using AiSoft.Socket.Models;
using AiSoft.Socket.Server;
using AiSoft.Socket.Server.Base;
using AiSoft.Tools.Helpers;

namespace DemoServer
{
    internal class LoginModule : IServerReceiveBase
    {
        public SocketServerBase SocketServer { get; set; }

        /// <summary>
        /// 随机
        /// </summary>
        private Random _random;

        /// <summary>
        /// 初始化
        /// </summary>
        public LoginModule()
        {
            var tick = DateTime.Now.Ticks;
            _random = new Random((int)(tick & 0xffffffffL) | (int)(tick >> 32));
        }

        /// <summary>
        /// 接收数据
        /// </summary>
        /// <param name="token"></param>
        /// <param name="msgModel"></param>
        public object ReceiveData(MessageUserToken token, MessageModel msgModel)
        {
            object result = null;
            switch (msgModel.SubCommand)
            {
                case 0x02:
                    result = OnlineProcess(token, msgModel);
                    break;
            }
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="msgModel"></param>
        private string OnlineProcess(MessageUserToken token, MessageModel msgModel)
        {
            ConsoleHelper.WriteInfoLine($"数据1：主命令：{msgModel.MainCommand}，副命令：{msgModel.SubCommand}，内容：{msgModel.Content}，返回：{msgModel.Result}，错误：{msgModel.ErrorMessage}");

            var a = msgModel.GetContent<string>();
            a = _random.Next(1000000, 9999999).ToString();
            return a;
        }
    }
}