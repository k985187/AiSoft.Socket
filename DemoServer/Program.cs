using System;
using System.Threading.Tasks;
using AiSoft.Socket.Server;
using AiSoft.Tools.Helpers;

namespace DemoServer
{
    class Program
    {
        static void Main(string[] args)
        {
            SocketServerBase server = null;
            Task.Run(async () =>
            {
                try
                {
                    server = new SocketServerBase(12346);
                    server.RegisterModule<LoginModule>(0x10);
                    server.OnReceiveData += (t, m) =>
                    {
                        ConsoleHelper.WriteInfoLine($"数据：主命令：{m.MainCommand}，副命令：{m.SubCommand}，内容：{m.Content}，返回：{m.Result}，错误：{m.ErrorMessage}");
                        server.Send(t, 0x01, 0x02, "abcde");
                    };
                    await server.StartAsync();
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
            server?.Stop();
        }
    }
}