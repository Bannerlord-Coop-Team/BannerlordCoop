using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.CampaignService.Statistics.Messages;

/// <summary>
/// Kills a client counted during a mission
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkMissionStatistics : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly int TroopsKnockedOrKilledByPlayer;

    [ProtoMember(3)]
    public readonly int TroopsKnockedOrKilledAsParty;

    [ProtoMember(4)]
    public readonly int HeroesKilledInBattle;

    [ProtoMember(5)]
    public readonly string[] CompanionIds;

    [ProtoMember(6)]
    public readonly int[] CompanionKills;

    public NetworkMissionStatistics(
        string playerHeroId,
        int troopsKnockedOrKilledByPlayer,
        int troopsKnockedOrKilledAsParty,
        int heroesKilledInBattle,
        string[] companionIds,
        int[] companionKills)
    {
        PlayerHeroId = playerHeroId;
        TroopsKnockedOrKilledByPlayer = troopsKnockedOrKilledByPlayer;
        TroopsKnockedOrKilledAsParty = troopsKnockedOrKilledAsParty;
        HeroesKilledInBattle = heroesKilledInBattle;
        CompanionIds = companionIds;
        CompanionKills = companionKills;
    }
}
