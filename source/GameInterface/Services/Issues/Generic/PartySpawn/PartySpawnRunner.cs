using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic.PartySpawn;

public static class PartySpawnRunner
{
    public static bool AlreadySpawned<TQuest, TCapture, TSpawned>(PartySpawnSpec<TQuest, TCapture, TSpawned> spec, TQuest quest)
        where TQuest : QuestBase
        => quest != null && (spec?.AlreadySpawnedCheck?.Invoke(quest) ?? false);

    public static bool TryForwardSpawnRequest<TQuest, TCapture, TSpawned>(PartySpawnSpec<TQuest, TCapture, TSpawned> spec, Hero owner, TQuest quest)
        where TQuest : QuestBase
    {
        if (spec?.TryCaptureForForwarding == null || spec.ForwardSpawnRequest == null || quest == null || owner == null) return false;
        if (!spec.TryCaptureForForwarding(quest, out var captured)) return false;

        spec.ForwardSpawnRequest(owner, captured);
        return true;
    }

    public static TSpawned CreateOnServer<TQuest, TCapture, TSpawned>(PartySpawnSpec<TQuest, TCapture, TSpawned> spec, Hero owner, TCapture captured)
        where TQuest : QuestBase
        => spec != null && spec.CreateOnServer != null ? spec.CreateOnServer(owner, captured) : default;

    public static void ApplySpawnResult<TQuest, TCapture, TSpawned>(PartySpawnSpec<TQuest, TCapture, TSpawned> spec, TQuest quest, TSpawned spawned)
        where TQuest : QuestBase
        => spec?.ApplySpawnResult?.Invoke(quest, spawned);
}
