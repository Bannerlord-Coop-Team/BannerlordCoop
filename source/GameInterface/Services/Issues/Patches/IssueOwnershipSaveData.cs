using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
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

internal sealed class IssueGenerationSaveData
{
    [SaveableField(1)]
    internal Hero IssueGiverHero;

    [SaveableField(2)]
    internal int Generation;

    private IssueGenerationSaveData()
    {
    }

    internal IssueGenerationSaveData(Hero issueGiverHero, int generation)
    {
        IssueGiverHero = issueGiverHero;
        Generation = generation;
    }
}

internal sealed class IssueJournalOwnershipSaveData
{
    [SaveableField(1)]
    internal JournalLogEntry Journal;

    [SaveableField(2)]
    internal string OwnerControllerId;

    private IssueJournalOwnershipSaveData()
    {
    }

    internal IssueJournalOwnershipSaveData(JournalLogEntry journal, string ownerControllerId)
    {
        Journal = journal;
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
        AddClassDefinition(typeof(IssueGenerationSaveData), 2);
        AddClassDefinition(typeof(IssueJournalOwnershipSaveData), 3);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<IssueOwnershipSaveData>));
        ConstructContainerDefinition(typeof(List<IssueGenerationSaveData>));
        ConstructContainerDefinition(typeof(List<IssueJournalOwnershipSaveData>));
    }
}
