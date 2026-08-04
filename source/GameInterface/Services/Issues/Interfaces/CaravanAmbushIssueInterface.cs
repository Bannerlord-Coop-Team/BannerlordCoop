using Common.Logging;
using Common.Util;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Helpers;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection/replay access <see cref="Patches.CaravanAmbushIssueCreationPatch"/>,
/// <see cref="Patches.CaravanAmbushPartySpawnGatePatch"/> and <see cref="Handlers.CaravanAmbushIssueHandler"/>
/// need to capture and authoritatively replicate a <see cref="CaravanAmbushIssueBehavior.CaravanAmbushIssue"/>.
///
/// CREATION: <c>CaravanAmbushIssue</c>'s ctor takes <c>(Hero, Settlement)</c> directly - the target settlement
/// is picked server-side (creation is already fully server-authoritative - see
/// <see cref="Patches.IssueManagerCreateNewIssuePatches"/> - via <c>CaravanAmbushIssueBehavior.GetTargetSettlement</c>'s
/// <c>MinBy</c> distance pick, itself deterministic GIVEN a shared map, but the SELECTION of which settlement is
/// even offered runs through <c>OnCheckForIssue</c> per-client before this issue exists at all, so - same
/// reasoning as Smugglers' two-settlement pair - <see cref="ConstructReplicated"/> needs no reflection at all,
/// the real public ctor accepts the captured settlement directly.
///
/// ACCEPT TIME (the real, independently-verified gap this quest needed its own infrastructure for - see
/// <see cref="Patches.CaravanAmbushPartySpawnGatePatch"/>'s doc comment for the full derivation): unlike
/// Smugglers, <c>CaravanAmbushIssueQuest.OnQuestAccepted()</c> has NO separable private "CreateXParty()" method -
/// both <c>_caravanParty</c> (<c>CaravanPartyComponent.CreateCaravanParty</c>) and <c>_banditParty</c>
/// (<c>BanditPartyComponent.CreateBanditParty</c>) are constructed inline, in the SAME method that also calls
/// <c>StartQuest()</c>, rolls 3 random reward items, and sets a deterministic 1-hour vicinity-check grace timer.
/// <c>CaravanPartyComponentLifetimePatches</c>/<c>BanditPartyComponentLifetimePatches</c> both hard-block their
/// component's ctor on a client exactly like <c>CustomPartyComponentLifetimePatches</c> did for Smugglers
/// (confirmed by direct read - both return false under <c>ModInformation.IsClient</c>), so BOTH party
/// constructions are a real client-authority gap, not just one.
///
/// <see cref="RunLocalAcceptSideEffects"/> replicates ONLY the two parts of <c>OnQuestAccepted()</c> that are
/// safe and must stay local to the genuine accepter's own machine regardless of host/client
/// (<c>StartQuest()</c> - so <c>RegisterEvents()</c>/<c>HourlyTick</c>/<c>MapEventEnded</c> etc. activate on the
/// CORRECT machine, i.e. the real accepter's, not the server's - and the activation journal log). The vicinity-
/// check grace timer (<c>CampaignTime.HoursFromNow(1f)</c>) is deliberately NOT captured/forced over the network -
/// it depends only on the shared, already-synchronized campaign clock, so every machine that independently
/// computes it (the client's own <see cref="RunLocalAcceptSideEffects"/>, or the server's own
/// <see cref="CreateReplicatedAccept"/>) lands on the same value within normal network-latency tolerance, and
/// it is only ever read inside <c>HourlyTick()</c>, itself owner-gated (Category A) - a peer who isn't the
/// owner never reads their own independently-computed copy anyway.
///
/// <see cref="CreateReplicatedAccept"/> is a faithful, parameterized reimplementation of the REST of
/// <c>OnQuestAccepted()</c>'s body (both party constructions, their roster fills, AI wiring, and the reward-item
/// roll), run ONLY on the server (either because the server itself is the genuine accepter, or because it's
/// servicing a client's forwarded <c>RequestCaravanAmbushAccept</c>). Parameterized on
/// <c>accepterMainPartySpeed</c> specifically because <c>MobilePartyHelper.TryMatchPartySpeedWithItemWeight(_caravanParty,
/// MobileParty.MainParty.Speed * 0.7f)</c> reads <c>MobileParty.MainParty</c> inside the real method's own body -
/// reading the SERVER's own MainParty there would be wrong whenever the genuine accepter is a remote client,
/// exactly the hazard flagged going into this task and already solved the same way for Smugglers' own
/// <c>desiredMenCount</c>/<c>customPartyBaseSpeed</c>. <see cref="Patches.CaravanAmbushPartySpawnGatePatch"/>
/// captures <c>MobileParty.MainParty.Speed</c> from the ACCEPTER's own machine (whichever machine that genuinely
/// is) before any blocking/forwarding decision, and this method takes it as a parameter instead of re-deriving
/// it locally.
///
/// DELIBERATE, DOCUMENTED SIMPLIFICATION: the real method's bandit-party hideout lookup
/// (<c>SettlementHelper.FindNearestHideoutToMobileParty(MobileParty.MainParty, ...)</c>) ALSO reads the
/// accepter's own <c>MobileParty.MainParty</c> (its POSITION, not its speed) to pick which hideout - and hence
/// which bandit clan/home settlement - the ephemeral, quest-scripted <c>_banditParty</c> nominally belongs to.
/// This is independently a real "read MainParty for accepter-specific data" gap by the same shape as the speed
/// read, found during this task's independent re-verification (NOT flagged in the task's own "known facts").
/// It is deliberately NOT captured/forwarded here: <c>DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement</c>
/// needs a real, live <see cref="MobileParty"/> instance (navmesh/naval-aware distance, not a bare position), not
/// something a captured <c>Vec2</c> can faithfully reproduce without either constructing a throwaway party
/// (heavy, its own correctness risk) or re-implementing that distance model here. <c>_banditParty</c> is
/// AI-locked (<c>DoNotMakeNewDecisions</c>) and scripted at the caravan for the whole quest - the chosen hideout
/// only matters as a cosmetic "where these bandits are nominally from" attribute and a one-time post-quest
/// "go home" AI order in <c>HandlePartyAiAfterCompletion()</c> - so this is accepted as a low-impact,
/// server's-own-local-context divergence rather than built out. Flagged explicitly here (and in the
/// implementation's final report) rather than silently dropped.
/// </summary>
public interface ICaravanAmbushIssueInterface : IGameAbstraction
{
    /// <summary>Reads the rolled/picked target settlement off an already-constructed issue (direct field read -
    /// see the type doc comment on why no reflection is needed here). Returns false if the settlement is
    /// missing, which should never happen for a real instance.</summary>
    bool TryCaptureTargetSettlement(CaravanAmbushIssueBehavior.CaravanAmbushIssue issue, out Settlement targetSettlement);

