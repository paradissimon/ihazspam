using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Common;

namespace MX
{
    /// <summary>
    /// SMTP implementation
    /// Spec: RFC 5321 - https://www.rfc-editor.org/info/rfc5321
    /// </summary>
    public sealed class SmtpSession
    {
        private enum SmtpState
        {
            WaitingForHelo,
            WaitingForMailFrom,
            WaitingForRcptTo,
            WaitingForAdditionalRcptTo,
            WaitingForEndOfData,
            WaitingForRset,
            Disconnect
        }

        public const int MaximumMessageSize = 2 * 1024 * 1024;

        public const byte Dot = 46;
        public const byte SemiColon = 58;
        public const byte LessThan = 60;
        public const byte GreaterThan = 62;

        public static readonly TimeSpan MaximumLifeTime = TimeSpan.FromSeconds(60);

        public static readonly byte[] Whitespace = Encoding.ASCII.GetBytes(" \r\n\t");
        public static readonly byte[] CrLf = Encoding.ASCII.GetBytes("\r\n");
        public static readonly byte[] CrLfDot = Encoding.ASCII.GetBytes("\r\n.");
        public static readonly byte[] EndOfMail = Encoding.ASCII.GetBytes(".\r\n");
        public static readonly byte[] MessageSizeExtension = Encoding.ASCII.GetBytes("SIZE=");

        public static readonly byte[] RSET = Encoding.ASCII.GetBytes("RSET");
        public static readonly byte[] HELO = Encoding.ASCII.GetBytes("HELO");
        public static readonly byte[] EHLO = Encoding.ASCII.GetBytes("EHLO");
        public static readonly byte[] MAIL = Encoding.ASCII.GetBytes("MAIL");
        public static readonly byte[] RCPT = Encoding.ASCII.GetBytes("RCPT");
        public static readonly byte[] DATA = Encoding.ASCII.GetBytes("DATA");
        public static readonly byte[] VRFY = Encoding.ASCII.GetBytes("VRFY");
        public static readonly byte[] EXPN = Encoding.ASCII.GetBytes("EXPN");
        public static readonly byte[] QUIT = Encoding.ASCII.GetBytes("QUIT");
        public static readonly byte[] NOOP = Encoding.ASCII.GetBytes("NOOP");
        public static readonly byte[] HELP = Encoding.ASCII.GetBytes("HELP");
        public static readonly byte[][] KnownCommands = { RSET, HELO, EHLO, MAIL, RCPT, DATA, VRFY, EXPN, QUIT, NOOP, HELP };

        private readonly byte[] ReplyGreeting;
        private readonly byte[] ReplyOk;
        private readonly byte[] ReplyEhlo;
        private readonly byte[] ReplyHelo;
        private readonly byte[] ReplySyntaxErrorRset;
        private readonly byte[] ReplySyntaxErrorEhlo;
        private readonly byte[] ReplySyntaxErrorHelo;
        private readonly byte[] ReplySyntaxErrorMail;
        private readonly byte[] ReplySyntaxErrorRcpt;
        private readonly byte[] ReplySyntaxErrorData;
        private readonly byte[] ReplySyntaxErrorQuit;
        private readonly byte[] ReplyCommandNotImplemented;
        private readonly byte[] ReplyUnknownCommand;
        private readonly byte[] ReplyBadSequence;
        private readonly byte[] ReplyHelp;
        private readonly byte[] ReplyStartMail;
        private readonly byte[] ReplyBye;
        private readonly byte[] ReplyNoSuchUserHere;
        private readonly byte[] ReplyMessageSizeTooBig;

        private readonly SmtpServer _server;
        private readonly object _lock;
        private readonly Socket _socket;
        private readonly string _serverName;
        private readonly DateTime _utcCreationTime;

        private SmtpState _state;
        private bool _isAborting;
        private byte[] _receiveBuffer;
        private byte[] _bufferedData;
        private int _bufferedDataLength;
        private string _sender;
        private string _lastRecipient;
        private FileStream _mailContent;
        private Guid _mailContentId;
        private int _mailContentExpectedSize;
        private bool _isAlive;
        private ArraySegment<byte> _emptyBuffer;

