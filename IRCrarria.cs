using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace IRCrarria
{
    [ApiVersion(2, 1)]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class IRCrarria : TerrariaPlugin
    {
        public sealed override string Author => "lemon-sh";
        public sealed override string Name => "IRCrarria";
        public sealed override string Description =>
            "IRC<->Terraria bridge that actually works with new TShock versions";
        public sealed override Version Version => typeof(IRCrarria).Assembly.GetName().Version!;

        private readonly IEnumerable<string> _helpText;
        private static readonly string ConfigPath = Path.Combine(TShock.SavePath, "ircrarria.toml");
        private static readonly DateTime StartTime = DateTime.Now;

        private static readonly Regex JoinLeftRegex = new(@"^.+ has (joined|left).$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static readonly Regex StripRegex = new(@"\x03(?:\d{1,2}(?:,\d{1,2})?)|[^\u0020-\u007E]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly Config _cfg;
        private IrcClient _irc = null!;

        public IRCrarria(Main game) : base(game)
        {
            var configText = File.ReadAllText(ConfigPath);
            _cfg = new Config(configText);
            _helpText = new List<string>
            {
                $"==--- {Name} {Version} ---==",
                $"{_cfg.Prefix}help - display this helpscreen",
                $"{_cfg.Prefix}serverinfo - display server info",
                $"{_cfg.Prefix}playing - list online players"
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
                _irc.Welcome -= OnIrcWelcome;
                _irc.Message -= OnIrcMessage;
                _irc.Join -= OnIrcJoin;
                _irc.Leave -= OnIrcLeave;
                _irc.Quit -= OnIrcQuit;
                _irc.RequestDisconnect();
            }

            base.Dispose(disposing);
        }

        private void OnChat(PlayerChatEventArgs ev)
        {
            var strippedText = ev.RawText.StripNonAscii();
            _irc.SendMessage(_cfg.Channel, $"\x00039<\x00038{ev.Player.Name}\x00039>\x3 {strippedText}");
            TShock.Utils.Broadcast($"[c/28FFBF:TER] [c/BCFFB9:{ev.Player.Name}] {strippedText}", Color.White);
            ev.Handled = true;
        }

        private void OnJoin(JoinEventArgs args)
        {
            var player = TShock.Players[args.Who];
            if (player != null) _irc.SendMessage(_cfg.Channel, $"\x00038{player.Name}\x00039 joined the game.");
        }

        private void OnLeave(LeaveEventArgs args)
        {
            var player = TShock.Players[args.Who];
            if (player != null) _irc.SendMessage(_cfg.Channel, $"\x00038{player.Name}\x00034 left the game.");
        }

        private void OnBroadcast(ServerBroadcastEventArgs args)
        {
            var text = args.Message.ToString();
            if (text.StartsWith("[c/CE1F6A:IRC]", StringComparison.Ordinal)
                || text.StartsWith("[c/28FFBF:TER]", StringComparison.Ordinal)
                || JoinLeftRegex.IsMatch(text)
                || text.Equals("Saving world...", StringComparison.OrdinalIgnoreCase)
                || text.Equals("World saved.", StringComparison.OrdinalIgnoreCase)
            ) return;
            _irc.SendMessage(_cfg.Channel, $"\x000311{text}");
        }

        private void OnPostInitialize(EventArgs args)
        {
            _irc = new IrcClient(_cfg.Hostname, _cfg.Port, _cfg.Username, _cfg.Nickname, _cfg.UseSsl, _cfg.SkipCertValidation);
            _irc.Welcome += OnIrcWelcome;
            _irc.Message += OnIrcMessage;
            _irc.Join += OnIrcJoin;
            _irc.Leave += OnIrcLeave;
            _irc.Quit += OnIrcQuit;
            new Thread(_ => _irc.Start()).Start();
        }

        private void OnIrcWelcome(IrcClient bot)
        {
            bot.SetSelfMode("+B");
            if (_cfg.ConnectCommands != null)
                foreach (var command in _cfg.ConnectCommands)
                    bot.ExecuteRaw(command);

            bot.JoinChannel(_cfg.Channel);
        }

        private void OnIrcMessage(IrcClient _, string source, string author, string content)
        {
            if (source != _cfg.Channel) return;
            var text = content.StripNonAscii();
            if (!ExecuteCommand(text)) TShock.Utils.Broadcast($"[c/CE1F6A:IRC] [c/FF9A8C:{author}] {text}", Color.White);
        }

        private static void OnIrcLeave(IrcClient _, string channel, string user, string? reason)
        {
            var msg = reason != null
                ? $"[c/CE1F6A:IRC] [c/FF9A8C:{user} has left {channel}:] {reason}"
                : $"[c/CE1F6A:IRC] [c/FF9A8C:{user} has left {channel}.]";
            TShock.Utils.Broadcast(msg, Color.White);
        }
        
        private static void OnIrcQuit(IrcClient _, string user, string? reason)
        {
            var msg = reason != null
                ? $"[c/CE1F6A:IRC] [c/FF9A8C:{user} has quit:] {reason}"
                : $"[c/CE1F6A:IRC] [c/FF9A8C:{user} has quit.]";
            TShock.Utils.Broadcast(msg, Color.White);
        }

        private static void OnIrcJoin(IrcClient _, string channel, string user)
        {
            TShock.Utils.Broadcast($"[c/CE1F6A:IRC] [c/28FFBF:{user} joined {channel}]", Color.White);
        }
        
        private bool ExecuteCommand(string text)
        {
            if (!text.StartsWith(_cfg.Prefix, StringComparison.Ordinal)) return false;
            switch (text[_cfg.Prefix.Length..])
            {
                case "help":
                    foreach (var line in _helpText) _irc.SendMessage(_cfg.Channel, line);
                    break;
                case "serverinfo":
                    _irc.SendMessage(_cfg.Channel, "==--- Server Information ---==");
                    _irc.SendMessage(_cfg.Channel, $"TShock Version: {TShock.VersionNum}");
                    if (_cfg.ExtraDetails != null)
                    {
                        foreach (var detail in _cfg.ExtraDetails)
                        {
                            if (detail.Value is string value)
                                _irc.SendMessage(_cfg.Channel, $"{detail.Key}: {value}");
                        }
                    }
                    var elapsed = DateTime.Now.Subtract(StartTime);
                    _irc.SendMessage(_cfg.Channel,
                        $"Uptime: {elapsed.Days}d {elapsed.Hours}h {elapsed.Minutes}min {elapsed.Seconds}s");
                    _irc.SendMessage(_cfg.Channel, "==--------------------------==");
                    break;
                case "playing":
                    _irc.SendMessage(_cfg.Channel,
                        $"[{TShock.Utils.GetActivePlayerCount()}/{TShock.Config.Settings.MaxSlots}] players.");
                    var playersOnline = new StringBuilder(256);
                    foreach (var player in TShock.Players.Where(player => player is {Active: true}))
                    {
                        playersOnline.Append(player.Name).Append("; ");
                    }

                    if (playersOnline.Length > 0) _irc.SendMessage(_cfg.Channel, playersOnline.ToString());
                    break;
                default:
                    _irc.SendMessage(_cfg.Channel, "Invalid command!");
                    break;
            }
            return true;
        }
    }

    public static class StringExtensions
    {
        public static string StripNonAscii(this string str) =>
            IRCrarria.StripRegex.Replace(str, string.Empty);
    }
}
