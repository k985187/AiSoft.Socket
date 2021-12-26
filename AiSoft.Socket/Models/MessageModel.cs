using System;
using ProtoBuf;
using AiSoft.Tools.Extensions;

namespace AiSoft.Socket.Models
{
    [Serializable, ProtoContract]
    public class MessageModel
    {
        /// <summary>
        /// 主命令
        /// </summary>
        [ProtoMember(1)]
        public byte MainCommand { get; set; }

        /// <summary>
        /// 副命令
        /// </summary>
        [ProtoMember(2)]
        public byte SubCommand { get; set; }

        /// <summary>
        /// 内容
        /// </summary>
        [ProtoMember(3)]
        public string Content { get; set; }

        /// <summary>
        /// 是否正确
        /// </summary>
        [ProtoMember(4)]
        public bool Result { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        [ProtoMember(5)]
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 初始化
        /// </summary>
        public MessageModel()
        {
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="mainCommand"></param>
        /// <param name="subCommand"></param>
        /// <param name="content"></param>
        /// <param name="result"></param>
        /// <param name="errorMessage"></param>
        public MessageModel Set<T>(byte mainCommand, byte subCommand, T content, bool result = true, string errorMessage = "")
        {
            MainCommand = mainCommand;
            SubCommand = subCommand;
            Content = content.JsonSerialize();
            Result = result;
            ErrorMessage = errorMessage;
            return this;
        }

        /// <summary>
        /// 提取消息内容
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetContent<T>()
        {
            if (!string.IsNullOrWhiteSpace(Content))
            {
                return Content.JsonDeserialize<T>();
            }
            return default(T);
        }
    }
}