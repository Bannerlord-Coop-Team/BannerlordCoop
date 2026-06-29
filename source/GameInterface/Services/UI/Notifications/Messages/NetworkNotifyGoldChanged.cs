using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkNotifyGoldChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string GiverHeroId;

    [ProtoMember(2)]
    public readonly string GiverPartyId;

    [ProtoMember(3)]
    public readonly string RecipientHeroId;

    [ProtoMember(4)]
    public readonly string RecipientPartyId;

    [ProtoMember(5)]
    public readonly int GoldAmount;

    [ProtoMember(6)]
    public readonly bool ShowQuickInformation;

    public NetworkNotifyGoldChanged(
        string giverHeroId,
        string giverPartyId,
        string recipientHeroId,
        string recipientPartyId,
        int goldAmount,
        bool showQuickInformation)
    {
        GiverHeroId = giverHeroId;
        GiverPartyId = giverPartyId;
        RecipientHeroId = recipientHeroId;
        RecipientPartyId = recipientPartyId;
        GoldAmount = goldAmount;
        ShowQuickInformation = showQuickInformation;
    }
}