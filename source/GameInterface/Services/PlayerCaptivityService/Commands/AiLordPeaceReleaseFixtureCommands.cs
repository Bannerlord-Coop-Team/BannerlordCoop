#if DEBUG
using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Stances.Messages;
using GameInterface.Utils.Commands;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.PlayerCaptivityService.Commands;

/// <summary>
/// Reversible AI-lord captivity fixture for validating peace releases in a live campaign.
/// </summary>
internal static class AiLordPeaceReleaseFixtureCommands
{
    private static AiLordPeaceReleaseFixture fixture;
    private static StanceLinkSnapshot clientDiplomaticSnapshot;

    [CommandLineArgumentFunction("observe_ai_lord_pair", "coop.debug.player_captivity")]
    public static string ObserveAiLordPair(List<string> args)
    {
        const string usage = "Usage: coop.debug.player_captivity.observe_ai_lord_pair <prisonerHeroId> <captorHeroId>";
        var context = new CommandContext("observe_ai_lord_pair", usage, args);

        if (!context.RequireArgCount(2, out var error)) return error;
        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to observe AI lords: " + error;
        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var prisoner, out error))
            return "Failed to observe AI lords: " + error;
        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[1], out var captorHero, out error))
            return "Failed to observe AI lords: " + error;

        var heroParty = prisoner.PartyBelongedTo;
        var captorParty = prisoner.PartyBelongedToAsPrisoner?.MobileParty;
        var captorHeroParty = captorHero.PartyBelongedTo;
        var prisonerFaction = prisoner.MapFaction;
        var captorFaction = captorHero.MapFaction;
        var output = new StringBuilder();
        output.AppendLine("PrisonerHeroId=" + prisoner.StringId);
        output.AppendLine("CaptorHeroId=" + captorHero.StringId);
        output.AppendLine("PrisonerFactionId=" + (prisonerFaction?.StringId ?? "none"));
        output.AppendLine("CaptorFactionId=" + (captorFaction?.StringId ?? "none"));
        output.AppendLine("AtWar=" + (prisonerFaction != null && captorFaction != null
            ? prisonerFaction.IsAtWarWith(captorFaction).ToString()
            : "none"));
        output.AppendLine("HeroState=" + prisoner.HeroState);
        output.AppendLine("IsPrisoner=" + prisoner.IsPrisoner);
        output.AppendLine("CaptorPartyId=" + (captorParty?.StringId ?? "none"));
        output.AppendLine("CaptorPrisonerCount=" + (captorParty?.PrisonRoster.GetTroopCount(prisoner.CharacterObject) ?? 0));
        output.AppendLine("HeroPartyId=" + (heroParty?.StringId ?? "none"));
        output.AppendLine("HeroPartyActive=" + (heroParty?.IsActive.ToString() ?? "none"));
        output.AppendLine("HeroPartyLeaderId=" + (heroParty?.LeaderHero?.StringId ?? "none"));
        output.AppendLine("HeroPartyPosition=" + (heroParty == null ? "none" : FormatPosition(heroParty.Position)));
        output.AppendLine("CaptorHeroPartyId=" + (captorHeroParty?.StringId ?? "none"));
        output.AppendLine("CaptorHeroPartyActive=" + (captorHeroParty?.IsActive.ToString() ?? "none"));
        output.AppendLine("CaptorHeroPartyPosition=" + (captorHeroParty == null ? "none" : FormatPosition(captorHeroParty.Position)));
        output.Append("DiplomaticStateFingerprint=" + GetDiplomaticStateFingerprint(prisonerFaction, captorFaction));
        return output.ToString();
    }

    [CommandLineArgumentFunction("focus_hero_party", "coop.debug.player_captivity")]
    public static string FocusHeroParty(List<string> args)
    {
        const string usage = "Usage: coop.debug.player_captivity.focus_hero_party <heroId>";
        var context = new CommandContext("focus_hero_party", usage, args);

        if (!context.RequireArgCount(1, out var error)) return error;
        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to focus hero party: " + error;
        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var hero, out error))
            return "Failed to focus hero party: " + error;

        var party = hero.PartyBelongedTo ?? hero.PartyBelongedToAsPrisoner?.MobileParty;
        if (party?.IsActive != true)
            return $"Failed to focus hero party: '{hero.StringId}' has no active member or captor party.";

        party.Party.SetAsCameraFollowParty();
        return $"Following party '{party.StringId}' for hero '{hero.StringId}' on the campaign map.";
    }

    [CommandLineArgumentFunction("snapshot_ai_lord_diplomacy_fixture", "coop.debug.player_captivity")]
    public static string SnapshotAiLordDiplomacyFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.player_captivity.snapshot_ai_lord_diplomacy_fixture <prisonerHeroId> <captorHeroId>";
        var context = new CommandContext("snapshot_ai_lord_diplomacy_fixture", usage, args);

        if (!ModInformation.IsClient) return "snapshot_ai_lord_diplomacy_fixture must be run on a client.";
        if (!context.RequireArgCount(2, out var error)) return error;
        if (clientDiplomaticSnapshot != null) return "A client AI-lord diplomatic fixture is already active.";
        if (!TryGetHeroFactions(args, out var prisonerFaction, out var captorFaction, out error))
            return "Failed to snapshot AI-lord diplomacy: " + error;

        clientDiplomaticSnapshot = StanceLinkSnapshot.Capture(prisonerFaction, captorFaction);
        return "Client AI-lord diplomatic fixture captured.\nOriginalDiplomaticStateFingerprint=" +
            clientDiplomaticSnapshot.OriginalFingerprint;
    }

    [CommandLineArgumentFunction("restore_ai_lord_diplomacy_fixture", "coop.debug.player_captivity")]
    public static string RestoreAiLordDiplomacyFixture(List<string> args)
    {
        if (!ModInformation.IsClient)
            return "restore_ai_lord_diplomacy_fixture must be run on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.player_captivity.restore_ai_lord_diplomacy_fixture";
        if (clientDiplomaticSnapshot == null)
            return "No client AI-lord diplomatic fixture is active.";

        var pendingSnapshot = clientDiplomaticSnapshot;
        pendingSnapshot.Restore(false);
        var verification = pendingSnapshot.VerifyRestored();
        if (verification != null)
            return "Failed to restore client AI-lord diplomatic fixture: " + verification;

        clientDiplomaticSnapshot = null;
        return "Client AI-lord diplomatic fixture restored.";
    }

    [CommandLineArgumentFunction("capture_ai_lord_fixture", "coop.debug.player_captivity")]
    public static string CaptureAiLordFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.player_captivity.capture_ai_lord_fixture <prisonerHeroId> <captorHeroId>";
        var context = new CommandContext("capture_ai_lord_fixture", usage, args);

        if (!context.RequireServer(out var error)) return error;
        if (!context.RequireArgCount(2, out error)) return error;
        if (fixture != null) return "An AI-lord captivity fixture is already active.";
        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to create AI-lord fixture: " + error;
        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var prisoner, out error))
            return "Failed to create AI-lord fixture: " + error;
        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[1], out var captorHero, out error))
            return "Failed to create AI-lord fixture: " + error;

        if (prisoner.IsPrisoner)
            return $"Failed to create AI-lord fixture: '{prisoner.StringId}' is already a prisoner.";
        if (!prisoner.IsLord || prisoner.IsPlayerHero())
            return $"Failed to create AI-lord fixture: '{prisoner.StringId}' is not an AI lord.";

        var prisonerParty = prisoner.PartyBelongedTo;
        var captorParty = captorHero.PartyBelongedTo;
        if (prisonerParty?.IsActive != true)
            return $"Failed to create AI-lord fixture: '{prisoner.StringId}' has no active party.";
        if (captorParty?.IsActive != true)
            return $"Failed to create AI-lord fixture: '{captorHero.StringId}' has no active party.";
        if (prisonerParty == captorParty)
            return "Failed to create AI-lord fixture: prisoner and captor belong to the same party.";

        var prisonerFaction = prisoner.MapFaction;
        var captorFaction = captorHero.MapFaction;
        if (prisonerFaction == null || captorFaction == null || prisonerFaction == captorFaction)
            return "Failed to create AI-lord fixture: prisoner and captor need distinct map factions.";

        var prisonerIndex = prisonerParty.MemberRoster.FindIndexOfTroop(prisoner.CharacterObject);
        if (prisonerIndex < 0)
            return $"Failed to create AI-lord fixture: '{prisoner.StringId}' is absent from its party roster.";

        if (!prisonerFaction.IsAtWarWith(captorFaction))
            return "Failed to create AI-lord fixture: the prisoner and captor factions must already be at war.";

        var pendingFixture = new AiLordPeaceReleaseFixture(
            prisoner,
            captorHero,
            prisonerParty,
            captorParty,
            prisonerParty.MemberRoster.GetElementCopyAtIndex(prisonerIndex),
            prisoner.HeroState,
            prisonerParty.LeaderHero,
            prisonerParty.CurrentSettlement,
            prisonerParty.Position,
            prisonerParty.IsActive,
            captorParty.CurrentSettlement,
            captorParty.Position,
            captorParty.IsActive,
            prisonerFaction,
            captorFaction,
            StanceLinkSnapshot.Capture(prisonerFaction, captorFaction));

        fixture = pendingFixture;
        try
        {
            TakePrisonerAction.Apply(captorParty.Party, prisoner);
            if (!prisoner.IsPrisoner || prisoner.PartyBelongedToAsPrisoner != captorParty.Party)
            {
                Restore(pendingFixture);
                var verification = VerifyRestored(pendingFixture);
                if (verification != null)
                    return "Failed to create AI-lord fixture: the capture action did not establish captivity; rollback failed: " + verification;

                fixture = null;
                return "Failed to create AI-lord fixture: the capture action did not establish captivity; the baseline was restored.";
            }

            return "AI-lord captivity fixture created.\n" + Observe(fixture);
        }
        catch (Exception captureException)
        {
            try
            {
                Restore(pendingFixture);
                var verification = VerifyRestored(pendingFixture);
                if (verification != null)
                    throw new InvalidOperationException(verification);

                fixture = null;
            }
            catch (Exception restoreException)
            {
                throw new AggregateException(
                    "AI-lord fixture capture failed and rollback failed; the retained fixture can be restored manually.",
                    captureException,
                    restoreException);
            }

            throw;
        }
    }

    [CommandLineArgumentFunction("observe_ai_lord_fixture", "coop.debug.player_captivity")]
    public static string ObserveAiLordFixture(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.player_captivity.observe_ai_lord_fixture";

        return fixture == null
            ? "No AI-lord captivity fixture is active."
            : Observe(fixture);
    }

    [CommandLineArgumentFunction("focus_party", "coop.debug.player_captivity")]
    public static string FocusParty(List<string> args)
    {
        const string usage = "Usage: coop.debug.player_captivity.focus_party <mobilePartyId>";
        var context = new CommandContext("focus_party", usage, args);

        if (!context.RequireArgCount(1, out var error)) return error;
        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to focus party: " + error;
        if (!objectManager.TryGetObject(args[0], out MobileParty party) &&
            !CommandHelpers.TryGetMobileParty(args[0], out party, out error))
            return "Failed to focus party: " + error;

        party.Party.SetAsCameraFollowParty();
        return $"Following party '{party.StringId}' on the campaign map.";
    }

    [CommandLineArgumentFunction("restore_ai_lord_fixture", "coop.debug.player_captivity")]
    public static string RestoreAiLordFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "restore_ai_lord_fixture must be run on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.player_captivity.restore_ai_lord_fixture";
        if (fixture == null)
            return "No AI-lord captivity fixture is active.";

        var pendingFixture = fixture;
        Restore(pendingFixture);
        var verification = VerifyRestored(pendingFixture);
        if (verification != null)
            return "Failed to restore AI-lord captivity fixture: " + verification;

        fixture = null;
        return "AI-lord captivity fixture restored.";
    }

    private static string Observe(AiLordPeaceReleaseFixture activeFixture)
    {
        var prisoner = activeFixture.Prisoner;
        var captorParty = prisoner.PartyBelongedToAsPrisoner?.MobileParty;
        var currentParty = prisoner.PartyBelongedTo;
        var output = new StringBuilder();
        output.AppendLine("PrisonerHeroId=" + prisoner.StringId);
        output.AppendLine("CaptorHeroId=" + activeFixture.CaptorHero.StringId);
        output.AppendLine("OriginallyAtWar=" + activeFixture.StanceSnapshot.WasAtWar);
        output.AppendLine("AtWar=" + activeFixture.PrisonerFaction.IsAtWarWith(activeFixture.CaptorFaction));
        output.AppendLine("HeroState=" + prisoner.HeroState);
        output.AppendLine("IsPrisoner=" + prisoner.IsPrisoner);
        output.AppendLine("CaptorPartyId=" + (captorParty?.StringId ?? "none"));
        output.AppendLine("CaptorPrisonerCount=" + activeFixture.CaptorParty.PrisonRoster.GetTroopCount(prisoner.CharacterObject));
        output.AppendLine("HeroPartyId=" + (currentParty?.StringId ?? "none"));
        output.AppendLine("HeroPartyActive=" + (currentParty?.IsActive.ToString() ?? "none"));
        output.AppendLine("OriginalPartyId=" + activeFixture.PrisonerParty.StringId);
        output.AppendLine("OriginalPartyActive=" + activeFixture.PrisonerParty.IsActive);
        output.AppendLine("OriginalPartyHeroCount=" + activeFixture.PrisonerParty.MemberRoster.GetTroopCount(prisoner.CharacterObject));
        output.AppendLine("OriginalPartyLeaderId=" + (activeFixture.PrisonerParty.LeaderHero?.StringId ?? "none"));
        output.AppendLine("OriginalPartyPosition=" + FormatPosition(activeFixture.PrisonerPartyPosition));
        output.AppendLine("OriginalCaptorPartyPosition=" + FormatPosition(activeFixture.CaptorPartyPosition));
        output.Append("OriginalDiplomaticStateFingerprint=" + activeFixture.StanceSnapshot.OriginalFingerprint);
        return output.ToString();
    }

    private static void Restore(AiLordPeaceReleaseFixture activeFixture)
    {
        var prisoner = activeFixture.Prisoner;
        if (prisoner.IsPrisoner)
            EndCaptivityAction.ApplyByPeace(prisoner);

        var currentParty = prisoner.PartyBelongedTo;
        if (currentParty != null && currentParty != activeFixture.PrisonerParty)
            DestroyPartyAction.Apply(null, currentParty);

        RestorePartyState(
            activeFixture.PrisonerParty,
            activeFixture.PrisonerPartySettlement,
            activeFixture.PrisonerPartyPosition,
            activeFixture.PrisonerPartyWasActive);

        RemoveRosterElement(activeFixture.PrisonerParty.MemberRoster, prisoner.CharacterObject);
        activeFixture.PrisonerParty.MemberRoster.AddToCounts(
            activeFixture.PrisonerElement.Character,
            activeFixture.PrisonerElement.Number,
            false,
            activeFixture.PrisonerElement.WoundedNumber,
            activeFixture.PrisonerElement.Xp,
            true);
        prisoner.ChangeState(activeFixture.PrisonerState);
        activeFixture.PrisonerParty.ChangePartyLeader(activeFixture.PrisonerPartyLeader);

        RestorePartyState(
            activeFixture.CaptorParty,
            activeFixture.CaptorPartySettlement,
            activeFixture.CaptorPartyPosition,
            activeFixture.CaptorPartyWasActive);

        activeFixture.StanceSnapshot.Restore(true);
        PublishForcedPosition(activeFixture.PrisonerParty);
        PublishForcedPosition(activeFixture.CaptorParty);
    }

    private static void RestorePartyState(
        MobileParty party,
        Settlement originalSettlement,
        CampaignVec2 originalPosition,
        bool wasActive)
    {
        if (originalSettlement == null)
        {
            if (party.CurrentSettlement != null)
                LeaveSettlementAction.ApplyForParty(party);
            party.Position = originalPosition;
        }
        else if (party.CurrentSettlement != originalSettlement)
        {
            if (party.CurrentSettlement != null)
                LeaveSettlementAction.ApplyForParty(party);
            EnterSettlementAction.ApplyForParty(party, originalSettlement);
        }

        party.IsActive = wasActive;
    }

    private static void RemoveRosterElement(TroopRoster roster, CharacterObject character)
    {
        var index = roster.FindIndexOfTroop(character);
        if (index < 0) return;

        var element = roster.GetElementCopyAtIndex(index);
        roster.AddToCounts(character, -element.Number, false, -element.WoundedNumber, 0, true);
    }

    private static string VerifyRestored(AiLordPeaceReleaseFixture restoredFixture)
    {
        var prisoner = restoredFixture.Prisoner;
        if (prisoner.IsPrisoner) return "the hero is still a prisoner.";
        if (prisoner.HeroState != restoredFixture.PrisonerState) return "the hero state differs from the baseline.";
        if (prisoner.PartyBelongedTo != restoredFixture.PrisonerParty) return "the hero did not return to the original party.";
        if (restoredFixture.PrisonerParty.MemberRoster.GetTroopCount(prisoner.CharacterObject) != restoredFixture.PrisonerElement.Number)
            return "the original party roster was not restored.";
        if (restoredFixture.PrisonerParty.LeaderHero != restoredFixture.PrisonerPartyLeader)
            return "the original party leader was not restored.";
        if (!restoredFixture.PrisonerParty.Position.Equals(restoredFixture.PrisonerPartyPosition))
            return "the original prisoner-party position was not restored.";
        if (!restoredFixture.CaptorParty.Position.Equals(restoredFixture.CaptorPartyPosition))
            return "the original captor-party position was not restored.";
        if (restoredFixture.PrisonerParty.IsActive != restoredFixture.PrisonerPartyWasActive)
            return "the original prisoner-party activity was not restored.";
        if (restoredFixture.CaptorParty.IsActive != restoredFixture.CaptorPartyWasActive)
            return "the original captor-party activity was not restored.";
        var stanceVerification = restoredFixture.StanceSnapshot.VerifyRestored();
        if (stanceVerification != null) return stanceVerification;
        return null;
    }

    private static void PublishForcedPosition(MobileParty party)
    {
        if (!ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            throw new InvalidOperationException("Failed to resolve party-position synchronization services.");
        if (!behaviorSnapshot.TryCreate(party, out PartyBehaviorUpdateData data))
            throw new InvalidOperationException($"Failed to snapshot party behavior for '{party.StringId}'.");

        data.ForcePosition = true;
        messageBroker.Publish(typeof(AiLordPeaceReleaseFixtureCommands), new PartyBehaviorUpdated(ref data));
    }

    private static string GetDiplomaticStateFingerprint(IFaction faction1, IFaction faction2)
    {
        if (faction1 == null || faction2 == null) return "none";
        return StanceLinkSnapshot.GetFingerprint(faction1, faction2);
    }

    private static bool TryGetHeroFactions(
        IReadOnlyList<string> args,
        out IFaction prisonerFaction,
        out IFaction captorFaction,
        out string error)
    {
        prisonerFaction = null;
        captorFaction = null;
        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error)) return false;
        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var prisoner, out error)) return false;
        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[1], out var captor, out error)) return false;

        prisonerFaction = prisoner.MapFaction;
        captorFaction = captor.MapFaction;
        if (prisonerFaction == null || captorFaction == null || prisonerFaction == captorFaction)
        {
            error = "the prisoner and captor need distinct map factions.";
            return false;
        }

        error = null;
        return true;
    }

    private static string FormatPosition(CampaignVec2 position) =>
        position.X.ToString("R", CultureInfo.InvariantCulture) + "," +
        position.Y.ToString("R", CultureInfo.InvariantCulture) + "," +
        position.IsOnLand;

    private sealed class AiLordPeaceReleaseFixture
    {
        public Hero Prisoner { get; }
        public Hero CaptorHero { get; }
        public MobileParty PrisonerParty { get; }
        public MobileParty CaptorParty { get; }
        public TroopRosterElement PrisonerElement { get; }
        public Hero.CharacterStates PrisonerState { get; }
        public Hero PrisonerPartyLeader { get; }
        public Settlement PrisonerPartySettlement { get; }
        public CampaignVec2 PrisonerPartyPosition { get; }
        public bool PrisonerPartyWasActive { get; }
        public Settlement CaptorPartySettlement { get; }
        public CampaignVec2 CaptorPartyPosition { get; }
        public bool CaptorPartyWasActive { get; }
        public IFaction PrisonerFaction { get; }
        public IFaction CaptorFaction { get; }
        public StanceLinkSnapshot StanceSnapshot { get; }

        public AiLordPeaceReleaseFixture(
            Hero prisoner,
            Hero captorHero,
            MobileParty prisonerParty,
            MobileParty captorParty,
            TroopRosterElement prisonerElement,
            Hero.CharacterStates prisonerState,
            Hero prisonerPartyLeader,
            Settlement prisonerPartySettlement,
            CampaignVec2 prisonerPartyPosition,
            bool prisonerPartyWasActive,
            Settlement captorPartySettlement,
            CampaignVec2 captorPartyPosition,
            bool captorPartyWasActive,
            IFaction prisonerFaction,
            IFaction captorFaction,
            StanceLinkSnapshot stanceSnapshot)
        {
            Prisoner = prisoner;
            CaptorHero = captorHero;
            PrisonerParty = prisonerParty;
            CaptorParty = captorParty;
            PrisonerElement = prisonerElement;
            PrisonerState = prisonerState;
            PrisonerPartyLeader = prisonerPartyLeader;
            PrisonerPartySettlement = prisonerPartySettlement;
            PrisonerPartyPosition = prisonerPartyPosition;
            PrisonerPartyWasActive = prisonerPartyWasActive;
            CaptorPartySettlement = captorPartySettlement;
            CaptorPartyPosition = captorPartyPosition;
            CaptorPartyWasActive = captorPartyWasActive;
            PrisonerFaction = prisonerFaction;
            CaptorFaction = captorFaction;
            StanceSnapshot = stanceSnapshot;
        }
    }

    private sealed class StanceLinkSnapshot
    {
        private readonly IFaction faction1;
        private readonly IFaction faction2;
        private readonly StanceType stanceType;
        private readonly int behaviorPriority;
        private readonly CampaignTime warStartDate;
        private readonly CampaignTime peaceDeclarationDate;
        private readonly int troopCasualties1;
        private readonly int troopCasualties2;
        private readonly int shipCasualties1;
        private readonly int shipCasualties2;
        private readonly int successfulSieges1;
        private readonly int successfulSieges2;
        private readonly int successfulRaids1;
        private readonly int successfulRaids2;
        private readonly int totalTributePaidFrom1To2;
        private readonly int dailyTributeFrom1To2;
        private readonly int dailyTributeInstallments;
        private readonly int successfulTownSieges1;
        private readonly int successfulTownSieges2;
        private readonly int? faction1PoliticalStagnation;
        private readonly int? faction2PoliticalStagnation;

        public bool WasAtWar { get; }
        public string OriginalFingerprint { get; }

        private StanceLinkSnapshot(IFaction faction1, IFaction faction2, StanceLink stance)
        {
            this.faction1 = faction1;
            this.faction2 = faction2;
            stanceType = stance._stanceType;
            behaviorPriority = stance.BehaviorPriority;
            warStartDate = stance._warStartDate;
            peaceDeclarationDate = stance._peaceDeclarationDate;
            troopCasualties1 = stance._troopCasualties1;
            troopCasualties2 = stance._troopCasualties2;
            shipCasualties1 = stance.ShipCasualties1;
            shipCasualties2 = stance.ShipCasualties2;
            successfulSieges1 = stance._successfulSieges1;
            successfulSieges2 = stance._successfulSieges2;
            successfulRaids1 = stance._successfulRaids1;
            successfulRaids2 = stance._successfulRaids2;
            totalTributePaidFrom1To2 = stance._totalTributePaidFrom1To2;
            dailyTributeFrom1To2 = stance._dailyTributeFrom1To2;
            dailyTributeInstallments = stance._dailyTributeInstallments;
            successfulTownSieges1 = stance._successfulTownSieges1;
            successfulTownSieges2 = stance._successfulTownSieges2;
            faction1PoliticalStagnation = (faction1 as Kingdom)?.PoliticalStagnation;
            faction2PoliticalStagnation = (faction2 as Kingdom)?.PoliticalStagnation;
            WasAtWar = stance.IsAtWar;
            OriginalFingerprint = GetFingerprint(faction1, faction2);
        }

        public static StanceLinkSnapshot Capture(IFaction faction1, IFaction faction2) =>
            new(faction1, faction2, faction1.GetStanceWith(faction2));

        public void Restore(bool publishStanceChange)
        {
            var stanceChanged = faction1.IsAtWarWith(faction2) != WasAtWar;
            if (WasAtWar)
                FactionManager.DeclareWar(faction1, faction2);
            else
                FactionManager.SetNeutral(faction1, faction2);

            var stance = faction1.GetStanceWith(faction2);
            stance._stanceType = stanceType;
            stance.BehaviorPriority = behaviorPriority;
            stance._warStartDate = warStartDate;
            stance._peaceDeclarationDate = peaceDeclarationDate;
            stance._troopCasualties1 = troopCasualties1;
            stance._troopCasualties2 = troopCasualties2;
            stance.ShipCasualties1 = shipCasualties1;
            stance.ShipCasualties2 = shipCasualties2;
            stance._successfulSieges1 = successfulSieges1;
            stance._successfulSieges2 = successfulSieges2;
            stance._successfulRaids1 = successfulRaids1;
            stance._successfulRaids2 = successfulRaids2;
            stance._totalTributePaidFrom1To2 = totalTributePaidFrom1To2;
            stance._dailyTributeFrom1To2 = dailyTributeFrom1To2;
            stance._dailyTributeInstallments = dailyTributeInstallments;
            stance._successfulTownSieges1 = successfulTownSieges1;
            stance._successfulTownSieges2 = successfulTownSieges2;
            if (faction1 is Kingdom kingdom1 && faction1PoliticalStagnation.HasValue)
                kingdom1.PoliticalStagnation = faction1PoliticalStagnation.Value;
            if (faction2 is Kingdom kingdom2 && faction2PoliticalStagnation.HasValue)
                kingdom2.PoliticalStagnation = faction2PoliticalStagnation.Value;

            faction1.UpdateFactionsAtWarWith();
            faction2.UpdateFactionsAtWarWith();
            if (publishStanceChange && WasAtWar && stanceChanged)
                MessageBroker.Instance.Publish(faction1, new FactionWarDeclared(faction1, faction2, (int)DeclareWarAction.DeclareWarDetail.Default));
        }

        public string VerifyRestored()
        {
            var stance = faction1.GetStanceWith(faction2);
            return GetFingerprint(faction1, faction2) == OriginalFingerprint
                ? null
                : "the complete diplomatic state differs from the baseline.";
        }

        public static string GetFingerprint(IFaction faction1, IFaction faction2)
        {
            var stance = faction1.GetStanceWith(faction2);
            var state = new StringBuilder()
                .Append(stance._stanceType).Append('|')
                .Append(stance.BehaviorPriority).Append('|')
                .Append(stance._warStartDate).Append('|')
                .Append(stance._peaceDeclarationDate).Append('|')
                .Append(stance._troopCasualties1).Append('|')
                .Append(stance._troopCasualties2).Append('|')
                .Append(stance.ShipCasualties1).Append('|')
                .Append(stance.ShipCasualties2).Append('|')
                .Append(stance._successfulSieges1).Append('|')
                .Append(stance._successfulSieges2).Append('|')
                .Append(stance._successfulRaids1).Append('|')
                .Append(stance._successfulRaids2).Append('|')
                .Append(stance._totalTributePaidFrom1To2).Append('|')
                .Append(stance._dailyTributeFrom1To2).Append('|')
                .Append(stance._dailyTributeInstallments).Append('|')
                .Append(stance._successfulTownSieges1).Append('|')
                .Append(stance._successfulTownSieges2).Append('|')
                .Append((faction1 as Kingdom)?.PoliticalStagnation.ToString(CultureInfo.InvariantCulture) ?? "none").Append('|')
                .Append((faction2 as Kingdom)?.PoliticalStagnation.ToString(CultureInfo.InvariantCulture) ?? "none")
                .ToString();
            return Fingerprint(state);
        }

        private static string Fingerprint(string state)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(state));
            var output = new StringBuilder(hash.Length * 2);
            foreach (var value in hash) output.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return output.ToString();
        }
    }
}
#endif
