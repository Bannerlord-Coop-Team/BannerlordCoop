using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Replaces the old blanket "disable the whole vanilla Issue/Quest system" patches
/// (<c>IssueManagerDisablePatches</c>, <c>DisableIssuesCampaignBehavior</c>,
/// <c>DisableLordConversationIssueDialogs</c> - all deleted) with a table-driven allowlist: every
/// individual issue-type <see cref="CampaignBehaviorBase"/> (every "*IssueBehavior"-shaped class, detected
/// generically as any concrete <see cref="CampaignBehaviorBase"/> subclass with a nested type deriving
/// <see cref="IssueBase"/>, across the <c>TaleWorlds.CampaignSystem</c> and <c>SandBox</c> assemblies) has
/// its own <c>RegisterEvents</c> forced to a no-op, EXCEPT the types in <see cref="Allowlist"/>.
///
/// Why this is safe (see the design doc/commit message for the full derivation): each issue-type behavior's
/// own RegisterEvents only ever does <c>CampaignEvents.OnCheckForIssueEvent.AddNonSerializedListener</c>,
/// which nominates itself as a candidate via <c>IssueManager.AddPotentialIssueData</c>. Nothing fires
/// OnCheckForIssueEvent except <c>IssueManager.CheckForIssues</c>, itself only ever called from
/// <c>IssuesCampaignBehavior</c> (whose own RegisterEvents is intentionally NOT touched here - it is safe to
/// run now that it can only ever pick a nominee from the allowlisted type). So with every issue-type
/// behavior except the allowlisted ones gated off, <c>IssueManager.Issues</c> can only ever contain
/// instances of an allowlisted type, and the three previously-disabled LordConversationsCampaignBehavior
/// dialogue-gate conditions (which just check vanilla `hero.Issue != null` state, nothing type-specific) are
/// naturally safe without needing their own patch anymore.
/// </summary>
[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class DisableAllIssueBehaviorsExceptAllowlist
{
    private static readonly ILogger Logger = LogManager.GetLogger<DisableAllIssueBehaviorsExceptAllowlist>();

    // A dedicated Harmony instance for the dynamically-discovered per-type patches below - these targets
    // aren't known at compile time, so they can't be declared with the usual [HarmonyPatch(typeof(...))]
    // attribute; the class-level attribute above is only used to piggyback on IssuesCampaignBehavior's own
    // RegisterEvents (patched with a real [HarmonyPatch] below) as a "run this once, early" hook.
    private static readonly Harmony DynamicHarmony = new Harmony("GameInterface.Issues.DisableAllIssueBehaviorsExceptAllowlist");

    private static readonly string[] ScannedAssemblyNames = { "TaleWorlds.CampaignSystem", "SandBox" };

    /// <summary>
    /// Issue-type behaviors vetted and allowed to actually generate/offer their issue in co-op. Extend this
    /// as more issue types get the same sync + audit treatment as VillageNeedsToolsIssueBehavior.
    ///
    /// Made internal (was private) and read via <see cref="IsAllowlisted"/> so the other shared choke points
    /// (<see cref="IssueFinalizedPatches"/>, <see cref="IssueManagerQuestCompletedReasonCapture"/>) can check
    /// "is this a synced issue type at all" generically against ISSUE (not behavior) types, instead of each
    /// growing its own hand-maintained <c>is (A or B or C or ...)</c> pattern match every time a new type is
    /// added here.
    /// </summary>
    internal static readonly HashSet<Type> Allowlist = new HashSet<Type>
    {
        typeof(VillageNeedsToolsIssueBehavior),
        typeof(VillageNeedsCraftingMaterialsIssueBehavior),
        typeof(LordNeedsHorsesIssueBehavior),
        typeof(CapturedByBountyHuntersIssueBehavior),
        typeof(ArmyNeedsSuppliesIssueBehavior),
        typeof(LandlordTrainingForRetainersIssueBehavior),
        typeof(GangLeaderNeedsRecruitsIssueBehavior),
        typeof(LadysKnightOutIssueBehavior),
        typeof(ScoutEnemyGarrisonsIssueBehavior),
    };

    /// <summary>
    /// True if <paramref name="issue"/>'s concrete type is the nested Issue type of one of the
    /// <see cref="Allowlist"/> behaviors. Used by <see cref="IssueFinalizedPatches"/> and
    /// <see cref="IssueManagerQuestCompletedReasonCapture"/> to recognize any currently-synced issue type
    /// generically - safe because <see cref="Allowlist"/> already guarantees no OTHER issue type can ever be
    /// created in the first place (see this type's own doc comment), so any <see cref="IssueBase"/> that
    /// exists at all is either one of these, or an untouched pre-existing one from an old save (harmlessly
    /// excluded here the same way it always was).
    /// </summary>
    internal static bool IsAllowlisted(IssueBase issue)
    {
        return issue != null && issue.GetType().DeclaringType is Type declaringType && Allowlist.Contains(declaringType);
    }

    private static bool applied;

    // Runs once, the first time any IssuesCampaignBehavior instance registers its events (both on the
    // client and on the server, each the first time a campaign session starts in that process) - a natural,
    // already-existing entry point, so no changes to ServiceModule/GameInterface's boot sequence are needed.
    [HarmonyPatch(nameof(IssuesCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    private static void RegisterEventsPrefix()
    {
        if (applied) return;
        applied = true;

        try
        {
            ApplyAllowlist();
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to apply the Issues allowlist patch; ALL issue-type behaviors " +
                "(including the vetted allowlist) may remain active and unpatched.");
        }
    }

    private static void ApplyAllowlist()
    {
        var prefix = new HarmonyMethod(typeof(DisableAllIssueBehaviorsExceptAllowlist), nameof(DisablePrefix));

        var disabled = new List<string>();
        var skippedAllowlisted = new List<string>();

        foreach (var type in FindIssueBehaviorTypes())
        {
            if (Allowlist.Contains(type))
            {
                skippedAllowlisted.Add(type.FullName);
                continue;
            }

            var method = AccessTools.DeclaredMethod(type, nameof(CampaignBehaviorBase.RegisterEvents));
            if (method == null)
            {
                Logger.Warning("Could not find a declared RegisterEvents on issue behavior {Type}; " +
                    "leaving it unpatched", type.FullName);
                continue;
            }

            DynamicHarmony.Patch(method, prefix: prefix);
            disabled.Add(type.FullName);
        }

        Logger.Information(
            "Issues allowlist applied: disabled RegisterEvents on {DisabledCount} vanilla issue behavior(s), " +
            "left {AllowedCount} allowlisted active: {Allowed}. Disabled: {Disabled}",
            disabled.Count, skippedAllowlisted.Count, string.Join(", ", skippedAllowlisted), string.Join(", ", disabled));
    }

    private static bool DisablePrefix() => false;

    private static IEnumerable<Type> FindIssueBehaviorTypes()
    {
        foreach (var assemblyName in ScannedAssemblyNames)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (assembly == null)
            {
                Logger.Warning("Assembly {AssemblyName} not loaded; skipping it when scanning for issue behaviors", assemblyName);
                continue;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            foreach (var type in types)
            {
                if (type == null || type.IsAbstract || !type.IsClass) continue;
                if (!typeof(CampaignBehaviorBase).IsAssignableFrom(type)) continue;
                if (!HasNestedIssueType(type)) continue;

                yield return type;
            }
        }
    }

    private static bool HasNestedIssueType(Type behaviorType)
    {
        foreach (var nested in behaviorType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (typeof(IssueBase).IsAssignableFrom(nested)) return true;
        }
        return false;
    }
}
