using System;

namespace Coop.Core.Common.Session;

/// <summary>
/// The wording a join attempt shows while it is in flight: loading-screen title and description,
/// the cancel button's label, and what the player is told once they cancel.
/// </summary>
public sealed class JoinAttemptPresentation
{
    private const string JoiningTitle = "Connecting to Coop Server";
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

    public static JoinAttemptPresentation For(JoinIntent intent) => intent switch
    {
        JoinIntent.PlayerDirect => new JoinAttemptPresentation(intent, JoiningTitle,
            "Contacting the server...", PlayerCancelLabel, PlayerCancelledNotice),

        // A provider join can dial a local pump, so the host is named by route not by address.
        JoinIntent.PlayerProvider => new JoinAttemptPresentation(intent, JoiningTitle,
            "Contacting the host through the platform network...", PlayerCancelLabel, PlayerCancelledNotice),

        // Cancelling abandons the wait, not the server, which this instance never owns.
        JoinIntent.HostLoopback => new JoinAttemptPresentation(intent, HostingTitle,
            "Waiting for the server to load the campaign save...", "Stop Waiting",
            "Stopped waiting for the co-op server. Its window stays open until you close it."),

        _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unknown join intent"),
    };
}
