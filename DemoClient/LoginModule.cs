using System;
using AiSoft.Socket.Client;
using AiSoft.Socket.Client.Base;
using AiSoft.Socket.Models;
using AiSoft.Tools.Helpers;

namespace DemoClient
{
    internal class LoginModule : IClientReceiveBase
    {
        public SocketClientBase SocketClient { get; set; }

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
        /// <param name="msgModel"></param>
        public void ReceiveData(MessageModel msgModel)
        {
            switch (msgModel.SubCommand)
            {
                case 0x02:
                    OnlineProcess(msgModel);
                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msgModel"></param>
        private void OnlineProcess(MessageModel msgModel)
        {
            ConsoleHelper.WriteInfoLine($"数据1：主命令：{msgModel.MainCommand}，副命令：{msgModel.SubCommand}，内容：{msgModel.Content}，返回：{msgModel.Result}，错误：{msgModel.ErrorMessage}");

            var a = msgModel.GetContent<string>();
            a = _random.Next(1000000, 9999999).ToString();
            SocketClient.Send(msgModel.MainCommand, msgModel.SubCommand, a);
        }
    }
}