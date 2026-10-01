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

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class DisableAllIssueBehaviorsExceptAllowlist
{
    private static readonly ILogger Logger = LogManager.GetLogger<DisableAllIssueBehaviorsExceptAllowlist>();

    private static readonly Harmony DynamicHarmony = new Harmony("GameInterface.Issues.DisableAllIssueBehaviorsExceptAllowlist");

    private static readonly string[] ScannedAssemblyNames = { "TaleWorlds.CampaignSystem", "SandBox" };

    internal static readonly HashSet<Type> Allowlist = new HashSet<Type>
    {
        typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior),
    };

    internal static bool IsAllowlisted(IssueBase issue)
    {
        return issue != null && issue.GetType().DeclaringType is Type declaringType && Allowlist.Contains(declaringType);
    }

    private static bool applied;

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

        VerifyAllowlistIntegrity();
    }

    private static void VerifyAllowlistIntegrity()
    {
        var offenders = new List<string>();

        foreach (var type in Allowlist)
        {
            var method = AccessTools.DeclaredMethod(type, nameof(CampaignBehaviorBase.RegisterEvents));
            if (method == null)
            {
                Logger.Warning("Allowlist integrity check: could not find a declared RegisterEvents on " +
                    "allowlisted type {Type}; cannot verify it isn't being blocked by an orphaned patch", type.FullName);
                continue;
            }

            var info = Harmony.GetPatchInfo(method);
            if (info?.Prefixes == null || info.Prefixes.Count == 0) continue;

            var owners = string.Join("; ", info.Prefixes.Select(p =>
                $"owner='{p.owner}' method={p.PatchMethod.DeclaringType?.FullName}.{p.PatchMethod.Name}"));
            offenders.Add($"{type.FullName} [{owners}]");
        }

        if (offenders.Count == 0) return;

        Logger.Error(
            "!!!!! ISSUES ALLOWLIST INTEGRITY FAILURE !!!!! {Count} allowlisted issue behavior type(s) still " +
            "have a RegisterEvents prefix patch applied, meaning they can NEVER be offered to any hero despite " +
            "being correctly allowlisted here. This is the exact orphaned-disable-patch bug already fixed 3 " +
            "times (commits e96018702, 479f810e7, and the 12-type sweep that followed it): a leftover " +
            "standalone 'Disable<Type>IssueBehavior' Harmony patch class elsewhere in the codebase, predating " +
            "this allowlist, unconditionally no-ops RegisterEvents. Find and DELETE the offending patch " +
            "class(es) - do not add any CallOriginalPolicy/category gate to them, they should not exist at " +
            "all now that the type is allowlisted. Affected: {Offenders}",
            offenders.Count, string.Join(" | ", offenders));
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

[HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_hero_main_options_have_issue_on_condition")]
internal class GateIssueDialogueEntryOptionByAllowlist
{
    private static void Postfix(ref bool __result)
    {
        if (!__result) return;
        __result = DisableAllIssueBehaviorsExceptAllowlist.IsAllowlisted(Hero.OneToOneConversationHero?.Issue);
    }
}

[HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_lord_task_given_on_condition")]
internal class GateIssueDialogueTaskGivenOptionByAllowlist
{
    private static void Postfix(ref bool __result)
    {
        if (!__result) return;
        __result = DisableAllIssueBehaviorsExceptAllowlist.IsAllowlisted(Hero.OneToOneConversationHero?.Issue);
    }
}

[HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_lord_task_given_alternative_on_condition")]
internal class GateIssueDialogueTaskGivenAlternativeOptionByAllowlist
{
    private static void Postfix(ref bool __result)
    {
        if (!__result) return;
        __result = DisableAllIssueBehaviorsExceptAllowlist.IsAllowlisted(Hero.OneToOneConversationHero?.Issue);
    }
}