        public TimeSpan LifeTime
        {
            get {
                return DateTime.UtcNow.Subtract(_utcCreationTime);
            }
        }

        public bool IsAlive
        {
            get {
                lock (_lock)
                {
                    return _isAlive;
                }
            }
        }

        public SmtpSession(SmtpServer server, Socket s)
        {
            _server = server;
            _serverName = GetServerName(s);

            var ascii = Encoding.ASCII;
            ReplyGreeting = ascii.GetBytes(string.Format("220 {0} ESMTP ready.\r\n", _serverName));
            ReplyOk = ascii.GetBytes("250 OK\r\n");
            ReplyEhlo = ascii.GetBytes(string.Format("250-{0}\r\n250-8BITMIME\r\n250 SIZE {0}\r\n", _serverName, MaximumMessageSize));
            ReplyHelo = ascii.GetBytes(string.Format("250 {0}\r\n", _serverName));
            ReplySyntaxErrorRset = ascii.GetBytes("501 RSET syntax error\r\n");
            ReplySyntaxErrorEhlo = ascii.GetBytes("501 EHLO syntax error\r\n");
            ReplySyntaxErrorHelo = ascii.GetBytes("501 HELO syntax error\r\n");
            ReplySyntaxErrorMail = ascii.GetBytes("501 MAIL syntax error\r\n");
            ReplySyntaxErrorRcpt = ascii.GetBytes("501 RCPT syntax error\r\n");
            ReplySyntaxErrorData = ascii.GetBytes("501 DATA syntax error\r\n");
            ReplySyntaxErrorQuit = ascii.GetBytes("501 QUIT syntax error\r\n");
            ReplyCommandNotImplemented = ascii.GetBytes("502 Command not implemented\r\n");
            ReplyUnknownCommand = ascii.GetBytes("500 Unknown command\r\n");
            ReplyBadSequence = ascii.GetBytes("503 Bad sequence of command\r\n");
            ReplyHelp = ascii.GetBytes("211 You don't need help\r\n");
            ReplyStartMail = ascii.GetBytes("354 Start mail input; end with <CRLF>.<CRLF>\r\n");
            ReplyBye = ascii.GetBytes("221 Bye\r\n");
            ReplyNoSuchUserHere = ascii.GetBytes("550 No such user here\r\n");
            ReplyMessageSizeTooBig = ascii.GetBytes("552 Message size exceeds maximum permitted\r\n");

            _lock = new object();
            _socket = s;
            _socket.ReceiveBufferSize = 8192;
            _socket.SendBufferSize = 8192;
            _utcCreationTime = DateTime.UtcNow;

            _state = SmtpState.WaitingForHelo;
            _isAborting = false;
            _receiveBuffer = new byte[_socket.ReceiveBufferSize + 128];
            _bufferedData = new byte[_socket.ReceiveBufferSize + 128];
            _bufferedDataLength = 0;
            ResetMailInfo();
            _isAlive = true;
            _emptyBuffer = new ArraySegment<byte>(Bytes.Empty);
            Greet();
            Receive();
        }

        public void Abort()
        {
            var notify = false;

            lock (_lock)
            {
                if (!_isAborting)
                {
                    notify = true;
                    _isAlive = false;
                    _isAborting = true;
                    try { _socket.Dispose(); } catch (Exception) { }

                    ResetMailInfo();
                }
            }

            if (notify)
            {
                _server.NotifyTerminatedSession(this);
            }
        }

        private string GetServerName(Socket s)
        {
            return "[" + (s.LocalEndPoint.AddressFamily == AddressFamily.InterNetworkV6 ? "IPv6:" : "") + ((IPEndPoint)s.LocalEndPoint).Address.ToString() + "]";
        }

        private void ResetMailInfo()
        {
            _sender = null;
            _lastRecipient = null;
            if (_mailContent != null)
            {
                _mailContent.Dispose();
                _mailContent = null;
            }
            _mailContentId = Guid.Empty;
            _mailContentExpectedSize = -1;
        }