    /// <summary>
    /// Builds a <see cref="CaravanAmbushIssueBehavior.CaravanAmbushIssue"/> via its real public ctor, which
    /// takes the settlement directly - no field-forcing/reflection needed (see the type doc comment). Does not
    /// register it with the <see cref="IssueManager"/> - see <see cref="RegisterReplicated"/>.
    /// </summary>
    CaravanAmbushIssueBehavior.CaravanAmbushIssue ConstructReplicated(Hero owner, Settlement targetSettlement);

    /// <summary>
    /// Registers an already-built, already-correct issue instance with <see cref="Campaign.Current"/>'s
    /// <see cref="IssueManager"/>, replaying <c>IssueManager.CreateNewIssue</c>'s own bookkeeping via a custom
    /// <see cref="PotentialIssueData"/> whose <c>OnStartIssue</c> hands back <paramref name="issue"/> instead
    /// of constructing (and re-rolling the target settlement on) a new one - same technique as
    /// <see cref="SmugglersIssueInterface.RegisterReplicated"/>.
    /// </summary>
    void RegisterReplicated(Hero owner, CaravanAmbushIssueBehavior.CaravanAmbushIssue issue);

    /// <summary>
    /// Runs the two parts of the real <c>OnQuestAccepted()</c> body that must stay local to the genuine
    /// accepter's own machine - <c>StartQuest()</c> and the activation journal log - plus the deterministic
    /// 1-hour vicinity-check grace timer. See the type doc comment for why these three are safe to run
    /// unforced/uncaptured. A no-op if the owner has no <see cref="CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest"/>
    /// yet.
    /// </summary>
    void RunLocalAcceptSideEffects(Hero owner);

