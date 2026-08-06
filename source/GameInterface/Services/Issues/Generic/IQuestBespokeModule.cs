using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic;

/// <summary>
/// Ground truth: <c>LordsNeedsTutorQuestFlagsPersistencePatches.cs</c> is the only shipped example of a registry
/// that needs both a <c>SyncData</c> piggyback and a per-Quest-instance rehydrate;
/// <c>VillageNeedsToolsIssueOwnershipPersistencePatches</c> does the identical shape for its own registry but
/// needs only the first hook - <c>IsLocalPeerOwner</c> always reads the static registry directly, nothing is
/// ever copied onto a Quest instance. This is why <see cref="OnGameLoaded"/> must be optional.
///
/// <typeparamref name="TIssue"/>/<typeparamref name="TQuest"/> let a migrated type's own bespoke lifecycle logic
/// (anything beyond the generic creation-capture/accept-mirror mechanisms) live in one place, hooked from the
/// generic runners at each lifecycle choke point, instead of scattered across ad-hoc independent Harmony
/// postfixes.
/// </summary>
public interface IQuestBespokeModule<TIssue, TQuest>
{
    void OnCreated(TIssue issue, object context);
    void OnAccepted(Hero owner, object context);
    void OnCompleted(Hero owner, object context);

    /// <summary>Piggybacks on the SAME per-behavior <c>SyncData</c> override every module needing persistence
    /// already has to patch individually.</summary>
    void OnSyncData(TaleWorlds.CampaignSystem.IDataStore dataStore);

    /// <summary>Optional in spirit: this project's <c>GameInterface</c> assembly targets
    /// <c>netstandard2.0</c>, which has no runtime support for C# 8 default interface implementations
    /// (<c>DefaultImplementationsOfInterfaces</c> requires netstandard2.1+), so "optional" is expressed by
    /// convention here instead - implementations that never copy registry state onto a live per-peer Quest
    /// instance should implement this as a no-op body, as <see cref="QuestBespokeModuleBase{TIssue,TQuest}"/>
    /// already does.</summary>
    void OnGameLoaded(TQuest instance);
}

/// <summary>Convenience base class giving every hook a no-op default (see
/// <see cref="IQuestBespokeModule{TIssue,TQuest}.OnGameLoaded"/> for why this exists instead of a C# 8 default
/// interface member) - a concrete module can derive from this and override only the hooks it actually
/// needs.</summary>
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
