﻿using System.Net.Security;
using System.Net.Sockets;

namespace IRCrarria
{
    public class IrcClient
    {
        private class IrcStream : IDisposable
        {
            private readonly StreamReader _reader;
            private readonly TextWriter _writer;
            private readonly SslStream? _sslStream;
            private readonly TcpClient _tcpStream;

            public IrcStream(string server, int port, bool ssl, bool ignoreCert)
            {
                _tcpStream = new TcpClient();
                if (!_tcpStream.ConnectAsync(server, port).Wait(10000))
                {
                    throw new TimeoutException("IRC connection timeout");
                }
                if (ssl)
                {
                    _sslStream = GetSslStream(_tcpStream.GetStream(), server, ignoreCert);
                    _reader = new StreamReader(_sslStream);
                    _writer = new StreamWriter(_sslStream);
                }
                else
                {
                    _reader = new StreamReader(_tcpStream.GetStream());
                    _writer = new StreamWriter(_tcpStream.GetStream());
                }

                _writer = TextWriter.Synchronized(_writer);
            }
            
            public void WriteLine(string input)
            {
                _writer.Write(input);
                _writer.Write("\r\n");
            }

            public string? ReadLine()
            {
                return _reader.ReadLine();
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
                _reader.Dispose();
                _writer.Dispose();
                _sslStream?.Dispose();
                _tcpStream.Dispose();
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
        }
        
        private enum ClientState
        {
            Dead, Starting, Running
        }
        
        private string Server { get; }
        private int Port { get; }
        private string Username { get; }
        private string Nick { get; }
        private bool Ssl { get; }
        private bool IgnoreSslCert { get; }

        private ClientState _state = ClientState.Dead;
        private object _stateLock = new();

        private IrcStream? _stream;

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
        }

        private void EnsureAlive()
        {
            lock (_stateLock)
            {
                if (_state != ClientState.Running) throw new InvalidOperationException("This client is not running.");
            }
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
                    var message = ParsedCommand.Parse(inputLine);
                    if (message == null)
                    {
                        throw new IOException($"IRC Server sent malformed message: {inputLine}");
                    }

                    switch (message.Command)
                    {
                        case "PING":
                            _stream.WriteLine($"PONG {message.Params.Last()}");
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