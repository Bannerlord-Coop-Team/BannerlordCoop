using HarmonyLib;
using Helpers;
using SandBox.Issues;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.RivalGangMovingInIssueCreationPatch"/>,
/// <see cref="Patches.RivalGangMovingInPartySpawnGatePatch"/>,
/// <see cref="Patches.RivalGangMovingInAlleyBattlePatch"/> and
/// <see cref="Handlers.RivalGangMovingInIssueHandler"/>/<see cref="Handlers.RivalGangMovingInPartyCreationCoordinator"/>
/// need to capture and authoritatively replicate a <see cref="RivalGangMovingInIssueBehavior.RivalGangMovingInIssue"/>
/// and its quest's 2 scripted parties + 2 henchman Heroes. See doc/RivalGangMovingIn_Design_v2.md §2/§5 for the
/// full derivation.
///
/// CREATION: <c>RivalGangMovingInIssue</c>'s ctor takes <c>(Hero issueOwner, Hero rivalGangLeader)</c> directly -
/// <c>rivalGangLeader</c> is picked at issue-creation time by <c>GetRivalGangLeader</c>'s plain, deterministic-
/// order <c>Notables</c> walk (no <c>MBRandom</c>/<c>GetRandomElement</c> roll in it) - so <see cref="ConstructReplicated"/>
/// needs no reflection, the real public ctor accepts both directly, and <c>RivalGangLeader</c> is a public
/// property on the Issue (no reflection needed to read it either).
///
/// PARTY SPAWN (the real, independently-verified gap this quest needed its own infrastructure for - see
/// <see cref="Patches.RivalGangMovingInPartySpawnGatePatch"/>'s doc comment): both <c>_rivalGangLeaderParty</c>
/// and <c>_allyGangLeaderParty</c> are only ever set by <c>StartAlleyBattle()</c>'s calls to the private
/// <c>CreateRivalGangLeaderParty()</c>/<c>CreateAllyGangLeaderParty()</c>, both only ever reached on the genuine
/// accepter's own machine (Category B - only reachable via a live one-to-one conversation consequence chain).
/// Both call <c>CustomPartyComponent.CreateCustomPartyWithTroopRoster(...)</c>, whose inner
/// <c>new CustomPartyComponent(...)</c> is HARD-BLOCKED on a client, and the resulting <c>MobileParty</c>s are
/// silently orphaned (never registered/synced) - the same Smugglers-shaped bug, confirmed present for BOTH
/// parties here too. <see cref="CreateReplicatedParties"/> is a faithful reimplementation of both methods' bodies
/// combined into one call, run ONLY on the server, substituting
/// <c>SettlementHelper.FindNearestHideoutToSettlement(_questSettlement, ...)</c> for the client-authority-AND-
/// headless-server-hazardous <c>FindNearestHideoutToMobileParty(MobileParty.MainParty, ...)</c> vanilla uses -
/// see §5.2 of the design doc: since the troop counts here are fixed constants (15/20, not MainParty-relative
/// percentages like Smugglers' own <c>desiredMenCount</c>), nothing about these two methods is actually
/// per-accepter-divergent, so no client-captured value needs to be forwarded at all, and the settlement-based
/// substitution also closes the dedicated-headless-server <c>MobileParty.MainParty == null</c> gap for free. Each
/// method also mints its henchman <see cref="Hero"/> via <c>HeroCreator.CreateSpecialHero</c> exactly as vanilla
/// does - minted ONLY server-side, avoiding the known host-vs-client-accepter Hero-minting asymmetry bug this
/// project has already found elsewhere (Family Feud/Landowner's Daughter).
/// </summary>
public interface IRivalGangMovingInIssueInterface : IGameAbstraction
{
    /// <summary>Builds a <see cref="RivalGangMovingInIssueBehavior.RivalGangMovingInIssue"/> via its real public
    /// ctor, which takes both the owner and the already-captured <see cref="Hero"/> RivalGangLeader directly - no
    /// reflection/field-forcing needed (see the type doc comment). Does not register it with the
    /// <see cref="IssueManager"/> - see <see cref="RegisterReplicated"/>.</summary>
    RivalGangMovingInIssueBehavior.RivalGangMovingInIssue ConstructReplicated(Hero owner, Hero rivalGangLeader);

