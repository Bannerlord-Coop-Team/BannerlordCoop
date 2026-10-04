#if DEBUG
using Common.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.UI.ServerInfo;

/// <summary>[Debug] Shows and inspects the server info panel on a client without a server file.</summary>
internal class ServerInfoDebugCommands
{
    internal static readonly string[] SampleParagraphs =
    {
        "Welcome to the server. This is a preview of the server info panel, shown once each time you join.",
        "Be kind in chat, keep battles fair and ask before you join another player's army.",
        "The server restarts every day at 06:00 UTC. The world is saved before each restart.",
    };

    // Ten paragraphs, the server's paragraph cap, so the panel reaches its maximum height and scrolls.
    internal static readonly string[] LongSampleParagraphs =
    {
        "Welcome to the server. This preview has ten paragraphs, the most the server sends, so the panel reaches its full height and the text scrolls.",
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

    internal static readonly string[] SampleRules =
    {
        "Be respectful in chat and voice.",
        "No griefing, exploiting or duplicating items.",
        "Ask before joining another player's army.",
        "Declare wars between player kingdoms in chat first.",
        "Do not raid the villages of a player who is offline.",
        "Admins have the final word in disputes.",
    };

    internal static readonly ServerInfoLink[] SampleLinks =
    {
        new ServerInfoLink { Label = "Discord", Url = "https://discord.gg/example" },
        new ServerInfoLink { Label = "Website", Url = "https://example.com/" },
        new ServerInfoLink { Label = "Steam Group", Url = "https://steamcommunity.com/groups/example" },
    };

    internal static readonly ServerInfoNews[] SampleNews =
    {
        new ServerInfoNews { Date = "28 Sep 2026", Title = "Siege weekend", Text = "All castle sieges this weekend give double renown." },
        new ServerInfoNews { Date = "25 Sep 2026", Title = "Server updated to 0.1.6", Text = "Update your mod to join. The old version can no longer connect." },
        new ServerInfoNews { Date = "20 Sep 2026", Title = "Tournament in Danustica", Text = "Saturday 19:00 UTC, prize 50000 denars." },
    };

    // Links the client must refuse even if a server sent them; only the two good ones may show.
    internal static readonly ServerInfoLink[] BadSampleLinks =
    {
        new ServerInfoLink { Label = "Discord", Url = "https://discord.gg/example" },
        new ServerInfoLink { Label = "Script", Url = "javascript:void(0)" },
        new ServerInfoLink { Label = "Local file", Url = "file:///C:/Windows/win.ini" },
        new ServerInfoLink { Label = "Login", Url = "https://user:password@example.com/" },
        new ServerInfoLink { Label = "Relative", Url = "/rules" },
        new ServerInfoLink { Label = "Spaces", Url = "https://example.com/a b" },
        new ServerInfoLink { Label = "FTP", Url = "ftp://example.com/file" },
        new ServerInfoLink { Label = "", Url = "https://example.com/rules" },
    };

    internal static readonly IReadOnlyDictionary<string, NetworkServerInfo> Samples = new Dictionary<string, NetworkServerInfo>(StringComparer.OrdinalIgnoreCase)
    {
        ["full"] = new NetworkServerInfo(SampleParagraphs, SampleRules, SampleLinks, SampleNews),
        // Every section at its count cap, so each tab scrolls.
        ["long"] = new NetworkServerInfo(
            LongSampleParagraphs,
            Enumerable.Range(1, ServerInfoLimits.MaxRules).Select(index => $"Sample rule {index}: keep to the spirit of the rules above, and ask an admin when unsure.").ToArray(),
            Enumerable.Range(1, ServerInfoLimits.MaxLinks).Select(index => new ServerInfoLink { Label = $"Sample link {index}", Url = $"https://example.com/link/{index}" }).ToArray(),
            Enumerable.Range(1, ServerInfoLimits.MaxNews).Select(index => new ServerInfoNews
            {
                Date = $"{29 - index} Sep 2026",
                Title = $"Sample news {index}",
                Text = "A sample entry long enough to wrap onto a second line in the panel, so the news tab shows how longer entries read.",
            }).ToArray()),
        ["motd-only"] = new NetworkServerInfo(SampleParagraphs, null, null, null),
        ["bad-links"] = new NetworkServerInfo(new[] { "Only the Discord link and the rules address are safe to show." }, null, BadSampleLinks, null),
    };

    public sealed class ServerInfoPreviewCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";
        public string Name => "server_info_preview";
        public string Description => "Shows the server info panel with a sample: full, long (every tab scrolls), motd-only or bad-links.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } =
        {
            new ExpectedArgs("sample", "full (default), long, motd-only or bad-links", isRequired: false)
        };

        // Goes through the same client link check, pending and focus rules as a real join.
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string sample = args.Count == 0 ? "full" : args[0];
            if (!Samples.TryGetValue(sample, out var info))
                return new CoopCommandResult(false, "Unknown sample. Use " + string.Join(", ", Samples.Keys) + ".", "invalid_sample");
            if (!ContainerProvider.TryResolve<IServerInfoService>(out var service))
                return new CoopCommandResult(false, "Server info unavailable.", "unavailable");
            return Preview(service, info);
        }

        // After the join's own open, new info no longer opens the panel by itself, so the preview opens it like !motd.
        internal static CoopCommandResult Preview(IServerInfoService service, NetworkServerInfo info)
        {
            service.Show(info);
            service.Reopen();
            return new CoopCommandResult(true, service.Describe());
        }
    }

    public sealed class ServerInfoStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";
        public string Name => "server_info_state";
        public string Description => "Reports whether the server info panel is open, its tab, what is pending, the counts per tab and the link dialog.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        // Reads only this client's panel state.
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ContainerProvider.TryResolve<IServerInfoService>(out var service))
                return new CoopCommandResult(false, "Server info unavailable.", "unavailable");
            return new CoopCommandResult(true, service.Describe());
        }
    }
}
#endif
