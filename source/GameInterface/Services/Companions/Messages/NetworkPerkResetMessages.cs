using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Companions.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkResetPerksByArenaMaster : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly int PerkResetCost;

    [ProtoMember(3)]
    public readonly string HeroForPerkResetId;

    [ProtoMember(4)]
    public readonly string SelectedSkillForResetId;

    public NetworkResetPerksByArenaMaster(
        string mainHeroId,
        int perkResetCost,
        string heroForPerkResetId,
        string selectedSkillForResetId)
    {
        MainHeroId = mainHeroId;
        PerkResetCost = perkResetCost;
        HeroForPerkResetId = heroForPerkResetId;
        SelectedSkillForResetId = selectedSkillForResetId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRemoveACompanionFromPlayerParty : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerClanId;

    public NetworkRemoveACompanionFromPlayerParty(string playerClanId)
    {
        PlayerClanId = playerClanId;
    }
}