using System;
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
        public override string Author => "lemon-sh";
        public override string Name => "IRCrarria";
        public override string Description => "IRC<->Terraria bridge that actually works with new TShock versions";
        public override Version Version => new Version(1, 0, 0, 0);

        private static readonly string ConfigPath = Path.Combine(TShock.SavePath, "ircrarria.toml");

        private readonly Config _cfg;
        private StandardIrcClient _irc;
        private IrcChannel _ircChannel;

        public IRCrarria(Main game) : base(game)
        {
            var configText = File.ReadAllText(ConfigPath);
            _cfg = new Config(configText);
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
            _irc.LocalUser.SendMessage(_ircChannel, $"\x00039<\x00038{ev.Player.Name}\x00039>\x3 {ev.RawText}");
            TShock.Utils.Broadcast($"[c/28FFBF:TER] [c/BCFFB9:{ev.Player.Name}] {ev.RawText.StripNonAscii()}", Color.White);
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
            // stupid code to remove some of the terraria broadcasts, I don't think it can be deuglyfied
            if (Regex.IsMatch(text, "^.+ has (joined|left).$")
                || text.Equals("Saving world...", StringComparison.OrdinalIgnoreCase)
                || text.Equals("World saved.", StringComparison.OrdinalIgnoreCase)
                || text.Contains("IRC") || text.Contains("TER")) return;
            _irc.LocalUser.SendMessage(_ircChannel, $"\x000311{args.Message}");
        }
        
        private void OnPostInitialize(EventArgs args)
        {
            _irc = new StandardIrcClient();
            _irc.Registered += (isn, iarg) =>
            {
                _irc.LocalUser.SetModes('B');
                _irc.LocalUser.JoinedChannel += (jsn, jarg) =>
                {
                    if (_ircChannel != null) return;
                    jarg.Channel.MessageReceived += (ksn, karg) =>
                    {
                        TShock.Utils.Broadcast($"[c/CE1F6A:IRC] [c/FF9A8C:{karg.Source.Name}] {karg.Text.StripNonAscii()}", Color.White);
                    };
                    _ircChannel = jarg.Channel;
                };
                _irc.Channels.Join(_cfg.Channel);
            };
            _irc.Connect(_cfg.Hostname, _cfg.Port, _cfg.UseSsl, new IrcUserRegistrationInfo {
                NickName = _cfg.Nickname, UserName = _cfg.Username, RealName = _cfg.Username
            });
        }
    }
    
    public static class StringExtensions
    {
        public static string StripNonAscii(this string str)
        {
            return Encoding.ASCII.GetString(Encoding.Convert(Encoding.UTF8,
                Encoding.GetEncoding(Encoding.ASCII.EncodingName, new EncoderReplacementFallback(string.Empty),
                    new DecoderExceptionFallback()), Encoding.UTF8.GetBytes(str)));
        }
    }
}
