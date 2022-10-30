using System.Net.Security;
using System.Net.Sockets;

namespace IRCrarria
{
    public class IrcClient
    {
        private enum BotState
        {
            Dead, Starting, Running
        }
        
        private string Server { get; }
        private int Port { get; }
        private string Username { get; }
        private string Nick { get; }
        private bool Ssl { get; }
        private bool IgnoreSslCert { get; }

        private BotState _state;
        private TextWriter? _writer;
        private TcpClient? _irc;
        private SslStream? _sslStream;
        private StreamReader? _reader;

        public delegate void WelcomeEventHandler(IrcClient bot);
        public event WelcomeEventHandler? Welcome;

        public delegate void MessageEventHandler(IrcClient bot, string source, string author, string content);
        public event MessageEventHandler? Message;
        
        public delegate void JoinEventHandler(IrcClient bot, string channel, string user);
        public event JoinEventHandler? Join;

        public delegate void LeaveEventHandler(IrcClient bot, string channel, string user);
        public event LeaveEventHandler? Leave;
        
        public IrcClient(string server, int port, string username, string nick, bool ssl, bool ignoreSslCert)
        {
            Server = server;
            Port = port;
            Username = username;
            Nick = nick;
            Ssl = ssl;
            IgnoreSslCert = ignoreSslCert;
            _state = BotState.Dead;
        }

        private void EnsureAlive()
        {
            if (_state != BotState.Running) throw new InvalidOperationException("This bot is dead.");
        }
        
        private void KillAndDispose()
        {
            if (_state == BotState.Dead) return;
            _state = BotState.Dead;
            _sslStream?.Dispose(); _sslStream = null;
            _irc?.Dispose(); _irc = null;
            _reader?.Dispose(); _reader = null;
            _writer?.Dispose(); _writer = null;
        }
        
        public void RequestDisconnect()
        {
            if (_state == BotState.Running) SyncWriteStream("QUIT");
        }
        
        public void SendMessage(string target, string message)
        {
            EnsureAlive();
            SyncWriteStream($"PRIVMSG {target} :{message}");
        }

        public void ExecuteRaw(string raw)
        {
            EnsureAlive();
            SyncWriteStream(raw);
        }

        public void SetSelfMode(string mode)
        {
            EnsureAlive();
            SyncWriteStream($"MODE {Nick} {mode}");
        }

        public void JoinChannel(string channel)
        {
            SyncWriteStream($"JOIN {channel}");
        }
        
        private class ParsedCommand
        {
            public readonly string? Origin;
            public readonly string Command;
            public readonly List<string>? Params;

            public ParsedCommand(string? origin, string command, List<string>? @params)
            {
                Origin = origin;
                Command = command;
                Params = @params;
            }
        }

        // returns null on malformed input
        private static ParsedCommand? ParseIrc(string input)
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
            var trailingIndex = input.IndexOf(':', 1);
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

        private SslStream? GetSslStream(Stream tcpStream, string servername)
        {
            SslStream? outputStream = null;
            try
            {
                outputStream = new SslStream(tcpStream, false,
                    IgnoreSslCert ? (_, _, _, _) => true : (_, _, _, s) => s == SslPolicyErrors.None, null);
                outputStream.AuthenticateAsClient(servername);
                return outputStream;
            }
            catch (Exception)
            {
                outputStream?.Dispose();
                throw;
            }
        }

        private void SyncWriteStream(string input)
        {
            if (_writer == null) return;
            _writer.WriteLine(input);
            _writer.Flush();
        }

        private string GetAuthor(string prefix)
        {
            var sepIndex = prefix.IndexOf('!');
            return sepIndex == -1 ? prefix : prefix[..sepIndex];
        }
        
        public void Start()
        {
            if (_state != BotState.Dead) throw new InvalidOperationException("This bot is already running.");
            try
            {
                _state = BotState.Starting;
                _irc = new TcpClient();
                if (!_irc.ConnectAsync(Server, Port).Wait(10000))
                {
                    throw new TimeoutException("IRC connection timeout");
                }
                if (Ssl)
                {
                    _sslStream = GetSslStream(_irc.GetStream(), Server);
                    _reader = new StreamReader(_sslStream);
                    _writer = new StreamWriter(_sslStream);
                }
                else
                {
                    _reader = new StreamReader(_irc.GetStream());
                    _writer = new StreamWriter(_irc.GetStream());
                }

                _writer = TextWriter.Synchronized(_writer);
                _state = BotState.Running;
                
                SyncWriteStream($"NICK {Nick}");
                SyncWriteStream($"USER {Username} 0 * :{Username}");

                while (_reader.ReadLine() is { } inputLine)
                {
                    var message = ParseIrc(inputLine);
                    if (message == null)
                    {
                        throw new IOException($"IRC Server sent malformed message: {inputLine}");
                    }

                    switch (message.Command)
                    {
                        case "PING":
                            SyncWriteStream($"PONG {message.Params.Last()}");
                            break;
                        case "001":
                            Welcome?.Invoke(this);
                            break;
                        case "PRIVMSG":
                            Message?.Invoke(this, message.Params[0], GetAuthor(message.Origin), message.Params.Last());
                            break;
                        case "PART":
                            Leave?.Invoke(this, message.Params.Last(), GetAuthor(message.Origin));
                            break;
                        case "JOIN":
                            Join?.Invoke(this, message.Params.Last(), GetAuthor(message.Origin));
                            break;
                    }
                }
            }
            finally { KillAndDispose(); }
        }
    }
}