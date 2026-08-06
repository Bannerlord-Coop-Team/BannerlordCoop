using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Save-system representation of one Village Needs Tools issue's recorded owning ControllerId (see
/// <see cref="Interfaces.VillageNeedsToolsIssueOwnership"/>). This must survive save/reload since a quest or
/// alternative solution can run for real days/weeks of in-game time.
/// </summary>
internal sealed class VillageNeedsToolsIssueOwnershipSaveData
{
    [SaveableField(1)]
    internal Hero IssueGiverHero;

    [SaveableField(2)]
    internal string OwnerControllerId;

    private VillageNeedsToolsIssueOwnershipSaveData()
    {
    }

    internal VillageNeedsToolsIssueOwnershipSaveData(Hero issueGiverHero, string ownerControllerId)
    {
        IssueGiverHero = issueGiverHero;
        OwnerControllerId = ownerControllerId;
    }
}

/// <summary>
/// Registers the Village Needs Tools issue-ownership save records with Bannerlord's save system. Base id
/// picked to not collide with the two other registered definers in this project
/// (<c>SiegeAftermathSaveableTypeDefiner</c> = 44_177_000, <c>PlayerCaptivityAttackProtectionSaveableTypeDefiner</c>
/// = 44_182_000).
/// </summary>
public sealed class VillageNeedsToolsIssueOwnershipSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 44_183_000;

    public VillageNeedsToolsIssueOwnershipSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(VillageNeedsToolsIssueOwnershipSaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<VillageNeedsToolsIssueOwnershipSaveData>));
    }
}
