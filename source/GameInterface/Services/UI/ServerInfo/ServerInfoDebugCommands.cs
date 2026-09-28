#if DEBUG
using Common.Commands;
using System;
using System.Linq;

namespace GameInterface.Services.UI.ServerInfo;

/// <summary>[Debug] Shows and inspects the message of the day popup on a client without a server file.</summary>
internal class ServerInfoDebugCommands
{
    internal static readonly string[] SampleParagraphs =
    {
        "Welcome to the server. This is a preview of the message of the day, shown once each time you join.",
        "Be kind in chat, keep battles fair and ask before you join another player's army.",
        "The server restarts every day at 06:00 UTC. The world is saved before each restart.",
    };

    // Ten paragraphs, the server's paragraph cap, so the popup reaches its maximum height and scrolls.
    internal static readonly string[] LongSampleParagraphs =
    {
        "Welcome to the server. This preview has ten paragraphs, the most the server sends, so the popup reaches its full height and the text scrolls.",
        "Be kind in chat. Keep battles fair, and ask before you join another player's army or besiege a settlement another player is already attacking.",
        "The server restarts every day at 06:00 UTC. The world is saved before each restart, so finish your battle and wait on the map when the warning appears.",
        "Wars between player kingdoms need a reason in chat first. Raiding the villages of a player who is offline is not allowed.",
        "Trade between players is fine. Do not sell items that were duplicated by a bug; report the bug instead so it can be fixed.",
        "Keep your party and clan names readable. Names that copy another player or an admin may be changed without notice.",
        "Report problems in the server's Discord channel with the time, your hero name and what you were doing. Logs help more than screenshots.",
        "Mods beyond the co-op mod and its dependencies are not supported here. A different mod list can stop you from joining.",
        "Voice chat is push to talk. Keep it to the players near you and mute when you step away from the keyboard.",
        "Have fun, and thank you for playing. This last paragraph sits below the fold and only shows after scrolling down.",
    };

    public sealed class ServerInfoPreviewCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";
        public string Name => "motd_preview";
        public string Description => "Shows the message of the day popup with sample text, a long sample that scrolls, or your own text.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } =
        {
            new ExpectedArgs("text", "long for the scrolling sample, or quoted text with | between paragraphs", isRequired: false)
        };

        // Goes through the same pending and focus rules as a real join, so it opens once the map is free.
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ContainerProvider.TryResolve<IServerInfoService>(out var service))
                return new CoopCommandResult(false, "Message of the day unavailable.", "unavailable");
            service.Show(new NetworkServerInfo(Paragraphs(args), null, null, null));
            return new CoopCommandResult(true, service.Describe());
        }

        private static string[] Paragraphs(ICoopCommandArgs args)
        {
            if (args.Count == 0) return SampleParagraphs;
            if (args[0] == "long") return LongSampleParagraphs;
            return args[0].Split('|').Select(paragraph => paragraph.Trim()).ToArray();
        }
    }

    public sealed class ServerInfoStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";
        public string Name => "motd_state";
        public string Description => "Reports whether the message of the day popup is open, what it shows and whether a message is pending.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        // Reads only this client's popup state.
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ContainerProvider.TryResolve<IServerInfoService>(out var service))
                return new CoopCommandResult(false, "Message of the day unavailable.", "unavailable");
            return new CoopCommandResult(true, service.Describe());
        }
    }
}
#endif
