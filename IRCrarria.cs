using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IrcDotNet;
using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace IRCrarria
{
    [ApiVersion(2, 1)]
    public class IRCrarria : TerrariaPlugin
    {
        public sealed override string Author => "lemon-sh";
        public sealed override string Name => "IRCrarria";
        public sealed override string Description => "IRC<->Terraria bridge that actually works with new TShock versions";
        public sealed override Version Version => typeof(IRCrarria).Assembly.GetName().Version;

        private readonly IEnumerable<string> _helpText;
        private static readonly string ConfigPath = Path.Combine(TShock.SavePath, "ircrarria.toml");
        private static readonly DateTime StartTime = DateTime.Now;
        
        private static readonly Regex JoinLeftRegex = new Regex(@"^.+ has (joined|left).$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        public static readonly Regex StripRegex = new Regex(@"\x03(?:\d{1,2}(?:,\d{1,2})?)|[^\u0020-\u007E]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly Config _cfg;
        private StandardIrcClient _irc;
        private IrcChannel _ircChannel;

        public IRCrarria(Main game) : base(game)
        {
            var configText = File.ReadAllText(ConfigPath);
            _cfg = new Config(configText);
            _helpText = new List<string>
            {
                $"==--- {Name} {Version} ---==",
                $"{_cfg.Prefix}help - display this helpscreen",
                $"{_cfg.Prefix}playing - list people online on the server",
                $"{_cfg.Prefix}serverinfo - display server info",
                $"{_cfg.Prefix}uptime - display server uptime"
            };
        }

        public override void Initialize()
        {
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.ServerBroadcast.Register(this, OnBroadcast);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
            PlayerHooks.PlayerChat += OnChat;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.ServerBroadcast.Register(this, OnBroadcast);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
                PlayerHooks.PlayerChat -= OnChat;
                _irc.Disconnect();
                _irc.Dispose();
            }
            base.Dispose(disposing);
        }
        
        private void OnChat(PlayerChatEventArgs ev)
        {
            var strippedText = ev.RawText.StripNonAscii();
            _irc.LocalUser.SendMessage(_ircChannel, $"\x00039<\x00038{ev.Player.Name}\x00039>\x3 {strippedText}");
            TShock.Utils.Broadcast($"[c/28FFBF:TER] [c/BCFFB9:{ev.Player.Name}] {strippedText}", Color.White);
            ev.Handled = true;
        }

        private void OnJoin(JoinEventArgs args)
        {
            _irc.LocalUser.SendMessage(_ircChannel, $"\x00038{TShock.Players[args.Who].Name}\x00039 joined the game.");
        }

        private void OnLeave(LeaveEventArgs args)
        {
            _irc.LocalUser.SendMessage(_ircChannel, $"\x00038{TShock.Players[args.Who].Name}\x00034 left the game.");
        }
        
        private void OnBroadcast(ServerBroadcastEventArgs args)
        {
            var text = args.Message._text;
            if (JoinLeftRegex.IsMatch(text)
                || text.Equals("Saving world...", StringComparison.OrdinalIgnoreCase)
                || text.Equals("World saved.", StringComparison.OrdinalIgnoreCase)
                || text.Contains("IRC") || text.Contains("TER")) return;
            _irc.LocalUser.SendMessage(_ircChannel, $"\x000311{text}");
        }
        
        private void OnPostInitialize(EventArgs args)
        {
            _irc = new StandardIrcClient();
            _irc.Registered += (isn, iarg) =>
            {
                // this is where LocalUser becomes available
                _irc.LocalUser.SetModes('B');
                _irc.LocalUser.JoinedChannel += (jsn, jarg) =>
                {
                    if (_ircChannel != null) return;
                    _ircChannel = jarg.Channel;
                    _ircChannel.MessageReceived += (ksn, karg) =>
                    {
                        var text = karg.Text.StripNonAscii();
                        if (text.Equals(_cfg.Prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            
                        }
                        else
                        {
                            TShock.Utils.Broadcast($"[c/CE1F6A:IRC] [c/FF9A8C:{karg.Source.Name}] {text}", Color.White);
                        }
                    };
                };
                _irc.Channels.Join(_cfg.Channel);
            };
            _irc.Connect(_cfg.Hostname, _cfg.Port, _cfg.UseSsl, new IrcUserRegistrationInfo {
                NickName = _cfg.Nickname, UserName = _cfg.Username, RealName = _cfg.Username
            });
        }

        // ideas for commands "inspired" by terracord (https://github.com/FragLand/terracord)
        private void ExecuteCommand(string text)
        {
            if (!text.StartsWith(_cfg.Prefix, StringComparison.Ordinal)) return;
            switch (text.Substring(_cfg.Prefix.Length))
            {
                case "help":
                    foreach (var line in _helpText)
                    {
                        _irc.LocalUser.SendMessage(_ircChannel, line);
                    }
                    break;
                case "playing":
                    _irc.LocalUser.SendMessage(_ircChannel,
                        $"{TShock.Utils.GetActivePlayerCount()}/{TShock.Config.Settings.MaxSlots} online.");
                    var playersOnline = new StringBuilder(256);
                    foreach (var player in TShock.Players) if (player != null && player.Active)
                    {
                        playersOnline.Append(player.Name).Append("; ");
                    }
                    _irc.LocalUser.SendMessage(_ircChannel, playersOnline.ToString());
                    break;
                case "serverinfo":
                    _irc.LocalUser.SendMessage(_ircChannel, $"Server Name: {TShock.Config.Settings.ServerName}");
                    _irc.LocalUser.SendMessage(_ircChannel, $"Players: {TShock.Utils.GetActivePlayerCount()}/{TShock.Config.Settings.MaxSlots}");
                    _irc.LocalUser.SendMessage(_ircChannel, $"TShock Version: {TShock.VersionNum}");
                    break;
                case "uptime":
                    var elapsed = DateTime.Now.Subtract(StartTime);
                    _irc.LocalUser.SendMessage(_ircChannel,
                        $"Plugin uptime: {elapsed.Days}d {elapsed.Hours}h {elapsed.Minutes}min {elapsed.Seconds}s");
                    break;
                default:
                    _irc.LocalUser.SendMessage(_ircChannel, "Invalid command!");
                    break;
            }
        }
    }

    public static class StringExtensions
    {
        public static string StripNonAscii(this string str) =>
            IRCrarria.StripRegex.Replace(str, string.Empty);
    }
}