    /// <summary>Registers an already-built, already-correct issue instance with <see cref="Campaign.Current"/>'s
    /// <see cref="IssueManager"/>, replaying <c>IssueManager.CreateNewIssue</c>'s own bookkeeping via a custom
    /// <see cref="PotentialIssueData"/> whose <c>OnStartIssue</c> hands back <paramref name="issue"/> instead of
    /// constructing (and re-rolling the RivalGangLeader pick on) a new one - same technique as
    /// <see cref="VillageNeedsToolsIssueInterface.RegisterReplicated"/>.</summary>
    void RegisterReplicated(Hero owner, RivalGangMovingInIssueBehavior.RivalGangMovingInIssue issue);

    /// <summary>Reads the 4 quest-internal party/hero fields off <paramref name="owner"/>'s current
    /// <see cref="RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest"/>. Returns false if the hero has no
    /// quest of this type yet, or any of the 4 haven't been spawned on this peer yet.</summary>
    bool TryCaptureRivalGangParties(
        Hero owner,
        out MobileParty rivalGangLeaderParty,
        out Hero rivalGangLeaderHenchmanHero,
        out MobileParty allyGangLeaderParty,
        out Hero allyGangLeaderHenchmanHero);

    /// <summary>Force-writes all 4 quest-internal fields (reflection - see the type doc comment) onto
    /// <paramref name="owner"/>'s current quest, so every peer's own mirror references the SAME
    /// already-AutoRegistry-synced <see cref="MobileParty"/>/<see cref="Hero"/> objects the server genuinely
    /// created. A no-op if the owner has no <see cref="RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest"/>
    /// yet.</summary>
    void ForceRivalGangParties(
        Hero owner,
        MobileParty rivalGangLeaderParty,
        Hero rivalGangLeaderHenchmanHero,
        MobileParty allyGangLeaderParty,
        Hero allyGangLeaderHenchmanHero);

    /// <summary>
    /// The central mechanism (see the type doc comment): a faithful reimplementation of both the real, private
    /// <c>RivalGangMovingInIssueQuest.CreateRivalGangLeaderParty()</c> AND <c>CreateAllyGangLeaderParty()</c>,
    /// run on the server against <paramref name="owner"/>'s current quest, substituting a settlement-based
    /// hideout/culture lookup for the real methods' own (client-authority-AND-headless-server-hazardous)
    /// <c>MobileParty.MainParty</c>-based one. Also force-writes all 4 results onto the quest before returning.
    /// Returns false if the owner has no
    /// <see cref="RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest"/> yet, its quest settlement is
    /// missing, or no hideout can be found.
    /// </summary>
    bool CreateReplicatedParties(
        Hero owner,
        out MobileParty rivalGangLeaderParty,
        out Hero rivalGangLeaderHenchmanHero,
        out MobileParty allyGangLeaderParty,
        out Hero allyGangLeaderHenchmanHero);
}

/// <inheritdoc cref="IRivalGangMovingInIssueInterface"/>
public class RivalGangMovingInIssueInterface : IRivalGangMovingInIssueInterface
{
    private static readonly Type QuestType = typeof(RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest);

    private static readonly FieldInfo QuestSettlementField = AccessTools.Field(QuestType, "_questSettlement");
    private static readonly FieldInfo RivalGangLeaderField = AccessTools.Field(QuestType, "_rivalGangLeader");
    private static readonly FieldInfo RivalGangLeaderPartyField = AccessTools.Field(QuestType, "_rivalGangLeaderParty");
    private static readonly FieldInfo RivalGangLeaderHenchmanHeroField = AccessTools.Field(QuestType, "_rivalGangLeaderHenchmanHero");
    private static readonly FieldInfo AllyGangLeaderPartyField = AccessTools.Field(QuestType, "_allyGangLeaderParty");
    private static readonly FieldInfo AllyGangLeaderHenchmanHeroField = AccessTools.Field(QuestType, "_allyGangLeaderHenchmanHero");
    private static readonly MethodInfo GetTroopTypeTemplateForDifficultyMethod = AccessTools.Method(QuestType, "GetTroopTypeTemplateForDifficulty");

