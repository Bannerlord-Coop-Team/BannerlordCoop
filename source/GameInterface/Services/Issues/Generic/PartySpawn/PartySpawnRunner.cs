using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic.PartySpawn;

/// <summary>
/// Thin driver over <see cref="PartySpawnSpec{TQuest,TCapture,TSpawned}"/> - the shared coordination shape
/// behind every per-type <see cref="PartySpawnTrigger.SpawnMethodWrap"/> Prefix/Postfix pair. Each instance's
/// own concrete <c>[HarmonyPatch]</c>/<c>[HarmonyPrefix]</c>/<c>[HarmonyPostfix]</c> shell still does the actual
/// Harmony attachment (Harmony patches must be concrete static methods with the patched method's own fixed
/// signature - the same constraint <see cref="Gates.GateAndInjectDescriptor{TQuest}"/> is built around); this
/// class only factors out the capture/forward/create/force-write calls those shells make, instead of each one
/// duplicating the same 4-step dance by hand.
///
/// <c>TSpawned</c> (added alongside CaravanAmbush/SnareTheWealthy's migration - see
/// <see cref="PartySpawnSpec{TQuest,TCapture,TSpawned}"/>'s own doc comment for the full derivation): generalizes
/// <c>CreateOnServer</c>/<c>ApplySpawnResult</c> from a hardcoded single <see cref="MobileParty"/> to whatever
/// shape a given instance's real spawn method actually force-writes - a bare type-parameter widen for the 3
/// existing single-party consumers (<c>TSpawned = MobileParty</c>), a 2-tuple for the two 2-party consumers.
/// </summary>
public static class PartySpawnRunner
{
    public static bool AlreadySpawned<TQuest, TCapture, TSpawned>(PartySpawnSpec<TQuest, TCapture, TSpawned> spec, TQuest quest)
        where TQuest : QuestBase
        => quest != null && (spec?.AlreadySpawnedCheck?.Invoke(quest) ?? false);

    /// <summary>[Client/triggering peer] Captures the forwarding payload (if any) and publishes it via the
    /// spec's own <c>ForwardSpawnRequest</c> delegate. Returns false (nothing published) if capture fails - the
    /// real Harmony Prefix still unconditionally skips the broken local body either way (see
    /// <see cref="TryCaptureForForwarding{TQuest,TCapture}"/>'s own doc comment), this return value only tells
    /// the caller whether a request actually went out, for logging/test purposes.</summary>
    public static bool TryForwardSpawnRequest<TQuest, TCapture, TSpawned>(PartySpawnSpec<TQuest, TCapture, TSpawned> spec, Hero owner, TQuest quest)
        where TQuest : QuestBase
    {
        if (spec?.TryCaptureForForwarding == null || spec.ForwardSpawnRequest == null || quest == null || owner == null) return false;
        if (!spec.TryCaptureForForwarding(quest, out var captured)) return false;

        spec.ForwardSpawnRequest(owner, captured);
        return true;
    }

    /// <summary>[Server only] The real, parameterized reimplementation of the vanilla spawn method's body,
    /// substituting <paramref name="captured"/> for whatever the real body's own ambient reads would have
    /// produced. Also force-writes the result onto the creating peer's own quest instance (see
    /// <see cref="PartySpawnSpec{TQuest,TCapture,TSpawned}"/>'s own doc comment on <c>CreateOnServer</c>).</summary>
    public static TSpawned CreateOnServer<TQuest, TCapture, TSpawned>(PartySpawnSpec<TQuest, TCapture, TSpawned> spec, Hero owner, TCapture captured)
        where TQuest : QuestBase
        => spec != null && spec.CreateOnServer != null ? spec.CreateOnServer(owner, captured) : default;

    /// <summary>Force-writes the genuinely-created party/parties (received via network broadcast) onto THIS
    /// peer's own quest instance.</summary>
    public static void ApplySpawnResult<TQuest, TCapture, TSpawned>(PartySpawnSpec<TQuest, TCapture, TSpawned> spec, TQuest quest, TSpawned spawned)
        where TQuest : QuestBase
        => spec?.ApplySpawnResult?.Invoke(quest, spawned);
}