        private void Greet()
        {
            var e = CreateArgs(SendCompleted, ReplyGreeting);
            if (!_socket.SendAsync(e))
            {
                SendCompleted(_socket, e);
            }
        }

        private void Receive()
        {
            var e = CreateArgs(ReceiveCompleted, _receiveBuffer);
            if (!_socket.ReceiveAsync(e))
            {
                ReceiveCompleted(_socket, e);
            }
        }

        private void HandleCompleteLine(ArraySegment<byte> line)
        {
            var isHandled = false;
            var nextState = _state;
            byte[] response = null;

            ArraySegment<byte> cmd = _emptyBuffer;
            var tokens = new ArraySegment<byte>[] { };
            if (_state != SmtpState.WaitingForEndOfData)
            {
                tokens = Bytes.Split(line, Whitespace);
                if (tokens.Length > 0)
                {
                    Bytes.ToUpper(tokens[0]);
                    cmd = tokens[0];
                }
            }

            if (Bytes.IsSame(RSET, cmd))
            {
                isHandled = true;
                if (tokens.Length == 1)
                {
                    ResetMailInfo();
                    response = ReplyOk;
                    nextState = SmtpState.WaitingForHelo;
                }
                else
                {
                    response = ReplySyntaxErrorRset;
                }
            }
            else if (_state == SmtpState.WaitingForHelo && Bytes.IsSame(EHLO, cmd))
            {
                isHandled = true;
                if (tokens.Length == 2)
                {
                    response = ReplyEhlo;
                    nextState = SmtpState.WaitingForMailFrom;
                }
                else
                {
                    response = ReplySyntaxErrorEhlo;
                }
            }
            else if (_state == SmtpState.WaitingForHelo && Bytes.IsSame(HELO, cmd))
            {
                isHandled = true;
                if (tokens.Length == 2)
                {
                    response = ReplyHelo;
                    nextState = SmtpState.WaitingForMailFrom;
                }
                else
                {
                    response = ReplySyntaxErrorHelo;
                }
            }
            else if (_state == SmtpState.WaitingForMailFrom && Bytes.IsSame(MAIL, cmd))
            {
                isHandled = true;

                // extract mail address between < > after :
                bool go = false;
                int lt = -1, gt = -1;
                for (var i = line.Offset; i < line.Offset + line.Count; i++)
                {
                    byte ch = line.Array[i];
                    if (go)
                    {
                        if (lt == -1 && ch == LessThan) lt = i;
                        if (lt != -1 && ch == GreaterThan)
                        {
                            gt = i;
                            break;
                        }
                    }
                    else if (ch == SemiColon)
                    {
                        go = true;
                    }
                }

                if (lt != -1 && gt != -1)
                {
                    // sender found, 
                    _sender = Encoding.ASCII.GetString(line.Array, lt + 1, gt - lt - 1).Trim();

                    // check for optional SIZE=nnnnnnn extension after the sender
                    int sizeOffset = Bytes.OffsetOf(MessageSizeExtension, line.Array, gt + 1, line.Count - gt - 1);
                    if (sizeOffset != -1)
                    {
                        int valueBeginOffset = sizeOffset + MessageSizeExtension.Length;
                        int valueEndOffset = -1;
                        for (var i = valueBeginOffset; i < line.Offset + line.Count; i++)
                        {
                            byte ch = line.Array[i];
                            byte digit = Bytes.Digits[ch];
                            if (Whitespace.Contains(ch))
                            {
                                break;
                            }
                            else if (digit > 9 || digit < 0)
                            {
                                valueEndOffset = -1;
                                break;
                            }
                            valueEndOffset = i;
                        }

                        if (valueEndOffset >= valueBeginOffset)
                        {
                            int size = 0, scale = 1;
                            for (var i = valueEndOffset; i >= valueBeginOffset && size < MaximumMessageSize; i--, scale *= 10)
                            {
                                size += Bytes.Digits[line.Array[i]] * scale;
                            }

                            if (size > MaximumMessageSize)
                            {
                                response = ReplyMessageSizeTooBig;
                                nextState = SmtpState.WaitingForRset;
                            }
                            else
                            {
                                _mailContentExpectedSize = size;
                                response = ReplyOk;
                                nextState = SmtpState.WaitingForRcptTo;
                            }
                        }
                        else
                        {
                            response = ReplySyntaxErrorMail;
                        }
                    }
                    else
                    {
                        // no size extension
                        response = ReplyOk;
                        nextState = SmtpState.WaitingForRcptTo;
                    }
                }
                else
                {
                    response = ReplySyntaxErrorMail;
                }
            }
            else if ((_state == SmtpState.WaitingForRcptTo || _state == SmtpState.WaitingForAdditionalRcptTo) && Bytes.IsSame(RCPT, cmd))
            {
                // standard deviation: send only to the last given recipient even if not pretending so.
                isHandled = true;

                // extract mail address between < > after :
                bool go = false;
                int lt = -1, gt = -1;
                for (var i = line.Offset; i < line.Offset + line.Count; i++)
                {
                    byte ch = line.Array[i];
                    if (go)
                    {
                        if (lt == -1 && ch == LessThan) lt = i;
                        if (lt != -1 && ch == GreaterThan)
                        {
                            gt = i;
                            break;
                        }
                    }
                    else if (ch == SemiColon)
                    {
                        go = true;
                    }
                }

                if (lt != -1 && gt != -1)
                {
                    var recipient = Encoding.ASCII.GetString(line.Array, lt + 1, gt - lt - 1).Trim().ToLowerInvariant();
                    if (_server.MailDispatcher.IsMailboxActive(recipient))
                    {
                        _lastRecipient = recipient;
                        response = ReplyOk;
                        nextState = SmtpState.WaitingForAdditionalRcptTo;
                    }
                    else
                    {
                        response = ReplyNoSuchUserHere;
                    }
                }
                else
                {
                    response = ReplySyntaxErrorRcpt;
                }
            }
            else if (_state == SmtpState.WaitingForAdditionalRcptTo && Bytes.IsSame(DATA, cmd))
            {
                isHandled = true;
                if (tokens.Length == 1)
                {
                    response = ReplyStartMail;
                    if (_mailContent == null)
                    {
                        // presize stream to avoid garbage & extra reallocation if expected size makes sense.
                        // add a bit of slack for size errors
                        var size = (_mailContentExpectedSize > 0 && _mailContentExpectedSize <= MaximumMessageSize) ? _mailContentExpectedSize + 1024 : 4096;
                        _mailContentId = Guid.NewGuid();
                        _mailContent = new FileStream(IncomingMailDTO.GetContentFileName(_mailContentId), FileMode.CreateNew, FileAccess.Write, FileShare.None, 16384);
                    }
                    nextState = SmtpState.WaitingForEndOfData;
                }
                else
                {
                    response = ReplySyntaxErrorData;
                }
            }
            else if (_state == SmtpState.WaitingForEndOfData)
            {
                isHandled = true;
                if (line.Count == EndOfMail.Length && Bytes.StartsWith(EndOfMail, line))
                {
                    if (_mailContent.Length <= MaximumMessageSize)
                    {
                        response = ReplyOk;
                        nextState = SmtpState.WaitingForMailFrom;
                        _mailContent.Flush();                        
                        _server.MailDispatcher.Enqueue(new IncomingMailDTO(DateTime.UtcNow, _lastRecipient, _sender, (int)_mailContent.Length, _mailContentId));
                        _mailContent.Dispose();
                        _mailContent = null;
                    }
                    else
                    {
                        response = ReplyMessageSizeTooBig;
                        nextState = SmtpState.WaitingForRset;
                    }
                    ResetMailInfo();
                }
                else
                {
                    // accumulate mail body & handle dot unstuffing (while msg size doesn't exceed maximum size, since it will be discarded later anyway).
                    int offset = line.Offset;
                    int count = line.Count;
                    if (line.Array[offset] == Dot)
                    {
                        offset++;
                        count--;
                    }

                    if (_mailContent != null && _mailContent.Length + count <= MaximumMessageSize)
                    {
                        _mailContent.Write(line.Array, offset, count);
                    }
                }
            }
            else if (Bytes.IsSame(QUIT, cmd))
            {
                isHandled = true;
                if (tokens.Length == 1)
                {
                    ResetMailInfo();
                    response = ReplyBye;
                    nextState = SmtpState.Disconnect;
                }
                else
                {
                    response = ReplySyntaxErrorQuit;
                }
            }
            else if (Bytes.IsSame(VRFY, cmd) || Bytes.IsSame(EXPN, cmd))
            {
                isHandled = true;
                response = ReplyCommandNotImplemented;
            }
            else if (Bytes.IsSame(HELP, cmd))
            {
                isHandled = true;
                response = ReplyHelp;
            }
            else if (Bytes.IsSame(NOOP, cmd))
            {
                isHandled = true;
                response = ReplyOk;
            }

            if (!isHandled)
            {
                var isKnownCommand = KnownCommands.Any((knownCmd) => Bytes.IsSame(knownCmd, cmd));
                response = isKnownCommand ? ReplyBadSequence : ReplyUnknownCommand;
            }

            _state = nextState;
            if (response != null && response.Length > 0)
            {
                var e = CreateArgs(SendCompleted, response);
                if (!_socket.SendAsync(e))
                {
                    SendCompleted(_socket, e);
                }
            }
        }

