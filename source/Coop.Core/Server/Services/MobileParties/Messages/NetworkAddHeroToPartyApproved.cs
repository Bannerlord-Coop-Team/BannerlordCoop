using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>
/// Add hero to party approved by server
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkAddHeroToPartyApproved : ICommand
{
    [ProtoMember(1)]
    public string HeroId { get; }
    [ProtoMember(2)]
    public string NewPartyId { get; }
    [ProtoMember(3)]
    public bool ShowNotification { get; }

    public NetworkAddHeroToPartyApproved(string heroId, string newPartyId, bool showNotification)
    {
        HeroId = heroId;
        NewPartyId = newPartyId;
        ShowNotification = showNotification;
    }
}
