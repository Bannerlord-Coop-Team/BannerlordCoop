using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _isCurrentlyUsedByAQuest
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkIsCurrentlyUsedByAQuestChanged(bool IsCurrentlyUsedByAQuest, string MobilePartyId) : ICommand
{
    public bool IsCurrentlyUsedByAQuest { get; } = IsCurrentlyUsedByAQuest;
    public string MobilePartyId { get; } = MobilePartyId;
}