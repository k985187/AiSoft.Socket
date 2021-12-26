using System;
using System.Threading.Tasks;
using AiSoft.Socket.Client;
using AiSoft.Tools.Helpers;

namespace DemoClient
{
    class Program
    {
        static void Main(string[] args)
        { 
            bool _isConnect;
            SocketClientBase client = null;
            Task.Run(async () =>
            {
                try
                {
                    client = new SocketClientBase("127.0.0.1", 12346);
                    client.RegisterModule<LoginModule>(0x10);
                    client.OnDisconnected += (id, e) =>
                    {
                        _isConnect = false;
                        ConsoleHelper.WriteErrorLine($"Socket关闭：{e.Message}");
                    };
                    client.OnError += (id, e) =>
                    {
                        _isConnect = false;
                        ConsoleHelper.WriteErrorLine($"Socket错误：{e.Message}");
                    };
                    client.OnReconnected += (o) =>
                    {
                        _isConnect = false;
                        ConsoleHelper.WriteInfoLine("重新连接服务器");
                    };
                    client.OnOpened += (o) =>
                    {
                        _isConnect = true;
                        ConsoleHelper.WriteSuccessLine("连接服务器成功");
                        client.Send(0x01, 0x01, "abcde");
                    };
                    client.OnReceiveData += (m) =>
                    {
                        ConsoleHelper.WriteInfoLine($"数据：主命令：{m.MainCommand}，副命令：{m.SubCommand}，内容：{m.Content}，返回：{m.Result}，错误：{m.ErrorMessage}");
                        client.Send(0x10, 0x02, "abcde");
                    };
                    client.OnDelayTime += (t) =>
                    {
                        ConsoleHelper.WriteInfoLine($"延迟：{t} 毫秒");
                    };
                    ConsoleHelper.WriteInfoLine("开始连接服务器");
                    await Task.Run(client.Connect);
                }
                catch (Exception e)
                {
                    ConsoleHelper.WriteErrorLine(e);
                    LogHelper.WriteLog(e);
                }
            });
            if (OSHelper.IsWindows())
            {
                Console.ReadKey(true);
            }
            else
            {
                Console.ReadLine();
            }
            client?.Close();
        }
    }
}