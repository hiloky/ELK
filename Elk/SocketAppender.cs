using log4net.Appender;
using log4net.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Elk
{
    /// <summary>
    /// log4net socket
	/// ps:copy form https://github.com/ugurozsahin/log4net-socket-appender
    /// </summary>
    public class SocketAppender : AppenderSkeleton
    {
        private static DateTime? _nextTrialTime = null;
        private Socket _socket;
        private IConfiguration _configuration;

        public SocketAppender()
        {
            AddressFamily = AddressFamily.InterNetwork;
            SocketType = SocketType.Stream;
            ProtocolType = ProtocolType.Tcp;

            _configuration = BuildConfig(_publicConfigName);
        }

        private string _publicConfigName = "netcore-global";
        public string PublicConfigName
        {
            get
            {
                return _publicConfigName;
            }
            set
            {
                if (!string.IsNullOrEmpty(value) && value != _publicConfigName)
                {
                    _publicConfigName = value;
                    _configuration = BuildConfig(_publicConfigName);
                }
            }
        }
        /// <summary>
        /// log发送到的远程地址
        /// </summary>
        public string RemoteAddress { get; set; }

        /// <summary>
        /// 远程服务器端口
        /// </summary>
        public int RemotePort { get; set; }

        /// <summary>
        /// 是否启用调试模式
        /// </summary>
        public bool DebugMode { get; set; }

        /// <summary>
        /// 创建socket连接时使用的addressing scheme
        /// </summary>
        public AddressFamily AddressFamily { get; set; }

        /// <summary>
        /// SocketType
        /// </summary>
        public SocketType SocketType { get; set; }

        /// <summary>
        /// ProtocolType
        /// </summary>
        public ProtocolType ProtocolType { get; set; }

        /// <summary>
        /// 建立socket连接时失败的重试次数
        /// </summary>
        public int ConAttemptsCount { get; set; } = 1;

        /// <summary>
        /// 连接重试时间隔时间
        /// </summary>
        public int ConAttemptsWaitingTimeMilliSeconds { get; set; } = 1000;

        /// <summary>
        /// 是否使用线程池处理消息发送
        /// </summary>
        public bool UseThreadPoolQueue { get; set; } = true;

        /// <summary>
        /// 多少时间内重连
        /// </summary>
        public int ReconnectTimeInSeconds { get; set; } = 10;


        public override void ActivateOptions()
        {
            if (_nextTrialTime.HasValue && _nextTrialTime.Value > DateTime.Now) return;
            else _nextTrialTime = null;

            var address = _configuration?["log4net:RemoteAddress"];
            var port = _configuration?["log4net:RemotePort"];

            if (!string.IsNullOrWhiteSpace(address))
            {
                RemoteAddress = address.Trim();
            }
            if (!string.IsNullOrWhiteSpace(port) && int.TryParse(port, out var number))
            {
                RemotePort = number;
            }

            var retryCount = 0;
            while (++retryCount <= ConAttemptsCount)
            {
                try
                {
                    _socket = new Socket(AddressFamily, SocketType, ProtocolType);
                    _socket.Connect(RemoteAddress, RemotePort);
                    break;
                }
                catch (ArgumentNullException argumentNullException)
                {
                    Console.WriteLine("ArgumentNullException : {0}", argumentNullException);
                }
                catch (SocketException socketException)
                {
                    Console.WriteLine("SocketException : {0}", socketException);
                }
                catch (Exception exception)
                {
                    Console.WriteLine("Unexpected exception : {0}", exception);
                }
                Thread.Sleep(ConAttemptsWaitingTimeMilliSeconds);
            }
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (UseThreadPoolQueue)
                ThreadPool.QueueUserWorkItem(state => AppendLog(loggingEvent));
            else
                AppendLog(loggingEvent);
        }

        private void AppendLog(LoggingEvent loggingEvent)
        {
            var level = GetLogLevel();
            if (loggingEvent.Level < level)
            {
                return;
            }
            var rendered = string.Empty;
            var address = _configuration?["log4net:RemoteAddress"];
            var port = _configuration?["log4net:RemotePort"];
            if (!string.IsNullOrWhiteSpace(address))
            {
                if (RemoteAddress != address.Trim())
                {
                    Console.WriteLine($"[RemoteAddress changed]:: {RemoteAddress} -> {address}");
                    if (_socket.Connected)
                        //断开连接，触发之后的配置更新
                        _socket.Close();
                }
            }
            if (!string.IsNullOrWhiteSpace(port) && int.TryParse(port, out var number))
            {
                if (RemotePort != number)
                {
                    Console.WriteLine($"[RemotePort changed]:: {RemotePort} -> {port}");
                    if (_socket.Connected)
                        //断开连接，触发之后的配置更新
                        _socket.Close();
                }
            }
            if (_socket.Connected)
            {
                rendered = RenderLoggingEvent(loggingEvent);

                var msg = Encoding.UTF8.GetBytes(rendered);
                try
                {
                    var bytesSent = _socket.Send(msg);
                    if (DebugMode)
                    {
                        Console.WriteLine("- Bytes sent: " + bytesSent);
                    }
                }
                catch (ArgumentNullException argumentNullException)
                {
                    Console.WriteLine("ArgumentNullException : {0}", argumentNullException);
                }
                catch (SocketException socketException)
                {
                    Console.WriteLine("SocketException : {0}", socketException);
                }
                catch (Exception exception)
                {
                    Console.WriteLine("Unexpected exception : {0}", exception);
                }
            }
            else
            {
                Console.WriteLine("[Socket Not Connect]");
                if (!_nextTrialTime.HasValue)
                {
                    _nextTrialTime = DateTime.Now.AddSeconds(ReconnectTimeInSeconds);
                }
                ActivateOptions();
            }
        }

        protected override void OnClose()
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }


        private Level GetLogLevel()
        {
            var level = _configuration?["log4net:Level"];
            if (!string.IsNullOrEmpty(level))
            {
                switch (level.ToLower())
                {
                    case "all":
                        return Level.All;
                    case "trace":
                        return Level.Trace;
                    case "debug":
                        return Level.Debug;
                    case "info":
                        return Level.Info;
                    case "warn":
                        return Level.Warn;
                    case "error":
                        return Level.Error;
                    case "fatal":
                        return Level.Fatal;
                    case "off":
                        return Level.Off;
                    default:
                        return Level.All;
                }
            }
            return Level.All;
        }

        private IConfiguration BuildConfig(string publicConfigName)
        {
            var provider = new EnvironmentVariablesConfigurationProvider();
            provider.Load();
            provider.TryGet("ASPNETCORE_ENVIRONMENT", out string environmentName);

            ConfigurationBuilder configBuilder = new ConfigurationBuilder();
            return configBuilder.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddPropFile($"{publicConfigName}.prop", true)
                .AddJsonFile("Application.json", true, true)
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{environmentName}.json", true, true)
                .Build();
        }
    }
}
