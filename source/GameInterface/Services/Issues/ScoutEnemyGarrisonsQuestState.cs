using GameInterface.Services.Entity;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues;

using Quest = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;

internal interface IScoutEnemyGarrisonsQuestState
{
    void Remember(Quest quest, string controllerId, Hero hero, MobileParty party);
    bool TryGet(Quest quest, out ScoutEnemyGarrisonsQuestOwner owner);
    bool IsVisible(QuestBase quest);
    bool IsVisible(JournalLogEntry log);
    void Capture(Quest quest);
    void Restore(Quest quest);
    void SyncData(IDataStore dataStore);
}

internal sealed class ScoutEnemyGarrisonsQuestState : IScoutEnemyGarrisonsQuestState
{
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly Dictionary<string, ScoutEnemyGarrisonsQuestOwner> owners = new();

    public ScoutEnemyGarrisonsQuestState(IControllerIdProvider controllerIdProvider)
    {
        this.controllerIdProvider = controllerIdProvider;
    }

    public void Remember(Quest quest, string controllerId, Hero hero, MobileParty party)
    {
        owners[quest.StringId] = new ScoutEnemyGarrisonsQuestOwner(quest.StringId, controllerId, hero, party);
    }

    public bool TryGet(Quest quest, out ScoutEnemyGarrisonsQuestOwner owner)
    {
        owner = null;
        return quest?.StringId != null && owners.TryGetValue(quest.StringId, out owner);
    }

    public bool IsVisible(QuestBase quest) => quest is not Quest scout ||
        (TryGet(scout, out var owner) && owner.ControllerId == controllerIdProvider.ControllerId);

    public bool IsVisible(JournalLogEntry log)
    {
        foreach (var id in log._relatedObjectIds)
        {
            if (owners.TryGetValue(id, out var owner))
                return owner.ControllerId == controllerIdProvider.ControllerId;
        }
        return true;
    }

    public void Capture(Quest quest)
    {
        if (!TryGet(quest, out var owner)) return;
        owner.NeutralTargets = (quest._questSettlement1.IsCompletedThroughBeingNeutral ? 1 : 0) |
            (quest._questSettlement2.IsCompletedThroughBeingNeutral ? 2 : 0) |
            (quest._questSettlement3.IsCompletedThroughBeingNeutral ? 4 : 0);
    }

    public void Restore(Quest quest)
    {
        if (!TryGet(quest, out var owner)) return;
        quest._questSettlement1.IsCompletedThroughBeingNeutral = (owner.NeutralTargets & 1) != 0;
        quest._questSettlement2.IsCompletedThroughBeingNeutral = (owner.NeutralTargets & 2) != 0;
        quest._questSettlement3.IsCompletedThroughBeingNeutral = (owner.NeutralTargets & 4) != 0;
    }

    public void SyncData(IDataStore dataStore)
    {
        List<ScoutEnemyGarrisonsQuestOwner> saved = dataStore.IsSaving ? owners.Values.ToList() : null;
        dataStore.SyncData("_coop_scout_garrison_owners", ref saved);
        if (!dataStore.IsLoading) return;
        owners.Clear();
        foreach (var owner in saved ?? Enumerable.Empty<ScoutEnemyGarrisonsQuestOwner>())
        {
            if (!string.IsNullOrEmpty(owner?.QuestId)) owners[owner.QuestId] = owner;
        }
    }
}

internal sealed class ScoutEnemyGarrisonsQuestOwner
{
    [SaveableField(1)] internal string QuestId;
    [SaveableField(2)] internal string ControllerId;
    [SaveableField(3)] internal Hero Hero;
    [SaveableField(4)] internal MobileParty Party;
    [SaveableField(5)] internal int NeutralTargets;

    private ScoutEnemyGarrisonsQuestOwner() { }

    internal ScoutEnemyGarrisonsQuestOwner(string questId, string controllerId, Hero hero, MobileParty party)
    {
        QuestId = questId;
        ControllerId = controllerId;
        Hero = hero;
        Party = party;
    }
}

public sealed class ScoutEnemyGarrisonsSaveableTypeDefiner : SaveableTypeDefiner
{
    public ScoutEnemyGarrisonsSaveableTypeDefiner() : base(44_378_900) { }

    public override void DefineClassTypes() => AddClassDefinition(typeof(ScoutEnemyGarrisonsQuestOwner), 1);

    public override void DefineContainerDefinitions() => ConstructContainerDefinition(typeof(List<ScoutEnemyGarrisonsQuestOwner>));
}
