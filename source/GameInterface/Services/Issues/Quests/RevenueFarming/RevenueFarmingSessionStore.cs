using GameInterface.Services.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

using Quest = RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest;

internal interface IRevenueFarmingSessionStore
{
    RevenueFarmingSession Get(Quest quest);
    RevenueFarmingSession Get(IssueBase issue);
    RevenueFarmingSession Get(Hero questGiver, string questId);
    RevenueFarmingSession Register(Quest quest, Hero playerHero, string controllerId);
    Quest FindOngoing(Hero playerHero);
    Quest LocalQuest { get; }
    bool IsLocal(Quest quest);
    void SyncData(IDataStore dataStore);
}

internal sealed class RevenueFarmingSessionStore : IRevenueFarmingSessionStore
{
    private List<RevenueFarmingSession> sessions = new();
    private readonly IControllerIdProvider controller;

    public RevenueFarmingSessionStore(IControllerIdProvider controller)
    {
        this.controller = controller;
    }

    public Quest LocalQuest => sessions.FirstOrDefault(session => IsActive(session) && IsLocal(session.Quest))?.Quest;

    public bool IsLocal(Quest quest) => Get(quest) is { } session &&
        session.ControllerId == controller.ControllerId;

    public RevenueFarmingSession Get(Quest quest) => sessions.FirstOrDefault(session => session.Quest == quest);

    public RevenueFarmingSession Get(IssueBase issue) => sessions.FirstOrDefault(session =>
        session.Quest.QuestGiver == issue.IssueOwner && session.IssueId == issue.StringId);

    public RevenueFarmingSession Get(Hero questGiver, string questId) => sessions.FirstOrDefault(session =>
        session.Quest.QuestGiver == questGiver && session.Quest.StringId == questId);

    public Quest FindOngoing(Hero playerHero) => sessions.FirstOrDefault(session =>
        session.PlayerHero == playerHero && IsActive(session))?.Quest;

    private static bool IsActive(RevenueFarmingSession session) => session.Quest.IsOngoing &&
        session.Quest.QuestGiver?.Issue?.IssueQuest == session.Quest;

    public RevenueFarmingSession Register(Quest quest, Hero playerHero, string controllerId)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));
        if (playerHero == null) throw new ArgumentNullException(nameof(playerHero));
        if (string.IsNullOrEmpty(controllerId)) throw new ArgumentException("Revenue Farming owner is not registered.", nameof(controllerId));
        var session = Get(quest);
        if (session != null)
        {
            if (session.PlayerHero != playerHero || session.ControllerId != controllerId)
                throw new InvalidOperationException("Revenue Farming quest already has another owner.");
            if (session.IssueId == null) session.IssueId = quest.QuestGiver?.Issue?.StringId;
            return session;
        }

        session = new RevenueFarmingSession(quest, playerHero, controllerId, quest.QuestGiver?.Issue?.StringId);
        sessions.Add(session);
        return session;
    }

    public void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("_coop_revenue_farming_sessions", ref sessions);
        if (dataStore.IsLoading)
            sessions = sessions?.Where(session => session?.Quest != null && session.PlayerHero != null).ToList() ?? new();
    }
}

internal sealed class RevenueFarmingSession
{
    [SaveableField(1)] internal Quest Quest;
    [SaveableField(2)] internal Hero PlayerHero;
    [SaveableField(3)] internal string Incident;
    [SaveableField(4)] internal bool AwaitingPayment;
    [SaveableField(5)] internal int Revision;
    [SaveableField(6)] internal Settlement CollectionSettlement;
    [SaveableField(7)] internal MobileParty PlayerParty;
    [SaveableField(8)] internal string ControllerId;
    [SaveableField(9)] internal string IssueId;

    private RevenueFarmingSession() { }

    internal RevenueFarmingSession(Quest quest, Hero playerHero, string controllerId, string issueId = null)
    {
        Quest = quest;
        PlayerHero = playerHero;
        PlayerParty = playerHero.PartyBelongedTo;
        ControllerId = controllerId;
        IssueId = issueId;
    }
}

public sealed class RevenueFarmingSaveableTypeDefiner : SaveableTypeDefiner
{
    public RevenueFarmingSaveableTypeDefiner() : base(44_378_800) { }

    public override void DefineClassTypes() => AddClassDefinition(typeof(RevenueFarmingSession), 1);

    public override void DefineContainerDefinitions() => ConstructContainerDefinition(typeof(List<RevenueFarmingSession>));
}
