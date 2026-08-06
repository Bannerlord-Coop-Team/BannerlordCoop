using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Patches;

internal sealed class IssueOwnershipSaveData
{
    [SaveableField(1)]
    internal Hero IssueGiverHero;

    [SaveableField(2)]
    internal string OwnerControllerId;

    private IssueOwnershipSaveData()
    {
    }

    internal IssueOwnershipSaveData(Hero issueGiverHero, string ownerControllerId)
    {
        IssueGiverHero = issueGiverHero;
        OwnerControllerId = ownerControllerId;
    }
}

public sealed class IssueOwnershipSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 44_183_000;

    public IssueOwnershipSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(IssueOwnershipSaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<IssueOwnershipSaveData>));
    }
}
