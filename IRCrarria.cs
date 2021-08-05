using System;
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

        public IRCrarria(Main game) : base(game) { }

        public override void Initialize()
        {
            PlayerHooks.PlayerChat += OnChat;
        }

        private static void OnChat(PlayerChatEventArgs ev)
        {
            TShock.Utils.Broadcast($"[c/FF66FF:{ev.RawText}]", Microsoft.Xna.Framework.Color.LightCyan);
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
