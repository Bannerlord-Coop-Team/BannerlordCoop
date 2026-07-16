using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CampaignService.Messages;

public readonly struct UpdateCampaignOptions : IEvent { }

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateCampaignOptions : ICommand
{
    [ProtoMember(1)]
    public readonly bool AutoAllocateClanMemberPerks;

    [ProtoMember(2)]
    public readonly CampaignOptions.Difficulty PlayerTroopsReceivedDamage;

    [ProtoMember(3)]
    public readonly CampaignOptions.Difficulty RecruitmentDifficulty;

    [ProtoMember(4)]
    public readonly CampaignOptions.Difficulty PlayerMapMovementSpeed;

    [ProtoMember(5)]
    public readonly CampaignOptions.Difficulty StealthAndDisguiseDifficulty;

    [ProtoMember(6)]
    public readonly CampaignOptions.Difficulty CombatAIDifficulty;

    [ProtoMember(7)]
    public readonly bool IsLifeDeathCycleDisabled;

    [ProtoMember(8)]
    public readonly CampaignOptions.Difficulty PersuasionSuccessChance;

    [ProtoMember(9)]
    public readonly CampaignOptions.Difficulty ClanMemberDeathChance;

    [ProtoMember(10)]
    public readonly bool IsIronmanMode;

    [ProtoMember(11)]
    public readonly CampaignOptions.Difficulty BattleDeath;

    public NetworkUpdateCampaignOptions(
        bool autoAllocateClanMemberPerks,
        CampaignOptions.Difficulty playerTroopsReceivedDamage,
        CampaignOptions.Difficulty recruitmentDifficulty,
        CampaignOptions.Difficulty playerMapMovementSpeed,
        CampaignOptions.Difficulty stealthAndDisguiseDifficulty,
        CampaignOptions.Difficulty combatAIDifficulty,
        bool isLifeDeathCycleDisabled,
        CampaignOptions.Difficulty persuasianSuccessChance,
        CampaignOptions.Difficulty clanMemberDeathChance,
        bool isIronmanMode,
        CampaignOptions.Difficulty battleDeath)
    {
        AutoAllocateClanMemberPerks = autoAllocateClanMemberPerks;
        PlayerTroopsReceivedDamage = playerTroopsReceivedDamage;
        RecruitmentDifficulty = recruitmentDifficulty;
        PlayerMapMovementSpeed = playerMapMovementSpeed;
        StealthAndDisguiseDifficulty = stealthAndDisguiseDifficulty;
        CombatAIDifficulty = combatAIDifficulty;
        IsLifeDeathCycleDisabled = isLifeDeathCycleDisabled;
        PersuasionSuccessChance = persuasianSuccessChance;
        ClanMemberDeathChance = clanMemberDeathChance;
        IsIronmanMode = isIronmanMode;
        BattleDeath = battleDeath;
    }
}