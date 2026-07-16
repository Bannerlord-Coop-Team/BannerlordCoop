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
