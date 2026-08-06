using Common;
using GameInterface.Policies;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Blocks a client from directly originating a new issue (creation is server-authoritative; see
/// <see cref="IssuesCampaignBehaviorGenerationPatches"/> for why).
///
/// This is genuinely SHARED, type-agnostic infrastructure - confirmed by reading every other
/// <c>*IssueCreationPatch.cs</c> in this tree: this is the ONLY Harmony prefix on
/// <c>IssueManager.CreateNewIssue</c> anywhere in this codebase (every other type's own creation-capture file
/// carries only a type-checked Postfix that broadcasts a genuine creation's rolled terms). Every migrated AND
/// unmigrated quest type's own creation-replay path (e.g.
/// <see cref="Generic.CreationCapture.CreationCaptureRunner{TIssue,TFields}.ConstructAndRegisterReplicated"/>)
/// depends on this exact Prefix to let its own <c>AllowedThread</c>-wrapped replay through - do NOT delete
/// or scope this down when migrating any one type's own creation logic.
///
/// The former VillageNeedsTools-exclusive Postfix (captured + broadcast a genuine server-side
/// <c>VillageNeedsToolsIssue</c> creation) has been DELETED - that trigger is now
/// <see cref="Generic.Migrated.VillageNeedsTools.VillageNeedsToolsQuestType.OnGenuineCreation"/>, invoked ONLY
/// by the registry-driven <see cref="Generic.Dispatch.GenericQuestTypeDispatchPatches"/> once
/// <see cref="Generic.QuestTypeRegistry"/> confirms an issue instance is that registered type - see that
/// file's own doc comment for the full "what's shared vs. exclusive" derivation.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class IssueManagerCreateNewIssuePatches
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPrefix]
    private static bool Prefix()
    {
        // The replay path (Generic.CreationCapture.CreationCaptureRunner.ConstructAndRegisterReplicated, and
        // every pre-migration type's own equivalent) runs under AllowedThread on a client so its own
        // hand-built PotentialIssueData reaches the real method.
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }
}
