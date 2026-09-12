using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Actions;
namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkOnClanChangedKingdom : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;
    [ProtoMember(2)]
    public readonly string OldKingdomId;
    [ProtoMember(3)]
    public readonly string NewKingdomId;
    [ProtoMember(4)]
    public readonly ChangeKingdomAction.ChangeKingdomActionDetail Detail;

    public NetworkOnClanChangedKingdom(string clanId, string oldKingdomId, string newKingdomId, ChangeKingdomAction.ChangeKingdomActionDetail detail)
    {
        ClanId = clanId;
        OldKingdomId = oldKingdomId;
        NewKingdomId = newKingdomId;
        Detail = detail;
    }
}