        private SocketAsyncEventArgs CreateArgs(EventHandler<SocketAsyncEventArgs> asyncCompletionHandler, byte[] buffer)
        {
            var e = new SocketAsyncEventArgs();
            e.SetBuffer(buffer, 0, buffer != null ? buffer.Length : 0);
            e.Completed += asyncCompletionHandler;
            return e;
        }


        private void SendCompleted(object sender, SocketAsyncEventArgs e)
        {
            e.Completed -= SendCompleted;
            e.Dispose();
        }

        private void ReceiveCompleted(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                if (e.SocketError != SocketError.Success)
                {
                    return;
                }

                if (e.BytesTransferred == 0)
                {
                    // Remote disconnect
                    Abort();
                }
                else
                {
                    // Buffer incoming data into previously unprocessed data
                    if (_bufferedDataLength + e.BytesTransferred > _bufferedData.Length)
                    {
                        var realloc = new byte[_bufferedDataLength + e.BytesTransferred];
                        Buffer.BlockCopy(_bufferedData, 0, realloc, 0, _bufferedDataLength);
                        _bufferedData = realloc;
                    }
                    Buffer.BlockCopy(e.Buffer, e.Offset, _bufferedData, _bufferedDataLength, e.BytesTransferred);
                    _bufferedDataLength += e.BytesTransferred;

                    // Process buffered data line by line
                    var lineBegin = 0;
                    var lineEnd = -1;
                    var remaining = _bufferedDataLength;
                    while (remaining > 0 && (lineEnd = Bytes.OffsetOf(CrLf, _bufferedData, lineBegin, remaining)) > -1)
                    {
                        lineEnd += CrLf.Length;
                        var lineLength = lineEnd - lineBegin;
                        HandleCompleteLine(new ArraySegment<byte>(_bufferedData, lineBegin, lineLength));
                        lineBegin = lineEnd;
                        remaining -= lineLength;
                    }

                    // Move possibly incomplete line back to begining
                    Buffer.BlockCopy(_bufferedData, lineBegin, _bufferedData, 0, remaining);
                    _bufferedDataLength = remaining;

                    // Continue reading
                    if (_state == SmtpState.Disconnect)
                    {
                        Abort();
                    }
                    else
                    {
                        Receive();
                    }
                }
            }
            finally
            {
                e.Completed -= ReceiveCompleted;
                e.Dispose();
            }
        }
    }
}
