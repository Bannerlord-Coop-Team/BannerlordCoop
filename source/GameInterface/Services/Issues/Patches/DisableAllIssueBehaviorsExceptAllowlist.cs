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
    /// Issue-type behaviors vetted and allowed to actually generate/offer their issue in co-op. This is the
    /// "trade quests" subset extracted from the full village-needs-tools-sync branch - see that branch's own
    /// Allowlist for the complete (trade + non-trade) set.
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
        typeof(ArmyNeedsSuppliesIssueBehavior),
        // Village Needs Grain Seeds - TaleWorlds.CampaignSystem.dll. Its behavior-singleton grain-price cache is
        // a separate, standalone mechanism (HeadmanNeedsGrainPriceCachePatches/HeadmanNeedsGrainPricePersistencePatches),
        // not part of this allowlist/dispatch choke point at all.
        typeof(HeadmanNeedsGrainIssueBehavior),
        typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior),
        typeof(GangLeaderNeedsSpecialWeaponsIssueBehavior),
        typeof(LordNeedsHorsesIssueBehavior),
        typeof(LandLordTheArtOfTheTradeIssueBehavior),
        // Deliver the Herd to Town - TaleWorlds.CampaignSystem.dll. Its own dynamically-registered second
        // DialogFlow (GetDeliveryDialogFlow, added imperatively outside SetDialogs()/AddDialogs()) is a
        // separate, standalone ownership-gate mechanism (HeadmanNeedsToDeliverAHerdOwnershipGatePatches), not
        // part of this allowlist/dispatch choke point at all - see that patch's own doc comment. A leftover
        // orphaned DisableHeadmanNeedsToDeliverAHerdIssueBehavior patch (predating this allowlist) was found
        // blocking this type's RegisterEvents entirely and has been deleted - see VerifyAllowlistIntegrity's
        // own doc comment.
        typeof(HeadmanNeedsToDeliverAHerdIssueBehavior),
        // Artisan Can't Sell Products At A Fair Price - TaleWorlds.CampaignSystem.dll. Its own
        // dynamically-registered SECOND and THIRD DialogFlows (GetDeliveryDialogFlow/GetCounterOfferDialogFlow,
        // both added imperatively outside SetDialogs()/AddDialogs()) are a separate, standalone ownership-gate
        // mechanism (ArtisanCantSellProductsAtAFairPriceOwnershipGatePatches), not part of this
        // allowlist/dispatch choke point at all - see that patch's own doc comment. A leftover orphaned
        // DisableArtisanCantSellProductsAtAFairPriceIssueBehavior patch (predating this allowlist) was found
        // blocking this type's RegisterEvents entirely and has been deleted, same bug shape as above.
        typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior),
        // Gang Leader Needs to Offload Stolen Goods - TaleWorlds.CampaignSystem.dll. Second location is a
        // hideout (_issueHideout/_questHideout), with hideout-battle wiring and IsSettlementBusy priority-
        // locking - see GangLeaderNeedsToOffloadStolenGoodsOwnershipGatePatches's doc comment for the full
        // ownership-gate derivation, and IGangLeaderNeedsToOffloadStolenGoodsIssueInterface's doc comment for
        // its bespoke accept-time price-capture mechanism (this type is NOT in GenericAcceptMirrorIssueTypes'
        // QuestSolutionMirrorEligible set - its required-amount/price/reward are re-derived per-client at
        // accept time, same shape as Village Needs Crafting Materials). A leftover orphaned
        // DisableGangLeaderNeedsToOffloadStolenGoodsIssueBehavior patch (predating this allowlist) was found
        // blocking this type's RegisterEvents entirely and has been deleted, same bug shape as above.
        typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior),
        // Smugglers - single hostile MobileParty, no scripted-battle-pipeline gap, no bespoke capture-once
        // redirect complexity. No orphaned standalone disable patch was ever found for this type (grepped the
        // whole tree - zero hits), so this is the only structural change it needs. Its own bespoke
        // Interfaces/Messages/Patches/Handler set (source/GameInterface/Services/Issues/*/Smugglers*.cs)
        // covers creation-time target/origin-settlement capture, a party-spawn gate (a real, independently-
        // verified client-authority gap in CustomPartyComponent.CreateCustomPartyWithTroopRoster - see
        // SmugglersPartySpawnGatePatch's doc comment), and an ownership gate on the bribe/persuasion turn-in
        // path (SmugglersQuestOwnershipGatePatch) - the accept-quest/alternative-solution flows themselves
        // ride the fully generic GenericAcceptMirrorIssueTypes mechanism unchanged.
        typeof(SmugglersIssueBehavior),
        // Artisan Overpriced Goods, TaleWorlds.CampaignSystem.dll. This pass independently found a real,
        // pre-existing, vanilla-adjacent bug: ArtisanOverpricedGoodsIssueQuest's ctor receives counterOfferHero
        // but never stores it - every access point instead re-derives a private AntagonistHero property live via
        // an order-dependent Notables.FirstOrDefault missing the CanHaveCampaignIssues() filter the Issue's own
        // GetAntagonistMerchant applies at creation time (can silently resolve to a DIFFERENT hero, or null -
        // AcceptCounterOffer has zero null-guard). Fixed via a getter-redirect Harmony patch - see
        // Patches.ArtisanOverpricedGoodsAntagonistIdentityPatches's doc comment for the full derivation. Also
        // independently found a turn-in ownership gap on BOTH the delivery dialogue (live on every mirror peer
        // via SetDialogs(), called from the ctor itself) AND a chained-external-Consequence gap where gating
        // DeliverItemsFullyOnConsequence alone is not enough to stop QuestBase.CompleteQuestWithSuccess from
        // still firing - see Patches.ArtisanOverpricedGoodsOwnershipGatePatches's doc comment. No orphaned
        // standalone disable patch was found for this type (grepped the whole tree - zero hits, same as
        // Smugglers), so this allowlist entry is this type's only structural change.
        typeof(ArtisanOverpricedGoodsIssueBehavior),
        // Revenue Farming, TaleWorlds.CampaignSystem.dll. No spawned MobileParty anywhere in this behavior at
        // all (grepped the whole decompiled source for party-creation APIs - zero hits). Independently
        // confirmed a turn-in ownership gap on BOTH real turn-in surfaces (DiscussDialogFlow's inline delegate
        // AND the steward game-menu option, both funneling into the same two private
        // QuestCompletedWithSuccess/QuestCompletedWithBetray leaf methods, with NO chained-external-Consequence
        // gap unlike Artisan Overpriced Goods - see Patches.RevenueFarmingOwnershipGatePatches's doc comment for
        // the full per-listener trace), plus a genuinely bespoke accept-time mechanic: _revenueVillages/
        // _totalRequestedDenars are re-derived LIVE from _targetSettlement.BoundVillages at accept time
        // (per-client-divergent if raid timing differs), and _totalRequestedDenars is a genuinely different
        // number than the Issue-level preview property due to integer-division-on-differing-groupings - see
        // Interfaces.IRevenueFarmingIssueInterface's doc comment. No orphaned standalone disable patch was
        // found for this type (a 19-file dead-code sweep already removed DisableRevenueFarmingIssueBehavior.cs -
        // confirmed absent again here), so this allowlist entry is this type's only structural change.
        typeof(RevenueFarmingIssueBehavior),
        // Gang Leader Needs Weapons - its own creation is the simplest in this whole family - the Issue ctor
        // takes ONLY the owner Hero, since both fields it internally rolls are fully deterministic given shared
        // state (a single-element WeaponClass array + a pure function of every town's shared ItemRoster) - see
        // Interfaces.IGangLeaderNeedsWeaponsIssueInterface's type doc comment. _guardsParty is populated by
        // CreateGuardsParty(), called only from a RegisterEvents()-wired OnSettlementEnter listener rather than
        // an accept-time dialogue Consequence - and its own gating condition (party == MobileParty.MainParty) is
        // trivially satisfiable by ANY peer's own local settlement-entry, not just the genuine owner's, so it
        // needed BOTH a non-owner block AND a client-owner spawn-authority gate - see
        // Patches.GangLeaderNeedsWeaponsGuardsPartySpawnGatePatch's doc comment. A confirmed weapon-delivery
        // ownership gap (Patches.GangLeaderNeedsWeaponsQuestOwnershipGatePatch) plus three more independently-
        // found ones on the shared guard dialogue's convergent leaves are also fixed there. The scripted-battle
        // "whichever peer's menu opens first" race is closed by
        // Patches.GangLeaderNeedsWeaponsBattleStartApprovalPatches. No orphaned standalone disable patch was
        // found for this type (grepped the whole tree - zero hits, same as Smugglers/Artisan Overpriced Goods),
        // so the allowlist entry plus this type's own bespoke Interfaces/Messages/Patches/Handler set
        // (source/GameInterface/Services/Issues/*/GangLeaderNeedsWeapons*.cs) is the whole of this type's work.
        typeof(GangLeaderNeedsWeaponsIssueQuestBehavior),
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

        VerifyAllowlistIntegrity();
    }

    /// <summary>
    /// Structural safeguard against the exact bug fixed several times on the parent branch this was extracted
    /// from: an implementer adds a type to <see cref="Allowlist"/> without noticing/removing a pre-existing
    /// standalone "Disable&lt;Type&gt;IssueBehavior" patch elsewhere in the codebase (a leftover from before
    /// this allowlist mechanism existed) that unconditionally no-ops the SAME type's RegisterEvents. The loop
    /// in <see cref="ApplyAllowlist"/> above explicitly SKIPS patching allowlisted types, on the assumption
    /// nothing else is blocking them - so it does nothing to counteract such an orphaned patch. The type then
    /// silently never registers, exactly as if it had never been allowlisted, and nothing would ever notice
    /// until an independent review caught it.
    ///
    /// This runs once, right after every patch this method intends to apply has been applied, and asks
    /// Harmony itself (not our own bookkeeping) whether any allowlisted type's RegisterEvents still carries
    /// ANY prefix patch at all. No patch in this mod is ever meant to prefix an allowlisted issue behavior's
    /// RegisterEvents specifically - the loop above never does (it skips these types), and the handful of
    /// other patches that touch allowlisted issue types target different methods entirely (e.g. an Instance
    /// property getter, IssueManager.CreateNewIssue, a quest's own accept-condition delegate). So ANY
    /// RegisterEvents prefix found here can only be a leftover orphaned disable patch, and this fails loudly
    /// and early - at the same "first RegisterEvents call" chokepoint as the allowlist itself - instead of
    /// shipping silently broken again.
    /// </summary>
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
            "being correctly allowlisted here. This is the exact orphaned-disable-patch bug already fixed " +
            "multiple times on the parent branch this was extracted from: a leftover standalone " +
            "'Disable<Type>IssueBehavior' Harmony patch class elsewhere in the codebase, predating this " +
            "allowlist, unconditionally no-ops RegisterEvents. Find and DELETE the offending patch class(es) - " +
            "do not add any CallOriginalPolicy/category gate to them, they should not exist at all now that " +
            "the type is allowlisted. Affected: {Offenders}",
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
