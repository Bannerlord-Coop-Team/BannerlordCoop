using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic;

public interface IQuestBespokeModule<TIssue, TQuest>
{
    void OnCreated(TIssue issue, object context);
    void OnAccepted(Hero owner, object context);
    void OnCompleted(Hero owner, object context);
    void OnSyncData(TaleWorlds.CampaignSystem.IDataStore dataStore);
    void OnGameLoaded(TQuest instance);
}

// No-op default base; netstandard2.0 doesn't support C# 8 default interface members.
public abstract class QuestBespokeModuleBase<TIssue, TQuest> : IQuestBespokeModule<TIssue, TQuest>
{
    public virtual void OnCreated(TIssue issue, object context)
    {
    }

    public virtual void OnAccepted(Hero owner, object context)
    {
    }

    public virtual void OnCompleted(Hero owner, object context)
    {
    }

    public virtual void OnSyncData(TaleWorlds.CampaignSystem.IDataStore dataStore)
    {
    }

    public virtual void OnGameLoaded(TQuest instance)
    {
    }
}
