using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Hero Added to Party request
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkAddHeroToPartyRequest : ICommand
{
    [ProtoMember(1)]
    public string HeroId { get; }

    [ProtoMember(2)]
    public string NewPartyId { get; }
    [ProtoMember(3)]
    public bool ShowNotification { get; }

    public NetworkAddHeroToPartyRequest(string heroId, string newPartyId, bool showNotification)
    {
        HeroId = heroId;
        NewPartyId = newPartyId;
        ShowNotification = showNotification;
    }
}