    public RivalGangMovingInIssueBehavior.RivalGangMovingInIssue ConstructReplicated(Hero owner, Hero rivalGangLeader)
    {
        return new RivalGangMovingInIssueBehavior.RivalGangMovingInIssue(owner, rivalGangLeader);
    }

    public void RegisterReplicated(Hero owner, RivalGangMovingInIssueBehavior.RivalGangMovingInIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(RivalGangMovingInIssueBehavior.RivalGangMovingInIssue), IssueBase.IssueFrequency.Common);

        using (new Common.Util.AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }

    public bool TryCaptureRivalGangParties(
        Hero owner,
        out MobileParty rivalGangLeaderParty,
        out Hero rivalGangLeaderHenchmanHero,
        out MobileParty allyGangLeaderParty,
        out Hero allyGangLeaderHenchmanHero)
    {
        rivalGangLeaderParty = null;
        rivalGangLeaderHenchmanHero = null;
        allyGangLeaderParty = null;
        allyGangLeaderHenchmanHero = null;

        if (owner?.Issue?.IssueQuest is not RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest quest) return false;

        rivalGangLeaderParty = (MobileParty)RivalGangLeaderPartyField.GetValue(quest);
        rivalGangLeaderHenchmanHero = (Hero)RivalGangLeaderHenchmanHeroField.GetValue(quest);
        allyGangLeaderParty = (MobileParty)AllyGangLeaderPartyField.GetValue(quest);
        allyGangLeaderHenchmanHero = (Hero)AllyGangLeaderHenchmanHeroField.GetValue(quest);

        return rivalGangLeaderParty != null && rivalGangLeaderHenchmanHero != null
            && allyGangLeaderParty != null && allyGangLeaderHenchmanHero != null;
    }

    public void ForceRivalGangParties(
        Hero owner,
        MobileParty rivalGangLeaderParty,
        Hero rivalGangLeaderHenchmanHero,
        MobileParty allyGangLeaderParty,
        Hero allyGangLeaderHenchmanHero)
    {
        if (owner?.Issue?.IssueQuest is not RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest quest) return;

        RivalGangLeaderPartyField.SetValue(quest, rivalGangLeaderParty);
        RivalGangLeaderHenchmanHeroField.SetValue(quest, rivalGangLeaderHenchmanHero);
        AllyGangLeaderPartyField.SetValue(quest, allyGangLeaderParty);
        AllyGangLeaderHenchmanHeroField.SetValue(quest, allyGangLeaderHenchmanHero);
    }

