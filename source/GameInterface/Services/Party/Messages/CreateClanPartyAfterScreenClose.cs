using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;

namespace GameInterface.Services.Party.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct CreateClanPartyAfterScreenClose : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string NewLeaderHeroId;

    [ProtoMember(3)]
    public readonly TroopRosterData LeftMemberRosterData;

    [ProtoMember(4)]
    public readonly TroopRosterData LeftPrisonRosterData;

    public CreateClanPartyAfterScreenClose(
        string mainHeroId,
        string newLeaderHeroId,
        TroopRosterData leftMemberRosterData,
        TroopRosterData leftPrisonRosterData)
    {
        MainHeroId = mainHeroId;
        NewLeaderHeroId = newLeaderHeroId;
        LeftMemberRosterData = leftMemberRosterData;
        LeftPrisonRosterData = leftPrisonRosterData;
    }
}