    /// <summary>Reads <c>_caravanParty</c>/<c>_banditParty</c>/<c>_rewardItems</c> off <paramref name="owner"/>'s
    /// current <see cref="CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest"/>. Returns false if the hero has
    /// no quest of this type yet, or the parties haven't been spawned on this peer yet.</summary>
    bool TryCaptureAcceptedState(Hero owner, out MobileParty caravanParty, out MobileParty banditParty, out List<ItemObject> rewardItems);

    /// <summary>Force-writes <c>_caravanParty</c>/<c>_banditParty</c>/<c>_rewardItems</c> (reflection - see the
    /// type doc comment) onto <paramref name="owner"/>'s current quest, so every peer's own mirror references
    /// the SAME already-AutoRegistry-synced <see cref="MobileParty"/>s and the SAME server-rolled reward items
    /// the server genuinely created. A no-op if the owner has no
    /// <see cref="CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest"/> yet. <paramref name="rewardItems"/> may
    /// be null, in which case only the two party fields are forced.</summary>
    void ForceAcceptedState(Hero owner, MobileParty caravanParty, MobileParty banditParty, List<ItemObject> rewardItems);

    /// <summary>
    /// The central mechanism (see the type doc comment): a faithful, parameterized reimplementation of the
    /// party-construction/roster-fill/AI-wiring/reward-roll portion of the real, private
    /// <c>CaravanAmbushIssueQuest.OnQuestAccepted()</c>, run on the server against <paramref name="owner"/>'s
    /// current quest, using <paramref name="accepterMainPartySpeed"/> in place of the real method's own
    /// (server-side-wrong-when-a-remote-client-accepted) <c>MobileParty.MainParty.Speed</c> read. Also
    /// force-writes the result onto the quest via <see cref="ForceAcceptedState"/> before returning. Returns
    /// false if the owner has no <see cref="CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest"/> yet, or its
    /// target settlement is missing.
    /// </summary>
    bool CreateReplicatedAccept(Hero owner, float accepterMainPartySpeed, out MobileParty caravanParty, out MobileParty banditParty, out List<ItemObject> rewardItems);
}

