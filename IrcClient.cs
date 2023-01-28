using System.Globalization;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using TShockAPI;

namespace IRCrarria
{
    public class IrcClient
    {
        private class IrcStream : IDisposable
        {
            private readonly StreamReader _streamReader;
            private readonly Stream _stream;
            private readonly object _writeLock = new();

            public IrcStream(string server, int port, bool ssl, bool ignoreCert)
            {
                var tcpClient = new TcpClient();
                if (!tcpClient.ConnectAsync(server, port).Wait(10000))
                {
                    throw new TimeoutException("IRC connection timeout");
                }
                var tcpStream = tcpClient.GetStream();
                _stream = ssl ? GetSslStream(tcpStream, server, ignoreCert) : tcpStream;
                _streamReader = new StreamReader(_stream, leaveOpen:true);
            }
            
            public void WriteLine(string input)
            {
                lock (_writeLock)
                {
                    _stream.Write(Encoding.UTF8.GetBytes(input));
                    _stream.Write(new byte[]{0x0d, 0x0a}); // crlf
                }
            }

            public string? ReadLine()
            {
                return _streamReader.ReadLine();
            }
            
            private static SslStream GetSslStream(Stream tcpStream, string servername, bool ignoreSslCert)
            {
                SslStream? outputStream = null;
                try
                {
                    outputStream = new SslStream(tcpStream, false,
                        ignoreSslCert ? (_, _, _, _) => true : (_, _, _, s) => s == SslPolicyErrors.None, null);
                    outputStream.AuthenticateAsClient(servername);
                    return outputStream;
                }
                catch (Exception)
                {
                    outputStream?.Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                _streamReader.Dispose();
                _stream.Dispose();
            }
        }
        
        private class ParsedCommand
        {
            public readonly string? Origin;
            public readonly string Command;
            public readonly List<string>? Params;

            private ParsedCommand(string? origin, string command, List<string>? @params)
            {
                Origin = origin;
                Command = command;
                Params = @params;
            }

            public static ParsedCommand? Parse(string input)
            {
                if (string.IsNullOrEmpty(input)) return null;
                string? origin = null;
                var prefixSpace = 0;
                if (input[0] == ':')
                {
                    prefixSpace = input.IndexOf(' ');
                    if (prefixSpace == -1) return null;
                    origin = input[1..prefixSpace];
                }

                var commandIndex = prefixSpace == 0 ? 0 : prefixSpace + 1;
                var trailingIndex = input.IndexOf(':', prefixSpace);
                string fullCommand;
                string? trailing = null;
                if (trailingIndex == -1)
                {
                    if (input.Length <= commandIndex) return null;
                    fullCommand = input[commandIndex..];
                }
                else
                {
                    fullCommand = input[commandIndex..trailingIndex];
                    if (input.Length <= trailingIndex + 1) return null;
                    trailing = input[(trailingIndex + 1)..];
                }
                var tempParams = fullCommand.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                if (tempParams.Length == 0) return null;
                var command = tempParams[0];
                var parameters = new List<string>();
                if (tempParams.Length > 1) parameters.AddRange(tempParams[1..]);
                if (trailing != null) parameters.Add(trailing);
                return new ParsedCommand(origin, command, parameters);
            }
        }

        private string Server { get; }
        private int Port { get; }
        private string Username { get; }
        private string Nick { get; }
        private bool Ssl { get; }
        private bool IgnoreSslCert { get; }
        private bool IrcLog { get; }

        private enum ClientState
        {
            Dead, Starting, Running
        }

        private ClientState _state = ClientState.Dead;
        private readonly object _stateLock = new();

        private IrcStream? _stream;

        public delegate void WelcomeEventHandler(IrcClient bot);
        public event WelcomeEventHandler? Welcome;

        public delegate void MessageEventHandler(IrcClient bot, string source, string author, string content);
        public event MessageEventHandler? Message;
        
        public delegate void JoinEventHandler(IrcClient bot, string channel, string user);
        public event JoinEventHandler? Join;

        public delegate void LeaveEventHandler(IrcClient bot, string channel, string user, string? reason);
        public event LeaveEventHandler? Leave;
        
