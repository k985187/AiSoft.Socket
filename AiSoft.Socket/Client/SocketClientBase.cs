using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AiSoft.Socket.Client.Base;
using AiSoft.Socket.Extensions;
using AiSoft.Socket.Models;
using AiSoft.Tools.Extensions;
using AiSoft.Tools.Security.Internal;
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Common.Threading;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Model;

namespace AiSoft.Socket.Client
{
    /// <summary>
    /// 客户端基类
    /// </summary>
    public class SocketClientBase
    {
        /// <summary>
        /// 是否发送心跳(默认发送)
        /// </summary>
        public bool IsHeart { get; set; } = true;

        /// <summary>
        /// 心跳包时间(默认5000毫秒)
        /// </summary>
        public int HeartSpan { get; set; } = 5000;

        private DateTime _heartSendTime = DateTime.Now;

        private IClientSocket _client;

        private ISocketOption _option;

        private MessageContext _messageContext;

        /// <summary>
        /// 加密密匙
        /// </summary>
        private AESKey _encryptKey;

        /// <summary>
        /// 是否加密
        /// </summary>
        private bool _isEncrypt;

        /// <summary>
        /// 断开事件
        /// </summary>
        public event OnDisconnectedHandler OnDisconnected;

        /// <summary>
        /// 错误事件
        /// </summary>
        public event OnErrorHandler OnError;

        /// <summary>
        /// 连接成功事件
        /// </summary>
        public event OnAcceptedHandler OnOpened;

        /// <summary>
        /// 重连事件
        /// </summary>
        public event OnAcceptedHandler OnReconnected;

        /// <summary>
        /// 接收数据事件
        /// </summary>
        public event Action<MessageModel> OnReceiveData;

        /// <summary>
        /// 延迟时间事件
        /// </summary>
        public event Action<double> OnDelayTime;

        /// <summary>
        /// 当前重连次数
        /// </summary>
        private int _reconnect;

        /// <summary>
        /// 最大重连次数(默认Int最大值，为0不重连)
        /// </summary>
        public int MaxReconnect { get; set; } = int.MaxValue;

        /// <summary>
        /// 每次重连等待时间(默认1000毫秒)
        /// </summary>
        public int ReconnectDelay { get; set; } = 1000;

        private List<ReceiveModule> _receiveModules;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="bufferSize"></param>
        /// <param name="timeOut"></param>
        /// <param name="socketType"></param>
        /// <param name="isEncrypt"></param>
        public SocketClientBase(string ip = "127.0.0.1", int port = 12346, int bufferSize = 4 * 1024, int timeOut = 30000, SAEASocketType socketType = SAEASocketType.Tcp, bool isEncrypt = true)
        {
            _receiveModules = new List<ReceiveModule>();
            _encryptKey = new AESKey();
            _isEncrypt = isEncrypt;

            if (!ip.MatchInetAddress())
            {
                ip = ip.GetDnsIpAddress();
            }
            if (socketType == SAEASocketType.Udp && bufferSize > SocketOption.UDPMaxLength)
            {
                bufferSize = SocketOption.UDPMaxLength;
            }
            _messageContext = new MessageContext();
            _option = SocketOptionBuilder.Instance
                .SetSocket(socketType)
                .SetIP(ip)
                .SetPort(port)
                .UseIocp(_messageContext)
                .SetReadBufferSize(bufferSize)
                .SetWriteBufferSize(bufferSize)
                .SetTimeOut(timeOut)
                .Build();
            InitClientSocket();

            // 心跳包线程
            HeartAsync();
        }

        /// <summary>
        /// 初始化Socket客户端
        /// </summary>
        private void InitClientSocket()
        {
            _client = SocketFactory.CreateClientSocket(_option);
            _client.OnReceive += Client_OnReceive;
            _client.OnError += Client_OnError;
            _client.OnDisconnected += Client_OnDisconnected;
        }

