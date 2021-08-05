using System;
using System.IO;
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

        private Config _cfg;
        private StandardIrcClient _irc;
        private IrcChannel _ircChannel;

        public IRCrarria(Main game) : base(game)
        {
            var configText = File.ReadAllText(ConfigPath);
            _cfg = new Config(configText);
        }

        public override void Initialize()
        {
            PlayerHooks.PlayerChat += OnChat;
            _irc = new StandardIrcClient();
            _irc.Registered += (isn, iarg) =>
            {
                _irc.LocalUser.JoinedChannel += (jsn, jarg) =>
                {
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

        private void OnChat(PlayerChatEventArgs ev)
        {
            _irc.LocalUser.SendMessage(_ircChannel, $"<{ev.Player.Name}> {ev.RawText}");
            TShock.Utils.Broadcast($"[c/28FFBF:Terraria] [c/BCFFB9:{ev.Player.Name}] {ev.RawText.StripNonAscii()}", Color.White);
            ev.Handled = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                PlayerHooks.PlayerChat -= OnChat;
            }
            base.Dispose(disposing);
        }
    }
}