        public delegate void QuitEventHandler(IrcClient bot, string user, string? reason);
        public event QuitEventHandler? Quit;
        
        public IrcClient(string server, int port, string username, string nick, bool ssl, bool ignoreSslCert, bool ircLog)
        {
            Server = server;
            Port = port;
            Username = username;
            Nick = nick;
            Ssl = ssl;
            IgnoreSslCert = ignoreSslCert;
            IrcLog = ircLog;
        }

        private void EnsureAlive()
        {
            lock (_stateLock)
            {
                if (_state != ClientState.Running) throw new InvalidOperationException("This client is not running.");
            }
        }

        public bool IsAlive()
        {
            lock (_stateLock) return _state == ClientState.Running;
        }
        
        private void KillAndDispose()
        {
            lock (_stateLock)
            {
                if (_state == ClientState.Dead) return;
                _state = ClientState.Dead;
            }
            _stream?.Dispose(); _stream = null;
        }
        
        public void RequestDisconnect()
        {
            EnsureAlive();
            _stream?.WriteLine("QUIT");
        }
        
        public void SendMessage(string target, string message)
        {
            EnsureAlive();
            _stream?.WriteLine($"PRIVMSG {target} :{message}");
        }

        public void ExecuteRaw(string raw)
        {
            EnsureAlive();
            _stream?.WriteLine(raw);
        }

        public void SetSelfMode(string mode)
        {
            EnsureAlive();
            _stream?.WriteLine($"MODE {Nick} {mode}");
        }

        public void JoinChannel(string channel)
        {
            EnsureAlive();
            _stream?.WriteLine($"JOIN {channel}");
        }

        private static string GetAuthor(string prefix)
        {
            var sepIndex = prefix.IndexOf('!');
            return sepIndex == -1 ? prefix : prefix[..sepIndex];
        }
        
        public void Start()
        {
            lock (_stateLock)
            {
                if (_state != ClientState.Dead) throw new InvalidOperationException("This bot is already running (or still starting).");
                _state = ClientState.Starting;
            }
            try
            {
                _stream = new IrcStream(Server, Port, Ssl, IgnoreSslCert);

                lock (_stateLock) _state = ClientState.Running;

                _stream.WriteLine($"NICK {Nick}");
                _stream.WriteLine($"USER {Username} 0 * :{Username}");

                while (_stream.ReadLine() is { } inputLine)
                {
                    if (IrcLog) TShock.Log.Info($"> {inputLine}");
                    var message = ParsedCommand.Parse(inputLine);
                    if (message == null)
                    {
                        TShock.Log.Error("IRC Server sent malformed message: '{0}'", inputLine);
                        continue;
                    }

                    if (ushort.TryParse(message.Command, NumberStyles.None, null, out var numericCode))
                    {
                        switch (numericCode)
                        {
                            case 001:
                                Welcome?.Invoke(this);
                                break;
                            case > 400 and < 600:
                                TShock.Log.ConsoleWarn("IRC error: '{0}'", inputLine);
                                break;
                        }

                        continue;
                    }

                    switch (message.Command)
                    {
                        case "PING":
                            if (message.Params != null)
                            {
                                _stream.WriteLine($"PONG {message.Params.Last()}");
                            }
                            break;
                        case "PRIVMSG":
                            if (message.Params != null && message.Origin != null)
                            {
                                Message?.Invoke(this, message.Params[0], GetAuthor(message.Origin),
                                    message.Params[1]);
                            }
                            break;
                        case "PART":
                            if (message.Params != null && message.Origin != null)
                            {
                                Leave?.Invoke(this, message.Params[0], GetAuthor(message.Origin), message.Params.ElementAtOrDefault(1));
                            }
                            break;
                        case "QUIT":
                            if (message.Params != null && message.Origin != null)
                            {
                                Quit?.Invoke(this, GetAuthor(message.Origin), message.Params.LastOrDefault());
                            }
                            break;
                        case "JOIN":
                            if (message.Params != null && message.Origin != null)
                            {
                                Join?.Invoke(this, message.Params.Last(), GetAuthor(message.Origin));
                            }
                            break;
                    }
                }
            }
            finally { KillAndDispose(); }
        }
    }
}