        /// <summary>
        /// 错误
        /// </summary>
        /// <param name="id"></param>
        /// <param name="e"></param>
        protected virtual void Client_OnError(string id, Exception e)
        {
            _encryptKey = new AESKey();
            OnError?.Invoke(id, e);
            Task.Run(Reconnect);
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="id"></param>
        /// <param name="e"></param>
        protected virtual void Client_OnDisconnected(string id, Exception e)
        {
            _encryptKey = new AESKey();
            OnDisconnected?.Invoke(id, e);
            Task.Run(Reconnect);
        }

        /// <summary>
        /// 接收数据
        /// </summary>
        /// <param name="data"></param>
        protected virtual void Client_OnReceive(byte[] data)
        {
            if (data == null)
            {
                return;
            }
            _messageContext.Unpacker.Unpack(data, (s) =>
            {
                if (s.Content == null)
                {
                    return;
                }
                try
                {
                    var msgModel = _isEncrypt ? s.Content.DecryptTo(_encryptKey.Key, _encryptKey.IV).JsonPBDeserialize<MessageModel>() : s.Content.JsonPBDeserialize<MessageModel>();
                    if (s.Type == (byte)SocketProtocalType.Pong)
                    {
                        _encryptKey = msgModel.GetContent<AESKey>();
                        OnOpened?.Invoke(null);
                    }
                    else
                    {
                        OnReceive(msgModel);
                    }
                }
                catch (Exception e)
                {
                    //OnError?.Invoke(_messageContext.UserToken.ID, e);
                }
            }, (d) =>
            {
                OnDelayTime?.Invoke(d.Subtract(_heartSendTime).TotalMilliseconds);
            }, null);
        }

        /// <summary>
        /// 接收数据
        /// </summary>
        /// <param name="msgModel"></param>
        protected virtual void OnReceive(MessageModel msgModel)
        {
            var isHandle = false;
            var receiveModules = _receiveModules.Where(m => m.MainCommand == msgModel.MainCommand);
            foreach (var module in receiveModules)
            {
                module.ReceiveBase.ReceiveData(msgModel);
                isHandle = true;
            }
            if (!isHandle)
            {
                OnReceiveData?.Invoke(msgModel);
            }
        }

        /// <summary>
        /// 重连
        /// </summary>
        protected virtual void Reconnect()
        {
            if (Interlocked.Increment(ref _reconnect) > MaxReconnect)
            {
                _reconnect = 0;
                return;
            }
            if (MaxReconnect > 0)
            {
                Task.Delay(ReconnectDelay).ContinueWith(t =>
                {
                    OnReconnected?.Invoke(null);
                    InitClientSocket();
                    Connect();
                }).Wait();
            }
        }

        /// <summary>
        /// 心跳
        /// </summary>
        private void HeartAsync()
        {
            TaskHelper.LongRunning(async () =>
            {
                while (IsHeart)
                {
                    if (_client != null && _client.Connected)
                    {
                        if (_heartSendTime.AddMilliseconds(HeartSpan) <= DateTime.Now)
                        {
                            try
                            {
                                _heartSendTime = DateTimeHelper.Now;
                                var sm = new BaseSocketProtocal {BodyLength = 0, Type = (byte)SocketProtocalType.Heart};
                                //_client.SendAsync(sm.ToBytes());
                                Send(sm.ToBytes());
                            }
                            catch
                            {
                            }
                        }
                        await Task.Delay(HeartSpan);
                    }
                    else
                    {
                        await Task.Delay(1000);
                    }
                }
            });
        }

        /// <summary>
        /// 连接
        /// </summary>
        public void Connect()
        {
            if (!_client.Connected)
            {
                _client.ConnectAsync(e =>
                {
                    if (e != SocketError.Success)
                    {
                        Client_OnError("", new Exception($"连接服务器失败：{e}", null));
                    }
                    else
                    {
                        _reconnect = 0;
                        //OnOpened?.Invoke(null);
                    }
                });
            }
        }

        /// <summary>
        /// 关闭
        /// </summary>
        public void Close()
        {
            if (_client.Connected)
            {
                _client.Disconnect();
            }
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="mainCommand"></param>
        /// <param name="subCommand"></param>
        /// <param name="content"></param>
        /// <param name="result"></param>
        /// <param name="errorMessage"></param>
        public void Send<T>(byte mainCommand, byte subCommand, T content, bool result = true, string errorMessage = "")
        {
            var msgModel = new MessageModel().Set(mainCommand, subCommand, content, result, errorMessage);
            Send(msgModel);
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="msgModel"></param>
        public void Send(MessageModel msgModel)
        {
            var sp = GetPbData(msgModel);
            Send(sp);
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="data"></param>
        public void Send(byte[] data)
        {
            if (!_client.Connected)
            {
                return;
            }
            _client.SendAsync(data);
        }

        /// <summary>
        /// PB格式化
        /// </summary>
        /// <param name="msgModel"></param>
        /// <returns></returns>
        private byte[] GetPbData(MessageModel msgModel)
        {
            var data = _isEncrypt ? msgModel.JsonPBSerialize().EncryptTo(_encryptKey.Key, _encryptKey.IV) : msgModel.JsonPBSerialize();
            var sp = BaseSocketProtocal.Parse(data, SocketProtocalType.ChatMessage).ToBytes();
            return sp;
        }

        /// <summary>
        /// 注册模块
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mainCommand"></param>
        public void RegisterModule<T>(byte mainCommand) where T : IClientReceiveBase, new()
        {
            var receiveBase = new T();
            RegisterModule(receiveBase, mainCommand);
        }

        /// <summary>
        /// 注册模块
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="receiveBase"></param>
        /// <param name="mainCommand"></param>
        public void RegisterModule<T>(T receiveBase, byte mainCommand) where T : IClientReceiveBase
        {
            receiveBase.SocketClient = this;
            _receiveModules.Add(new ReceiveModule {ReceiveBase = receiveBase, MainCommand = mainCommand});
        }

        /// <summary>
        /// 卸载模块
        /// </summary>
        /// <param name="mainCommand"></param>
        public void UnRegisterModule(byte mainCommand)
        {
            _receiveModules.RemoveAll(p => p.MainCommand == mainCommand);
        }
    }
}