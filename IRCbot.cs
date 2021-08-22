using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;

namespace IRCrarria
{
    public class IRCbot
    {
        private string Server { get; }
        private int Port { get; }
        private string Username { get; }
        private string Nick { get; }
        private bool Ssl { get; }
        private bool IgnoreSslCert { get; }

        private volatile bool _dead;
        private StreamWriter _writer;
        private Mutex _writerLock;
        private TcpClient _irc;
        private SslStream _sslStream;
        private StreamReader _reader;

        public delegate void WelcomeEventHandler(IRCbot bot);
        public event WelcomeEventHandler Welcome;

        public delegate void MessageEventHandler(IRCbot bot, string source, string author, string content);
        public event MessageEventHandler Message;
        
        public delegate void JoinEventHandler(IRCbot bot, string channel, string user);
        public event JoinEventHandler Join;

        public delegate void LeaveEventHandler(IRCbot bot, string channel, string user);
        public event LeaveEventHandler Leave;
        
        public IRCbot(string server, int port, string username, string nick, bool ssl, bool ignoreSslCert)
        {
            Server = server;
            Port = port;
            Username = username;
            Nick = nick;
            Ssl = ssl;
            IgnoreSslCert = ignoreSslCert;
            _dead = true;
        }

        private void EnsureAlive()
        {
            // this actually made me laugh
            if (_dead) throw new InvalidOperationException("This bot is dead.");
        }
        
        private void KillAndDispose()
        {
            if (_dead) return;
            _dead = true;
            _sslStream?.Dispose(); _sslStream = null;
            _irc?.Dispose(); _irc = null;
            _reader?.Dispose(); _reader = null;
            _writer?.Dispose(); _writer = null;
            _writerLock?.Dispose(); _writerLock = null;
        }

        // this is not the cleanest method, but I got no idea on how to cancel the ReadLine();
        public void RequestDisconnect()
        {
            if (!_dead) SyncWriteStream("QUIT");
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
            public string Origin;
            public string Command;
            public List<string> Params;
        }

        // returns null on malformed input
        [Pure]
        private static ParsedCommand ParseIrc(string input)
        {
            var parsedCommand = new ParsedCommand();
            if (string.IsNullOrEmpty(input)) return null;
            var prefixLength = 0;
            if (input[0] == ':')
            {
                prefixLength = input.IndexOf(' ');
                if (prefixLength == -1) return null;
                parsedCommand.Origin = input.Substring(1, prefixLength-1);
            }

            var commandIndex = prefixLength == 0 ? 0 : prefixLength + 1;
            var trailingIndex = input.IndexOf(':', 1);
            string command, trailing = null;
            if (trailingIndex == -1)
            {
                if (input.Length <= commandIndex) return null;
                command = input.Substring(commandIndex);
            }
            else
            {
                command = input.Substring(commandIndex, trailingIndex-commandIndex);
                if (input.Length <= trailingIndex + 1) return parsedCommand;
                trailing = input.Substring(trailingIndex + 1);
            }
            var tempParams = command.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            parsedCommand.Command = tempParams.First();
            parsedCommand.Params = new List<string>();
            if (tempParams.Length > 1) parsedCommand.Params.AddRange(tempParams.Skip(1));
            if (trailing != null) parsedCommand.Params.Add(trailing);
            return parsedCommand;
        }

        private SslStream GetSslStream(Stream tcpStream, string servername)
        {
            SslStream outputStream = null;
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
            _writerLock.WaitOne(-1);
#if DEBUG
            Console.WriteLine("-> " + input);
#endif
            _writer.WriteLine(input);
            _writer.Flush();
            _writerLock.ReleaseMutex();
        }

        private string GetAuthor(string prefix)
        {
            var sepIndex = prefix.IndexOf('!');
            return sepIndex == -1 ? prefix : prefix.Substring(0, sepIndex);
        }
        
        public void Start()
        {
            if (!_dead) throw new InvalidOperationException("This bot is already running.");
            try
            {
                _dead = false;
                _writerLock = new Mutex();
                _irc = new TcpClient(Server, Port);
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
                
                SyncWriteStream($"NICK {Nick}");
                SyncWriteStream($"USER {Username} 0 * :{Username}");

                string inputLine;
                while ((inputLine = _reader.ReadLine()) != null)
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