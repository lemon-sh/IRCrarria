using System;
using IrcDotNet;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace IRCrarria
{
    [ApiVersion(2, 1)]
    public class IRCrarria : TerrariaPlugin
    {
        private const string IRCurl = "";
        private StandardIrcClient irc;
        private IrcChannel ircChannel;
        
        public override string Author => "lemon-sh";
        public override string Name => "IRCrarria";
        public override string Description => "IRC<->Terraria bridge that actually works with new TShock versions";
        public override Version Version => new Version(1, 0, 0, 0);

        public IRCrarria(Main game) : base(game) { }

        public override void Initialize()
        {
            PlayerHooks.PlayerChat += OnChat;
            irc = new StandardIrcClient();
            irc.Registered += (sender, args) =>
            {
                irc.LocalUser.JoinedChannel += (sender2, args2) =>
                {
                    ircChannel = args2.Channel;
                };
                irc.Channels.Join("#main");
            };
            irc.Connect(new Uri(IRCurl), new IrcUserRegistrationInfo {
                NickName = "ircrarria", UserName = "ircrarria", RealName = "IRCrarria"
            });
        }

        private void OnChat(PlayerChatEventArgs ev)
        {
            irc.LocalUser.SendMessage(ircChannel, ev.RawText);
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
