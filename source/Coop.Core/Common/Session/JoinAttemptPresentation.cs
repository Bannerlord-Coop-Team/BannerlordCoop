using System;

namespace Coop.Core.Common.Session;

/// <summary>
/// The wording a join attempt shows while it is in flight: loading-screen title and description,
/// the cancel button's label, and what the player is told once they cancel.
/// </summary>
public sealed class JoinAttemptPresentation
{
    // Matches the title the post-connect states keep using, so a successful attempt advances the
    // description without the heading changing under the player.
    internal const string JoiningTitle = "Connecting to Coop Server";
    private const string HostingTitle = "Hosting Coop Server";
    private const string PlayerCancelLabel = "Cancel";
    private const string PlayerCancelledNotice = "Connection attempt cancelled";

    private JoinAttemptPresentation(
        JoinIntent intent, string title, string description, string cancelLabel, string cancelledNotice)
    {
        Intent = intent;
        Title = title;
        Description = description;
        CancelLabel = cancelLabel;
        CancelledNotice = cancelledNotice;
    }

    public JoinIntent Intent { get; }
    public string Title { get; }
    public string Description { get; }
    public string CancelLabel { get; }
    public string CancelledNotice { get; }

    public static JoinAttemptPresentation For(JoinIntent intent, string address, int port) => intent switch
    {
        JoinIntent.PlayerDirect => new JoinAttemptPresentation(intent, JoiningTitle,
            $"Contacting {address}:{port}...", PlayerCancelLabel, PlayerCancelledNotice),

        // A tunnelled Steam join dials a local pump, so the host is named by route not by address.
        JoinIntent.PlayerSteam => new JoinAttemptPresentation(intent, JoiningTitle,
            "Contacting the host through Steam...", PlayerCancelLabel, PlayerCancelledNotice),

        // Cancelling abandons the wait, not the server, which this instance never owns.
        JoinIntent.HostLoopback => new JoinAttemptPresentation(intent, HostingTitle,
            "Waiting for the server to load the campaign save...", "Stop Waiting",
            "Stopped waiting for the co-op server. Its window stays open until you close it."),

        _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unknown join intent"),
    };
}
