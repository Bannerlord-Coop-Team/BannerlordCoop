using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.PlayerCaptivityService.Patches;

/// <summary>
/// Save-system representation of one former-captor attack protection for a released player party.
/// </summary>
internal sealed class PlayerCaptivityAttackProtectionSaveData
{
    [SaveableField(1)]
    internal MobileParty AttackerParty;

    [SaveableField(2)]
    internal MobileParty TargetParty;

    [SaveableField(3)]
    internal CampaignTime DisabledUntil;

    [SaveableField(4)]
    internal Kingdom TargetKingdom;

    [SaveableField(5)]
    internal Clan TargetClan;

    internal IFaction TargetFaction => (IFaction)TargetKingdom ?? TargetClan;
    private PlayerCaptivityAttackProtectionSaveData()
    {
    }

    internal PlayerCaptivityAttackProtectionSaveData(
        MobileParty attackerParty,
        MobileParty targetParty,
        CampaignTime disabledUntil)
    {
        AttackerParty = attackerParty;
        TargetParty = targetParty;
        DisabledUntil = disabledUntil;
    }

    internal PlayerCaptivityAttackProtectionSaveData(
        MobileParty attackerParty,
        IFaction targetFaction,
        CampaignTime disabledUntil)
    {
        AttackerParty = attackerParty;
        TargetKingdom = targetFaction as Kingdom;
        TargetClan = targetFaction as Clan;
        DisabledUntil = disabledUntil;
    }
}

/// <summary>
/// Registers co-op player-captivity attack-protection records with Bannerlord's save system.
/// </summary>
public sealed class PlayerCaptivityAttackProtectionSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 44_182_000;

    public PlayerCaptivityAttackProtectionSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(PlayerCaptivityAttackProtectionSaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<PlayerCaptivityAttackProtectionSaveData>));
    }
}
