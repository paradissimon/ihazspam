using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MX
{
    public sealed class SmtpServer
    {
        public const int DefaultTcpPort = 25;

        private readonly object _lock;
        private readonly ManualResetEvent _stopEvent;
        private readonly Socket _server;
        private readonly List<SmtpSession> _activeSessions;
        private readonly MailDispatcher _mailDispatcher;

        public MailDispatcher MailDispatcher
        {
            get {
                return _mailDispatcher;
            }
        }

        public SmtpServer()
        {
            _lock = new object();
            _stopEvent = new ManualResetEvent(false);
            _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //_server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            //_server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            _activeSessions = new List<SmtpSession>();
            _mailDispatcher = new MailDispatcher();
        }

        public void Run()
        {
            _mailDispatcher.Start();

            _server.Bind(new IPEndPoint(IPAddress.Any, DefaultTcpPort));
            _server.Listen(32);

            var e = new SocketAsyncEventArgs();
            e.SetBuffer(null, 0, 0);
            e.Completed += AcceptCompleted;
            if (!_server.AcceptAsync(e))
            {
                AcceptCompleted(_server, e);
            }

            while (_stopEvent.WaitOne(5000) == false)
            {
                var abort = new List<SmtpSession>();
                lock (_lock)
                {
                    for (var i = _activeSessions.Count - 1; i >= 0; i--)
                    {
                        var session = _activeSessions[i];
                        if (session.IsAlive == false)
                        {
                            _activeSessions.RemoveAt(i);
                        }
                        else if (session.LifeTime > SmtpSession.MaximumLifeTime)
                        {
                            _activeSessions.RemoveAt(i);
                            abort.Add(session);
                        }
                    }
                }

                for (var i = 0; i < abort.Count; i++)
                {
                    abort[i].Abort();
                }
            }
        }

        public void NotifyTerminatedSession(SmtpSession s)
        {
            lock (_lock)
            {
                _activeSessions.Remove(s);
            }
        }

        private void AcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                var s = e.AcceptSocket;
                lock (_lock)
                {
                    _activeSessions.Add(new SmtpSession(this, s));
                }
            }

            e.AcceptSocket = null;
            if (!_server.AcceptAsync(e))
            {
                AcceptCompleted(_server, e);
            }
        }
    }
}