    public bool CreateReplicatedParties(
        Hero owner,
        out MobileParty rivalGangLeaderParty,
        out Hero rivalGangLeaderHenchmanHero,
        out MobileParty allyGangLeaderParty,
        out Hero allyGangLeaderHenchmanHero)
    {
        rivalGangLeaderParty = null;
        rivalGangLeaderHenchmanHero = null;
        allyGangLeaderParty = null;
        allyGangLeaderHenchmanHero = null;

        if (owner?.Issue?.IssueQuest is not RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest quest) return false;

        var questSettlement = (Settlement)QuestSettlementField.GetValue(quest);
        var rivalGangLeader = (Hero)RivalGangLeaderField.GetValue(quest);
        if (questSettlement == null || rivalGangLeader == null) return false;

        var nearestHideout = SettlementHelper.FindNearestHideoutToSettlement(
            questSettlement, MobileParty.NavigationType.All, x => x.IsActive);
        if (nearestHideout == null) return false;

        var troopTypeTemplate = (CharacterObject)GetTroopTypeTemplateForDifficultyMethod.Invoke(quest, null);
        var clan = Clan.BanditFactions.FirstOrDefault(t => t.Culture == nearestHideout.Settlement.Culture);

        // --- Rival gang leader's party - faithful reimplementation of CreateRivalGangLeaderParty(). ---
        var rivalPartyName = new TextObject("{=u4jhIFwG}{GANG_LEADER}'s Party");
        rivalPartyName.SetTextVariable("RIVAL_GANG_LEADER", rivalGangLeader.Name);
        rivalPartyName.SetTextVariable("GANG_LEADER", rivalGangLeader.Name);

        rivalGangLeaderParty = CustomPartyComponent.CreateCustomPartyWithTroopRoster(
            questSettlement.GatePosition, 1f, questSettlement, rivalPartyName, clan,
            TroopRoster.CreateDummyTroopRoster(), TroopRoster.CreateDummyTroopRoster(), null, "", "", 0f, false);
        rivalGangLeaderParty.SetPartyUsedByQuest(true);
        rivalGangLeaderParty.MemberRoster.AddToCounts(troopTypeTemplate, 15, false, 0, 0, true, -1);

        var rivalHenchmanTemplate = MBObjectManager.Instance.GetObject<CharacterObject>("gangster_3");
        rivalGangLeaderHenchmanHero = HeroCreator.CreateSpecialHero(rivalHenchmanTemplate, null, null, null, -1);
        var rivalHenchmanName = new TextObject("{=zJqEdDiq}Henchman of {GANG_LEADER}");
        rivalHenchmanName.SetTextVariable("GANG_LEADER", rivalGangLeader.Name);
        rivalGangLeaderHenchmanHero.SetName(rivalHenchmanName, rivalHenchmanName);
        rivalGangLeaderHenchmanHero.HiddenInEncyclopedia = true;
        rivalGangLeaderHenchmanHero.Culture = rivalGangLeader.Culture;
        rivalGangLeaderHenchmanHero.SetNewOccupation(Occupation.Special);
        rivalGangLeaderHenchmanHero.ChangeState(Hero.CharacterStates.Active);
        rivalGangLeaderParty.MemberRoster.AddToCounts(rivalGangLeaderHenchmanHero.CharacterObject, 1, false, 0, 0, true, -1);
        rivalGangLeaderParty.IgnoreByOtherPartiesTill(CampaignTime.Never);
        EnterSettlementAction.ApplyForParty(rivalGangLeaderParty, questSettlement);

        // --- Ally (quest giver's) gang leader's party - faithful reimplementation of CreateAllyGangLeaderParty().
        // Deliberately does NOT call SetNewOccupation on the ally henchman here - vanilla doesn't either; it's
        // only set to Occupation.Special later, in OnFinalize, immediately before removal. ---
        var allyPartyName = new TextObject("{=u4jhIFwG}{GANG_LEADER}'s Party");
        allyPartyName.SetTextVariable("GANG_LEADER", owner.Name);

        allyGangLeaderParty = CustomPartyComponent.CreateCustomPartyWithTroopRoster(
            questSettlement.GatePosition, 1f, questSettlement, allyPartyName, clan,
            TroopRoster.CreateDummyTroopRoster(), TroopRoster.CreateDummyTroopRoster(), null, "", "", 0f, false);
        allyGangLeaderParty.SetPartyUsedByQuest(true);
        allyGangLeaderParty.MemberRoster.AddToCounts(troopTypeTemplate, 20, false, 0, 0, true, -1);

        var allyHenchmanTemplate = MBObjectManager.Instance.GetObject<CharacterObject>("gangster_2");
        allyGangLeaderHenchmanHero = HeroCreator.CreateSpecialHero(allyHenchmanTemplate, null, null, null, -1);
        var allyHenchmanName = new TextObject("{=zJqEdDiq}Henchman of {GANG_LEADER}");
        allyHenchmanName.SetTextVariable("GANG_LEADER", owner.Name);
        allyGangLeaderHenchmanHero.SetName(allyHenchmanName, allyHenchmanName);
        allyGangLeaderHenchmanHero.HiddenInEncyclopedia = true;
        allyGangLeaderHenchmanHero.Culture = owner.Culture;
        allyGangLeaderHenchmanHero.ChangeState(Hero.CharacterStates.Active);
        allyGangLeaderParty.MemberRoster.AddToCounts(allyGangLeaderHenchmanHero.CharacterObject, 1, false, 0, 0, true, -1);
        allyGangLeaderParty.IgnoreByOtherPartiesTill(CampaignTime.Never);
        EnterSettlementAction.ApplyForParty(allyGangLeaderParty, questSettlement);

        ForceRivalGangParties(owner, rivalGangLeaderParty, rivalGangLeaderHenchmanHero, allyGangLeaderParty, allyGangLeaderHenchmanHero);

        return true;
    }
}
