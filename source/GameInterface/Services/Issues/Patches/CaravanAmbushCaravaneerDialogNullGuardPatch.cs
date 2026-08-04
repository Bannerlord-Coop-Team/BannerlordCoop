using Common.Logging;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Real, pre-existing, vanilla-adjacent bug found during this task's independent re-verification (not flagged
/// in the task's own "known facts", the same shape as this session's other independently-found vanilla-adjacent
/// bugs, e.g. ArtisanOverpricedGoodsAntagonistIdentityPatches): unlike Smugglers'
/// <c>GetSmugglerPartyDialog()</c>'s Condition (which explicitly null-checks <c>_smugglerParty != null &amp;&amp; ...</c>
/// first), <c>CaravanAmbushIssueQuest.GetCaravaneerDialogFlow()</c>'s "start"-state (priority 125) Condition does
/// NOT null-check <c>_caravanParty</c> before dereferencing it:
///
/// <code>
/// CharacterObject.OneToOneConversationCharacter == ConversationHelper.GetConversationCharacterPartyLeader(_caravanParty.Party) &amp;&amp; _isCaravanSaved
/// </code>
///
/// This dialogue is registered UNCONDITIONALLY, on EVERY peer's own mirror, from <c>SetDialogs()</c> in the
/// ctor (Category B) - and "start" is evaluated whenever ANY conversation begins with ANY character (it is the
/// entry state, not something gated to this quest's own NPCs). In single-player vanilla this is safe only
/// because Quest construction and <c>OnQuestAccepted()</c> (which first sets <c>_caravanParty</c>) happen
/// within ONE uninterrupted conversation with the quest giver - there is no window for the player to start a
/// DIFFERENT conversation while <c>_caravanParty</c> is still null. In coop, a non-owner peer's own mirror
/// Quest object is constructed OUT OF BAND (via the generic accept-mirror mechanism, reacting to a network
/// message, not to any live conversation of their own) - so <c>_caravanParty</c> stays null on that peer until
/// <see cref="CaravanAmbushPartySpawnGatePatch"/>'s force-write round-trip lands. During that window (and, more
/// narrowly, on the genuine accepter's own machine too, between object construction and the force-write - a
/// race this same window also covers for both accepter and mirrors), starting ANY conversation with ANY NPC
/// anywhere in the game would evaluate this Condition and throw a <see cref="System.NullReferenceException"/>.
///
/// Fix: since the Condition is a compiler-generated closure method (captures only <c>this</c>, so Roslyn
/// compiles it as a private instance method directly on <c>CaravanAmbushIssueQuest</c> rather than a separate
/// display class) with no stable source-level name to reference via <c>nameof</c>, it is located reflectively
/// by name-fragment at patch-apply time (same general technique already used for dynamically-discovered
/// targets elsewhere in this codebase, e.g. <c>DisableAllIssueBehaviorsExceptAllowlist</c>) rather than
/// hardcoded - and <see cref="TargetMethods"/> yields nothing (logs an error, patches nothing else) if it can't
/// be found, so a future game-version compiler-output change fails loud without breaking the rest of this mod.
/// </summary>
[HarmonyPatch]
internal class CaravanAmbushCaravaneerDialogNullGuardPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CaravanAmbushCaravaneerDialogNullGuardPatch>();

    private static readonly FieldInfo CaravanPartyField =
        AccessTools.Field(typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest), "_caravanParty");

    private static IEnumerable<MethodBase> TargetMethods()
    {
        var method = AccessTools.GetDeclaredMethods(typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest))
            .FirstOrDefault(m => m.Name.Contains("GetCaravaneerDialogFlow") && m.ReturnType == typeof(bool) && m.GetParameters().Length == 0);

        if (method == null)
        {
            Logger.Error("Could not find CaravanAmbushIssueQuest's compiler-generated GetCaravaneerDialogFlow " +
                "condition delegate to patch; the vanilla-adjacent _caravanParty null-deref bug (see this " +
                "patch's own doc comment) is UNGUARDED for this game build.");
            yield break;
        }

        yield return method;
    }

    private static bool Prefix(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest __instance, ref bool __result)
    {
        if (CaravanPartyField.GetValue(__instance) != null) return true;

        // _caravanParty is still null (see the type doc comment) - skip the real body, which would
        // unconditionally dereference it and crash this peer's very next unrelated conversation.
        __result = false;
        return false;
    }
}
