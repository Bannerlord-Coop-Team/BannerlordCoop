using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BugReporting.Messages;

/// <summary>Submits a player-written bug report to the authoritative server.</summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestBugReport : ICommand
{
    public const int MaximumSummaryLength = 120;
    public const int MaximumDescriptionLength = 2000;

    [ProtoMember(1)]
    public string Summary { get; }

    [ProtoMember(2)]
    public string Description { get; }

    public NetworkRequestBugReport(string summary, string description)
    {
        Summary = summary;
        Description = description;
    }
}