/// <inheritdoc cref="ICaravanAmbushIssueInterface"/>
public class CaravanAmbushIssueInterface : ICaravanAmbushIssueInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<CaravanAmbushIssueInterface>();

    private static readonly FieldInfo CaravanPartyField =
        AccessTools.Field(typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest), "_caravanParty");
    private static readonly FieldInfo BanditPartyField =
        AccessTools.Field(typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest), "_banditParty");
    private static readonly FieldInfo RewardItemsField =
        AccessTools.Field(typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest), "_rewardItems");
    private static readonly FieldInfo VicinityCheckDisabledUntilField =
        AccessTools.Field(typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest), "_vicinityCheckDisabledUntil");
    private static readonly FieldInfo IssueDifficultyField =
        AccessTools.Field(typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest), "_issueDifficulty");
    private static readonly FieldInfo QuestTargetSettlementField =
        AccessTools.Field(typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest), "_targetSettlement");

    private static readonly PropertyInfo ActivatedLogTextProperty =
        AccessTools.Property(typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest), "CaravanAmbushIssueQuestActivatedLogText");

    public bool TryCaptureTargetSettlement(CaravanAmbushIssueBehavior.CaravanAmbushIssue issue, out Settlement targetSettlement)
    {
        targetSettlement = null;
        if (issue == null) return false;

        targetSettlement = issue._targetSettlement;
        return targetSettlement != null;
    }

    public CaravanAmbushIssueBehavior.CaravanAmbushIssue ConstructReplicated(Hero owner, Settlement targetSettlement)
    {
        return new CaravanAmbushIssueBehavior.CaravanAmbushIssue(owner, targetSettlement);
    }

    public void RegisterReplicated(Hero owner, CaravanAmbushIssueBehavior.CaravanAmbushIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssue), IssueBase.IssueFrequency.Common);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }

    public void RunLocalAcceptSideEffects(Hero owner)
    {
        if (owner?.Issue?.IssueQuest is not CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest quest) return;

        quest.StartQuest();

        var logText = (TextObject)ActivatedLogTextProperty.GetValue(quest);
        quest.AddLog(logText);

        VicinityCheckDisabledUntilField.SetValue(quest, CampaignTime.HoursFromNow(1f));
    }

    public bool TryCaptureAcceptedState(Hero owner, out MobileParty caravanParty, out MobileParty banditParty, out List<ItemObject> rewardItems)
    {
        caravanParty = null;
        banditParty = null;
        rewardItems = null;
        if (owner?.Issue?.IssueQuest is not CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest quest) return false;

        caravanParty = (MobileParty)CaravanPartyField.GetValue(quest);
        banditParty = (MobileParty)BanditPartyField.GetValue(quest);
        rewardItems = (List<ItemObject>)RewardItemsField.GetValue(quest);

        return caravanParty != null && banditParty != null;
    }

    public void ForceAcceptedState(Hero owner, MobileParty caravanParty, MobileParty banditParty, List<ItemObject> rewardItems)
    {
        if (owner?.Issue?.IssueQuest is not CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest quest) return;

        CaravanPartyField.SetValue(quest, caravanParty);
        BanditPartyField.SetValue(quest, banditParty);
        if (rewardItems != null)
        {
            RewardItemsField.SetValue(quest, rewardItems);
        }
    }

    public bool CreateReplicatedAccept(Hero owner, float accepterMainPartySpeed, out MobileParty caravanParty, out MobileParty banditParty, out List<ItemObject> rewardItems)
    {
        caravanParty = null;
        banditParty = null;
        rewardItems = null;
        if (owner?.Issue?.IssueQuest is not CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest quest) return false;

        var targetSettlement = (Settlement)QuestTargetSettlementField.GetValue(quest);
        if (targetSettlement == null) return false;

        var issueDifficulty = (float)IssueDifficultyField.GetValue(quest);
        var caravanPartyTroopCount = 22 + MathF.Ceiling(30f * issueDifficulty);
        var banditPartyTroopCount = 25 + MathF.Ceiling(50f * issueDifficulty);

        var itemRoster = new ItemRoster();
        itemRoster.AddToCounts(MBObjectManager.Instance.GetObject<ItemObject>("fish"), 20);
        itemRoster.AddToCounts(MBObjectManager.Instance.GetObject<ItemObject>("grain"), 40);
        itemRoster.AddToCounts(MBObjectManager.Instance.GetObject<ItemObject>("butter"), 20);
        itemRoster.AddToCounts(DefaultItems.HardWood, 60);

        var randomCaravanTemplate = CaravanHelper.GetRandomCaravanTemplate(owner.Culture, isElite: false, isLand: true);
        caravanParty = CaravanPartyComponent.CreateCaravanParty(owner, owner.CurrentSettlement, randomCaravanTemplate, isInitialSpawn: false, null, itemRoster);
        caravanParty.MemberRoster.Clear();
        caravanParty.MemberRoster.AddToCounts(owner.Culture.CaravanMaster, 1);
        caravanParty.MemberRoster.AddToCounts(owner.Culture.BasicTroop, caravanPartyTroopCount);
        caravanParty.IgnoreByOtherPartiesTill(quest.QuestDueTime);
        Campaign.Current.MobilePartyLocator.UpdateLocator(caravanParty);
        SetPartyAiAction.GetActionForVisitingSettlement(caravanParty, targetSettlement, MobileParty.NavigationType.Default, isFromPort: false, isTargetingPort: false);
        caravanParty.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: true);
        caravanParty.SetPartyUsedByQuest(isActivelyUsed: true);
        quest.AddTrackedObject(caravanParty);
        // Captured from the ACCEPTER's own machine by CaravanAmbushPartySpawnGatePatch before forwarding - see
        // the type doc comment for why the server's own MobileParty.MainParty.Speed would be wrong here.
        MobilePartyHelper.TryMatchPartySpeedWithItemWeight(caravanParty, accepterMainPartySpeed * 0.7f);

        // Deliberate, documented simplification - see the type doc comment: uses THIS machine's (the server's)
        // own MobileParty.MainParty for the hideout lookup rather than the accepter's, a low-impact cosmetic
        // divergence when the accepter is a remote client.
        var hideout = SettlementHelper.FindNearestHideoutToMobileParty(MobileParty.MainParty, MobileParty.NavigationType.Default, x => x.IsActive);
        var clan = Clan.BanditFactions.FirstOrDefault(x => x.StringId == "looters");
        var partyTemplateObject = Campaign.Current.ObjectManager.GetObject<PartyTemplateObject>("kingdom_hero_party_caravan_ambushers") ?? clan.DefaultPartyTemplate;
        banditParty = BanditPartyComponent.CreateBanditParty("caravan_ambush_quest_" + clan.Name, clan, hideout.Settlement.Hideout, isBossParty: false, partyTemplateObject, targetSettlement.GatePosition);
        banditParty.Party.SetCustomName(new TextObject("{=u1Pkt4HC}Raiders"));
        Campaign.Current.MobilePartyLocator.UpdateLocator(banditParty);
        banditParty.MemberRoster.Clear();
        banditParty.SetPartyUsedByQuest(isActivelyUsed: true);
        quest.AddTrackedObject(banditParty);

        // Canonical, single, server-side-only RNG roll for the bandit roster composition - same reasoning as
        // Smugglers' MobilePartyHelper.FillPartyManuallyAfterCreation roll: only ever run once, here, so its
        // result is faithfully captured by the AutoRegistry sync as-is with no divergence risk.
        for (var i = 0; i < banditPartyTroopCount; i++)
        {
            var weighted = new List<(PartyTemplateStack, float)>();
            foreach (var stack in partyTemplateObject.Stacks)
            {
                weighted.Add((stack, 64 - stack.Character.Level));
            }
            var chosen = MBRandom.ChooseWeighted(weighted);
            banditParty.MemberRoster.AddToCounts(chosen.Character, 1);
        }
        banditParty.ItemRoster.AddToCounts(MBObjectManager.Instance.GetObject<ItemObject>("sumpter_horse"), banditPartyTroopCount / 4);
        banditParty.IgnoreByOtherPartiesTill(quest.QuestDueTime);
        SetPartyAiAction.GetActionForEngagingParty(banditParty, caravanParty, MobileParty.NavigationType.Default, isFromPort: false);
        banditParty.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: true);

        // Canonical, single, server-side-only RNG roll for the 3 reward items - same reasoning as the bandit
        // roster above, except this is a QUEST-level field (not part of any MobileParty/TroopRoster), so it is
        // NOT covered by AutoRegistry sync at all and needs its own explicit force-write back to every peer
        // (including back to the owner itself, if the owner is the remote client that triggered this replay) -
        // see ForceAcceptedState/NetworkCaravanAmbushAccepted.
        rewardItems = new List<ItemObject>();
        for (var i = 0; i < 3; i++)
        {
            rewardItems.Add(Items.All.GetRandomElementWithPredicate(t => t.IsTradeGood && !t.NotMerchandise));
        }

        ForceAcceptedState(owner, caravanParty, banditParty, rewardItems);

        return true;
    }
}
