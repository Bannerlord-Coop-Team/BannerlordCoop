using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyMoraleLossDueToFunds : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    [ProtoMember(2)]
    public readonly float MoraleChange;

    public NetworkNotifyMoraleLossDueToFunds(string mobilePartyId, float moraleChange)
    {
        MobilePartyId = mobilePartyId;
        MoraleChange = moraleChange;
    }
}
