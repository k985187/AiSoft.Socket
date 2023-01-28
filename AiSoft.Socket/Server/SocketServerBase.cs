using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiSoft.Nat;
using AiSoft.Socket.Extensions;
using AiSoft.Socket.Models;
using AiSoft.Socket.Server.Base;
using AiSoft.Socket.Server.Collection;
using AiSoft.Tools.Helpers;
using AiSoft.Tools.Security;
using SAEA.Common.Caching;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace AiSoft.Socket.Server
{
    /// <summary>
    /// 服务端基类
    /// </summary>
    public class SocketServerBase : IDisposable
    {
        /// <summary>
        /// 状态显示时间(默认5000毫秒)
        /// </summary>
        public int StateSpan { get; set; } = 5000;

        /// <summary>
        /// 是否NAT映射(默认不映射)
        /// </summary>
        public bool IsNat { get; set; } = false;

        private long _packReceiveCount;

        private long _packSendCount;

        private IServerSocket _server;

        /// <summary>
        /// 是否加密
        /// </summary>
        private bool _isEncrypt;

        /// <summary>
        /// 新建连接事件
        /// </summary>
        public event OnAcceptedHandler OnAccepted;

        /// <summary>
        /// 断开事件
        /// </summary>
        public event OnDisconnectedHandler OnDisconnected;

        /// <summary>
        /// 错误事件
        /// </summary>
        public event OnErrorHandler OnError;

        /// <summary>
        /// 接收数据事件
        /// </summary>
        public event Action<MessageUserToken, MessageModel> OnReceiveData;

        private List<ReceiveModule> _receiveModules;

        private ClientList _clientList;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="port">端口</param>
        /// <param name="bufferSize"></param>
        /// <param name="count"></param>
        /// <param name="timeOut"></param>
        /// <param name="socketType"></param>
        /// <param name="isEncrypt"></param>
        public SocketServerBase(int port = 12346, int bufferSize = 4 * 1024, int count = 10000, int timeOut = 30000, SAEASocketType socketType = SAEASocketType.Tcp, bool isEncrypt = true)
        {
            _receiveModules = new List<ReceiveModule>();
            _clientList = new ClientList();
            _isEncrypt = isEncrypt;

            if (socketType == SAEASocketType.Udp)
            {
                if (bufferSize > SocketOption.UDPMaxLength)
                {
                    bufferSize = SocketOption.UDPMaxLength;
                }
                if (count > 10000)
                {
                    count = 10000;
                }
            }
            var option = SocketOptionBuilder.Instance
                .SetSocket(socketType)
                .SetPort(port)
                .UseIocp(new MessageContext())
                .SetReadBufferSize(bufferSize)
                .SetWriteBufferSize(bufferSize)
                .SetCount(count)
                .SetTimeOut(timeOut)
                .Build();
            _server = SocketFactory.CreateServerSocket(option);
            _server.OnAccepted += Server_OnAccepted;
            _server.OnReceive += Server_OnReceive;
            _server.OnError += Server_OnError;
            _server.OnDisconnected += Server_OnDisconnected;

            // 状态线程
            StateAsync();
        }

        /// <summary>
        /// 销毁
        /// </summary>
        ~SocketServerBase()
        {
            Dispose();
        }

        /// <summary>
        /// 清理
        /// </summary>
        public void Dispose()
        {
            DeleteNat();
            _server.Dispose();
            _receiveModules.Clear();
            _clientList.Clear();
        }

        /// <summary>
        /// 添加Nat
        /// </summary>
        protected virtual void CreateNat()
        {
            if (IsNat)
            {
                NatBuilder.Create(_server.SocketOption.Port, _server.SocketOption.Port, $"AiSoft.Sock.Nat.{_server.SocketOption.Port}");
            }
        }

        /// <summary>
        /// 删除Nat
        /// </summary>
        protected virtual void DeleteNat()
        {
            if (IsNat)
            {
                NatBuilder.Delete(_server.SocketOption.Port, _server.SocketOption.Port, $"AiSoft.Sock.Nat.{_server.SocketOption.Port}");
            }
        }

        /// <summary>
        /// 客户端错误
        /// </summary>
        /// <param name="id"></param>
        /// <param name="e"></param>
        protected virtual void Server_OnError(string id, Exception e)
        {
            ConsoleHelper.WriteErrorLine($"远程：{id}，错误：{e}");
            OnError?.Invoke(id, e);
            _clientList.Del(id);
        }

        /// <summary>
        /// 新建客户端连接
        /// </summary>
        /// <param name="obj"></param>
        protected virtual void Server_OnAccepted(object obj)
        {
#if DEBUG
            if (obj is MessageUserToken userToken)
            {
                ConsoleHelper.WriteSuccessLine($"新建连接：{userToken.ID}");
            }
            else
            {
                ConsoleHelper.WriteErrorLine("非法新建连接");
            }
#endif
            OnAccepted?.Invoke(obj);
            if (obj is MessageUserToken o && _isEncrypt)
            {
                var aesKey = EncryptProvider.CreateAesKey();
                var msgModel = new MessageModel().Set(0, 0, aesKey);
                var data = _clientList.Encrypt(o.ID, msgModel.JsonPBSerialize(), _isEncrypt);
                var sp = BaseSocketProtocal.Parse(data, SocketProtocalType.Pong).ToBytes();
                Send(o, sp);
                _clientList.Set(o.ID, aesKey);
            }
        }

        /// <summary>
        /// 客户端断开连接
        /// </summary>
        /// <param name="id"></param>
        /// <param name="e"></param>
        protected virtual void Server_OnDisconnected(string id, Exception e)
        {
#if DEBUG
            ConsoleHelper.WriteSuccessLine($"远程断开连接：{id}");
#endif
            OnDisconnected?.Invoke(id, e);
            _clientList.Del(id);
        }

        /// <summary>
        /// 开始
        /// </summary>
        public void Start()
        {
            CreateNat();
            _server.Start();
            ConsoleHelper.WriteInfoLine($"开始监听：{_server.SocketOption.Port}");
        }

        /// <summary>
        /// 异步开始
        /// </summary>
        public async Task StartAsync()
        {
            await Task.Run(Start);
        }

        /// <summary>
        /// 结束
        /// </summary>
        public void Stop()
        {
            DeleteNat();
            _packReceiveCount = 0;
            _packSendCount = 0;
            _server.Stop();
            _clientList.Clear();
            ConsoleHelper.WriteInfoLine("停止监听");
        }

        /// <summary>
        /// 异步结束
        /// </summary>
        public async Task StopAsync()
        {
            await Task.Run(Stop);
        }

        /// <summary>
        /// 状态
        /// </summary>
        private void StateAsync()
        {
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    ConsoleHelper.WriteInfoLine($"当前在线；{_server.ClientCounts}，共发送数据包：{_packSendCount}，共处理数据包：{_packReceiveCount}");
                    await Task.Delay(StateSpan);
                }
            }, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// 接收数据
        /// </summary>
        /// <param name="currentSession"></param>
        /// <param name="data"></param>
        protected virtual void Server_OnReceive(ISession currentSession, byte[] data)
        {
            if (data == null)
            {
                return;
            }
            if (!(currentSession is MessageUserToken userToken))
            {
                return;
            }
            userToken.Unpacker.Unpack(data, (s) =>
            {
                if (s.Content == null)
                {
                    return;
                }
                try
                {
                    Interlocked.Increment(ref _packReceiveCount);
                    var msgModel = _clientList.Decrypt(userToken.ID, s.Content, _isEncrypt).JsonPBDeserialize<MessageModel>();
                    OnReceive(userToken, msgModel);
                }
                catch (Exception e)
                {
                    _server.Disconnect(userToken.ID);
                    ConsoleHelper.WriteErrorLine($"远程：{userToken.ID}，接收消息错误：{e}");
                    LogHelper.WriteLog(e);
                }
            }, (d) =>
            {
                var sm = new BaseSocketProtocal {BodyLength = 0, Type = (byte)SocketProtocalType.Heart};
                Send(userToken, sm.ToBytes());
            }, null);
        }

        /// <summary>
        /// 接收数据
        /// </summary>
        /// <param name="token"></param>
        /// <param name="msgModel"></param>
        protected virtual void OnReceive(MessageUserToken token, MessageModel msgModel)
        {
#if DEBUG
            ConsoleHelper.WriteWarningLine($"数据来源：{token.ID}");
            ConsoleHelper.WriteWarningLine($"数据：主命令：{msgModel.MainCommand}，副命令：{msgModel.SubCommand}，内容：{msgModel.Content}");
#endif
            var isHandle = false;
            var receiveModules = _receiveModules.Where(m => m.MainCommand == msgModel.MainCommand);
            foreach (var module in receiveModules)
            {
                var result = module.ReceiveBase.ReceiveData(token, msgModel);
                if (result != null)
                {
                    Send(token, msgModel.MainCommand, msgModel.SubCommand, result, msgModel.Result, msgModel.ErrorMessage);
                }
                isHandle = true;
            }
            if (!isHandle)
            {
                OnReceiveData?.Invoke(token, msgModel);
            }
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="mainCommand"></param>
        /// <param name="subCommand"></param>
        /// <param name="content"></param>
        /// <param name="result"></param>
        /// <param name="errorMessage"></param>
        public void Send<T>(IUserToken userToken, byte mainCommand, byte subCommand, T content, bool result = true, string errorMessage = "")
        {
            var msgModel = new MessageModel().Set(mainCommand, subCommand, content, result, errorMessage);
            Send(userToken, msgModel);
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="msgModel"></param>
        public void Send(IUserToken userToken, MessageModel msgModel)
        {
            var sp = GetPbData(userToken, msgModel);
            Send(userToken, sp);
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        public void Send(IUserToken userToken, byte[] data)
        {
            Interlocked.Increment(ref _packSendCount);
            _server.SendAsync(userToken.ID, data);
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="mainCommand"></param>
        /// <param name="subCommand"></param>
        /// <param name="content"></param>
        /// <param name="result"></param>
        /// <param name="errorMessage"></param>
        public void SendAll<T>(byte mainCommand, byte subCommand, T content, bool result = true, string errorMessage = "")
        {
            var msgModel = new MessageModel().Set(mainCommand, subCommand, content, result, errorMessage);
            SendAll(msgModel);
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="msgModel"></param>
        public void SendAll(MessageModel msgModel)
        {
            var allClient = GetAllClient();
            Parallel.ForEach(allClient, t =>
            {
                var sp = GetPbData(t, msgModel);
                Send(t, sp);
            });
        }

        /// <summary>
        /// 返回所有客户端
        /// </summary>
        /// <returns></returns>
        public List<IUserToken> GetAllClient()
        {
            var tokenIdList = _clientList.ToList();
            var userTokenList = new List<IUserToken>();
            Parallel.ForEach(tokenIdList, t => { userTokenList.Add(GetUserToken(t)); });
            return userTokenList;
        }

        /// <summary>
        /// 获取用户Token
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public MessageUserToken GetUserToken(string id)
        {
            return (MessageUserToken)_server.GetCurrentObj(id);
        }

        /// <summary>
        /// PB格式化
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="msgModel"></param>
        /// <returns></returns>
        private byte[] GetPbData(IUserToken userToken, MessageModel msgModel)
        {
            var data = _clientList.Encrypt(userToken.ID, msgModel.JsonPBSerialize(), _isEncrypt);
            var sp = BaseSocketProtocal.Parse(data, SocketProtocalType.ChatMessage).ToBytes();
            return sp;
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="id"></param>
        public void Disconnect(string id)
        {
            _server.Disconnect(id);
        }

        /// <summary>
        /// 注册模块
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mainCommand"></param>
        public void RegisterModule<T>(byte mainCommand) where T : IServerReceiveBase, new()
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
        public void RegisterModule<T>(T receiveBase, byte mainCommand) where T : IServerReceiveBase
        {
            receiveBase.SocketServer = this;
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