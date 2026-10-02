using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Patches;

internal sealed class IssueOwnershipSaveData
{
    [SaveableField(1)]
    internal Hero IssueGiverHero;

    [SaveableField(2)]
    internal string OwnerControllerId;

    [SaveableField(3)]
    internal MobileParty WeaponsGuardsParty;

    [SaveableField(4)]
    internal bool WeaponsBattlePending;

    [SaveableField(5)]
    internal string WeaponsQuestId;

    private IssueOwnershipSaveData()
    {
    }

    internal IssueOwnershipSaveData(Hero issueGiverHero, string ownerControllerId)
    {
        IssueGiverHero = issueGiverHero;
        OwnerControllerId = ownerControllerId;
        if (issueGiverHero.Issue?.IssueQuest is GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest quest)
        {
            WeaponsGuardsParty = quest._guardsParty;
            WeaponsBattlePending = quest._checkForBattleResult;
            WeaponsQuestId = quest.StringId;
        }
    }

    internal void RestoreQuestReferences()
    {
        if (IssueGiverHero?.Issue?.IssueQuest is not GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest quest ||
            !quest.IsOngoing || quest.StringId != WeaponsQuestId) return;
        quest._guardsParty = WeaponsGuardsParty;
        quest._checkForBattleResult = WeaponsBattlePending;
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
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<IssueOwnershipSaveData>));
        ConstructContainerDefinition(typeof(List<IssueGenerationSaveData>));
    }
}
