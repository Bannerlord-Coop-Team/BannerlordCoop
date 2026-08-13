using Autofac;
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Services.Missions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Stances.Handlers;
using GameInterface.Services.Stances.Messages;
using GameInterface.Services.Villages.Interfaces;
using GameInterface.Utils.Commands;
using Helpers;
using Newtonsoft.Json;
using ProtoBuf;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Villages.Commands;

public class MapEventDebugCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventDebugCommands>();
#if DEBUG
    private const string MountedBattleReceiverTroopId = "imperial_recruit";
    private const string MountedBattleOwnerTroopId = "imperial_heavy_horseman";
    private const int MountedBattleReceiverTroops = 88;
    private const int MountedBattleOwnerTroops = 153;
    private static MountedBattleFixture mountedBattleFixture;
    private static MountedBattleFixture restoredMountedBattleFixture;

    private sealed class MountedBattleFixture
    {
        public Campaign Campaign;
        public string Token;
        public MountedBattlePartySnapshot Receiver;
        public MountedBattlePartySnapshot Owner;
        public IFaction ReceiverFaction;
        public IFaction OwnerFaction;
        public MountedBattleStanceSnapshot Stance;
        public int HorsemanSlots;
        public MapEvent MapEvent;
        public bool Begun;
        public bool Restored;
    }

    private sealed class MountedBattlePartySnapshot
    {
        public string ControllerId;
        public string MobilePartyId;
        public string PartyBaseId;
        public MobileParty Party;
        public Hero Leader;
        public MapEvent OriginalMapEvent;
        public TroopRosterElement[] MemberRoster;
        public TroopRosterElement[] PrisonRoster;
        public ItemRosterElement[] ItemRoster;
        public Dictionary<Hero, int> HeroHitPoints;
        public PartyBehaviorUpdateData Behavior;
        public float RecentEventsMorale;
        public int PartyTradeGold;
        public int LeaderGold;
        public int LeaderLevel;
        public Dictionary<SkillObject, int> LeaderSkillLevels;
        public Dictionary<SkillObject, float> LeaderSkillXps;
        public int LeaderTotalXp;
        public int LeaderUnspentFocusPoints;
        public int LeaderUnspentAttributePoints;
        public Equipment OriginalBattleEquipment;
        public Equipment MountedBattleEquipment;
    }

    private sealed class MountedBattleStanceSnapshot
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
        private readonly bool faction1WasAtWarWithFaction2;
        private readonly bool faction2WasAtWarWithFaction1;

        public bool WasAtWar { get; }
        public bool StanceLinkExisted { get; }

        private MountedBattleStanceSnapshot(
            IFaction faction1,
            IFaction faction2,
            StanceLink stance,
            bool stanceLinkExisted)
        {
            this.faction1 = stanceLinkExisted ? stance.Faction1 : faction1;
            this.faction2 = stanceLinkExisted ? stance.Faction2 : faction2;
            if (stanceLinkExisted)
            {
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
            }
            faction1PoliticalStagnation =
                (this.faction1 as Kingdom)?.PoliticalStagnation;
            faction2PoliticalStagnation =
                (this.faction2 as Kingdom)?.PoliticalStagnation;
            faction1WasAtWarWithFaction2 =
                this.faction1.FactionsAtWarWith?.Contains(this.faction2) == true;
            faction2WasAtWarWithFaction1 =
                this.faction2.FactionsAtWarWith?.Contains(this.faction1) == true;
            WasAtWar = stanceLinkExisted && stance.IsAtWar;
            StanceLinkExisted = stanceLinkExisted;
        }

        public static bool TryCapture(
            IFaction faction1,
            IFaction faction2,
            out MountedBattleStanceSnapshot snapshot)
        {
            snapshot = null;
            var stances = FactionManager.Instance._stances._stances;
            bool stanceLinkExisted = stances.TryGetValue(
                GetStanceKey(faction1, faction2),
                out StanceLink stance);

            snapshot = new MountedBattleStanceSnapshot(
                faction1,
                faction2,
                stance,
                stanceLinkExisted);
            return true;
        }

        public void ApplyFixtureWarState()
        {
            ApplyStanceType(StanceType.War);
        }

        public void Restore()
        {
            if (!StanceLinkExisted)
            {
                var stances = FactionManager.Instance._stances._stances;
                stances.Remove(GetStanceKey(faction1, faction2));
                SetFactionAtWarWith(
                    faction1,
                    faction2,
                    faction1WasAtWarWithFaction2);
                SetFactionAtWarWith(
                    faction2,
                    faction1,
                    faction2WasAtWarWithFaction1);
                return;
            }

            ApplyStanceType(stanceType);
            RestoreFields();
        }

        private void ApplyStanceType(StanceType targetStanceType)
        {
            var stances = FactionManager.Instance._stances._stances;
            var key = GetStanceKey(faction1, faction2);
            if (!stances.TryGetValue(key, out StanceLink stance))
            {
                stance = FactionManager.Instance.GetStanceLinkInternal(
                    faction1,
                    faction2);
                stances[key] = stance;
            }

            stance._stanceType = targetStanceType;
            if (!StanceLinkExisted)
            {
                bool atWar = targetStanceType == StanceType.War;
                SetFactionAtWarWith(faction1, faction2, atWar);
                SetFactionAtWarWith(faction2, faction1, atWar);
                return;
            }

            faction1.UpdateFactionsAtWarWith();
            faction2.UpdateFactionsAtWarWith();
        }

        private void RestoreFields()
        {
            StanceLink stance = FactionManager.Instance.GetStanceLinkInternal(
                faction1,
                faction2);
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
            if (faction1 is Kingdom kingdom1 &&
                faction1PoliticalStagnation.HasValue)
            {
                kingdom1.PoliticalStagnation =
                    faction1PoliticalStagnation.Value;
            }
            if (faction2 is Kingdom kingdom2 &&
                faction2PoliticalStagnation.HasValue)
            {
                kingdom2.PoliticalStagnation =
                    faction2PoliticalStagnation.Value;
            }
        }

        public bool IsRestored()
        {
            var stances = FactionManager.Instance._stances._stances;
            if (!StanceLinkExisted)
            {
                return !stances.ContainsKey(GetStanceKey(faction1, faction2)) &&
                       (faction1.FactionsAtWarWith?.Contains(faction2) == true) ==
                           faction1WasAtWarWithFaction2 &&
                       (faction2.FactionsAtWarWith?.Contains(faction1) == true) ==
                           faction2WasAtWarWithFaction1;
            }
            if (!stances.TryGetValue(
                    GetStanceKey(faction1, faction2),
                    out StanceLink stance))
            {
                return false;
            }

            return stance._stanceType == stanceType &&
                   stance.BehaviorPriority == behaviorPriority &&
                   stance._warStartDate == warStartDate &&
                   stance._peaceDeclarationDate == peaceDeclarationDate &&
                   stance._troopCasualties1 == troopCasualties1 &&
                   stance._troopCasualties2 == troopCasualties2 &&
                   stance.ShipCasualties1 == shipCasualties1 &&
                   stance.ShipCasualties2 == shipCasualties2 &&
                   stance._successfulSieges1 == successfulSieges1 &&
                   stance._successfulSieges2 == successfulSieges2 &&
                   stance._successfulRaids1 == successfulRaids1 &&
                   stance._successfulRaids2 == successfulRaids2 &&
                   stance._totalTributePaidFrom1To2 ==
                       totalTributePaidFrom1To2 &&
                   stance._dailyTributeFrom1To2 == dailyTributeFrom1To2 &&
                   stance._dailyTributeInstallments ==
                       dailyTributeInstallments &&
                   stance._successfulTownSieges1 == successfulTownSieges1 &&
                   stance._successfulTownSieges2 == successfulTownSieges2 &&
                   (faction1 as Kingdom)?.PoliticalStagnation ==
                       faction1PoliticalStagnation &&
                   (faction2 as Kingdom)?.PoliticalStagnation ==
                       faction2PoliticalStagnation &&
                   AreFactionsAtWar(faction1, faction2) == WasAtWar;
        }

        public bool TryCreateFixtureWarMessage(
            string fixtureToken,
            IObjectManager objectManager,
            out NetworkRestoreMountedBattleStance message) =>
            TryCreateMessage(
                fixtureToken,
                objectManager,
                out message,
                StanceType.War,
                restoreExactSnapshot: false);

        public bool TryCreateRestoreMessage(
            string fixtureToken,
            IObjectManager objectManager,
            out NetworkRestoreMountedBattleStance message) =>
            TryCreateMessage(
                fixtureToken,
                objectManager,
                out message,
                stanceType,
                restoreExactSnapshot: true);

        private bool TryCreateMessage(
            string fixtureToken,
            IObjectManager objectManager,
            out NetworkRestoreMountedBattleStance message,
            StanceType messageStanceType,
            bool restoreExactSnapshot)
        {
            message = null;
            if (!objectManager.TryGetIdWithLogging(
                    faction1,
                    out string faction1Id) ||
                !objectManager.TryGetIdWithLogging(
                    faction2,
                    out string faction2Id))
            {
                return false;
            }

            message = new NetworkRestoreMountedBattleStance(
                fixtureToken,
                faction1Id,
                faction2Id,
                (int)messageStanceType,
                behaviorPriority,
                warStartDate.NumTicks,
                peaceDeclarationDate.NumTicks,
                troopCasualties1,
                troopCasualties2,
                shipCasualties1,
                shipCasualties2,
                successfulSieges1,
                successfulSieges2,
                successfulRaids1,
                successfulRaids2,
                totalTributePaidFrom1To2,
                dailyTributeFrom1To2,
                dailyTributeInstallments,
                successfulTownSieges1,
                successfulTownSieges2,
                faction1PoliticalStagnation.HasValue,
                faction1PoliticalStagnation.GetValueOrDefault(),
                faction2PoliticalStagnation.HasValue,
                faction2PoliticalStagnation.GetValueOrDefault(),
                restoreExactSnapshot,
                !StanceLinkExisted,
                faction1WasAtWarWithFaction2,
                faction2WasAtWarWithFaction1);
            return true;
        }

        private static (IFaction, IFaction) GetStanceKey(
            IFaction faction1,
            IFaction faction2) =>
            faction1.Id < faction2.Id
                ? (faction1, faction2)
                : (faction2, faction1);

        private static void SetFactionAtWarWith(
            IFaction faction,
            IFaction otherFaction,
            bool atWar)
        {
            if (faction is Clan clan)
            {
                if (!atWar)
                {
                    clan._factionsAtWarWith?.Remove(otherFaction);
                    return;
                }

                clan._factionsAtWarWith ??= new MBList<IFaction>();
                if (!clan._factionsAtWarWith.Contains(otherFaction))
                    clan._factionsAtWarWith.Add(otherFaction);
            }
            else if (faction is Kingdom kingdom)
            {
                if (!atWar)
                {
                    kingdom._factionsAtWarWith?.Remove(otherFaction);
                    return;
                }

                kingdom._factionsAtWarWith ??= new MBList<IFaction>();
                if (!kingdom._factionsAtWarWith.Contains(otherFaction))
                    kingdom._factionsAtWarWith.Add(otherFaction);
            }
        }
    }
#endif
    private static LateJoinModeFixture lateJoinModeFixture;

    private sealed class LateJoinModeFixture
    {
        public string MapEventId { get; set; }
        public string FirstControllerId { get; set; }
        public string FirstPlayerPartyId { get; set; }
        public string FirstPlayerMobilePartyId { get; set; }
        public PartyBehaviorUpdateData FirstPlayerBehavior { get; set; }
        public string JoiningControllerId { get; set; }
        public string JoiningPlayerPartyId { get; set; }
        public string JoiningPlayerMobilePartyId { get; set; }
        public PartyBehaviorUpdateData JoiningPlayerBehavior { get; set; }
        public string OpponentMobilePartyId { get; set; }
        public PartyBehaviorUpdateData OpponentBehavior { get; set; }
        public bool JoiningPartyJoined { get; set; }
    }

    private static WoundedAlliedFixture woundedAlliedFixture;
    private static BattleRewardFixture battleRewardFixture;
    private static PlayerFieldBattleFixture playerFieldBattleFixture;
    private static BanditAttackFixture banditAttackFixture;

    private sealed class WoundedAlliedFixture
    {
        public string ControllerId;
        public Hero PlayerHero;
        public MobileParty PlayerParty;
        public MapEvent MapEvent;
        public PartyBase[] InvolvedParties;
        public int OriginalHitPoints;
        public float OriginalRecentEventsMorale;
        public TroopRosterElement[] OriginalRoster;
        public CampaignVec2 OriginalPosition;
    }

    private sealed class BattleRewardFixture
    {
        public BattleRewardPlayerSnapshot Initiator;
        public BattleRewardPlayerSnapshot LateJoiner;
        public MobileParty BanditParty;
        public MobileParty ReinforcementParty;
        public CharacterObject BanditTroop;
        public CampaignVec2 FixturePosition;
        public MapEvent MapEvent;
        public MapEventParty InitiatorMapEventParty;
        public MapEventParty LateJoinerMapEventParty;
        public bool LateJoinerAdded;
        public bool ReinforcementAdded;
        public bool PartialRoutIssued;
        public bool EnemiesRouted;
    }

    private sealed class BattleRewardPlayerSnapshot
    {
        public string ControllerId;
        public Hero Hero;
        public MobileParty Party;
        public TroopRosterElement[] MemberRoster;
        public TroopRosterElement[] PrisonRoster;
        public ItemRosterElement[] ItemRoster;
        public PartyBehaviorUpdateData Behavior;
        public int HitPoints;
        public float RecentEventsMorale;
    }

    private sealed class PlayerFieldBattleFixture
    {
        public MobileParty AttackerParty;
        public MobileParty DefenderParty;
        public IFaction AttackerFaction;
        public IFaction DefenderFaction;
        public bool WasAtWar;
    }

    private sealed class BanditAttackFixture
    {
        public string ControllerId;
        public MobileParty PlayerParty;
        public MobileParty BanditParty;
        public Settlement PlayerSettlement;
        public Settlement BanditSettlement;
        public bool BanditWasActive;
        public TroopRosterElement[] BanditMemberRoster;
        public MapEvent MapEvent;
        public PartyBase[] InvolvedParties;
        public PartyBehaviorUpdateData PlayerBehavior;
        public PartyBehaviorUpdateData BanditBehavior;
    }

    /// <summary>
    /// Attempts to get the ObjectManager
    /// </summary>
    /// <param name="objectManager">Resolved ObjectManager, will be null if unable to resolve</param>
    /// <returns>True if ObjectManager was resolved, otherwise False</returns>
    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out objectManager);
    }

    private static bool MatchesPartyId(IObjectManager objectManager, MobileParty party, string id)
    {
        if (party == null || string.IsNullOrEmpty(id)) return false;
        if (party.StringId == id) return true;
        if (objectManager.TryGetId(party, out string mobilePartyId) && mobilePartyId == id) return true;

        return party.Party != null &&
               objectManager.TryGetId(party.Party, out string partyBaseId) &&
               partyBaseId == id;
    }

#if DEBUG
    // coop.debug.mapevent.mounted_battle_fixture_capture testclient2 imperial_recruit 88 testclient imperial_heavy_horseman 153
    /// <summary>Captures the two player parties before staging the reusable mounted engagement fixture.</summary>
    [CommandLineArgumentFunction("mounted_battle_fixture_capture", "coop.debug.mapevent")]
    public static string CaptureMountedBattleFixture(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 6)
        {
            return "Usage: coop.debug.mapevent.mounted_battle_fixture_capture " +
                   "<receiverControllerId> <receiverTroopId> <receiverHealthyTroops> " +
                   "<ownerControllerId> <ownerTroopId> <ownerHealthyRiders>";
        }
        if (args[0] != "testclient2" || args[1] != MountedBattleReceiverTroopId ||
            args[3] != "testclient" || args[4] != MountedBattleOwnerTroopId ||
            !int.TryParse(args[2], out var receiverTroops) || receiverTroops != MountedBattleReceiverTroops ||
            !int.TryParse(args[5], out var ownerTroops) || ownerTroops != MountedBattleOwnerTroops)
        {
            return "This fixture requires testclient2 with 88 imperial_recruit and testclient with " +
                   "153 imperial_heavy_horseman.";
        }
        if (mountedBattleFixture != null)
        {
            bool sameUnbegunFixture = !mountedBattleFixture.Begun &&
                                      mountedBattleFixture.Campaign == Campaign.Current &&
                                      mountedBattleFixture.Receiver.ControllerId == args[0] &&
                                      mountedBattleFixture.Owner.ControllerId == args[3];
            return sameUnbegunFixture
                ? FormatMountedBattleFixture("already-captured", mountedBattleFixture, null)
                : "A mounted battle fixture is already active. Restore it before capturing another.";
        }
        if (!TryGetObjectManager(out var objectManager) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
            !ContainerProvider.TryResolve<IBattleAgentBudget>(out var agentBudget))
        {
            return "Unable to resolve the mounted battle fixture services.";
        }
        if (!TryCaptureMountedBattleParty(
                args[0], objectManager, playerManager, behaviorSnapshot, out var receiver, out var error) ||
            !TryCaptureMountedBattleParty(
                args[3], objectManager, playerManager, behaviorSnapshot, out var owner, out error))
        {
            return error;
        }
        if (receiver.Party == owner.Party)
            return "The mounted battle fixture requires two different player parties.";
        if (receiver.Party.MapFaction == owner.Party.MapFaction)
            return "The mounted battle fixture players must have different map factions.";
        if (!MountedBattleStanceSnapshot.TryCapture(
                receiver.Party.MapFaction,
                owner.Party.MapFaction,
                out var stance))
            return "Unable to capture the mounted battle diplomatic stance.";
        if (!objectManager.TryGetObjectWithLogging(MountedBattleReceiverTroopId, out CharacterObject receiverTroop) ||
            !objectManager.TryGetObjectWithLogging(MountedBattleOwnerTroopId, out CharacterObject ownerTroop))
        {
            return "Unable to resolve the mounted battle fixture troops.";
        }
        int horsemanSlots = agentBudget.SlotsForEquipment(ownerTroop.Equipment);
        if (horsemanSlots != 2)
            return "imperial_heavy_horseman does not have mounted equipment with two battle-agent slots.";

        mountedBattleFixture = new MountedBattleFixture
        {
            Campaign = Campaign.Current,
            Token = Guid.NewGuid().ToString("N"),
            Receiver = receiver,
            Owner = owner,
            ReceiverFaction = receiver.Party.MapFaction,
            OwnerFaction = owner.Party.MapFaction,
            Stance = stance,
            HorsemanSlots = horsemanSlots,
        };
        restoredMountedBattleFixture = null;
        return FormatMountedBattleFixture("captured", mountedBattleFixture, null);
    }

    // coop.debug.mapevent.mounted_battle_fixture_begin <fixtureToken>
    /// <summary>Stages the captured rosters and starts a real player-party hostile encounter.</summary>
    [CommandLineArgumentFunction("mounted_battle_fixture_begin", "coop.debug.mapevent")]
    public static string BeginMountedBattleFixture(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.mounted_battle_fixture_begin <fixtureToken>";
        if (!TryGetMountedBattleFixture(args[0], out var fixture, out var error))
            return error;
        if (fixture.Begun)
            return FormatMountedBattleFixture("already-begun", fixture, null);
        if (fixture.Campaign != Campaign.Current)
            return "The mounted battle fixture belongs to a previous campaign.";
        if (!TryGetObjectManager(out var objectManager) ||
            !ContainerProvider.TryResolve<INetwork>(out var network) ||
            !ContainerProvider.TryResolve<IBattleAgentBudget>(out var agentBudget))
        {
            return "Unable to resolve the mounted battle encounter services.";
        }
        if (!TryResolveMountedBattleParty(objectManager, fixture.Receiver, out error) ||
            !TryResolveMountedBattleParty(objectManager, fixture.Owner, out error))
        {
            return error;
        }
        if (!CanStageMountedBattleParty(fixture.Receiver.Party) || !CanStageMountedBattleParty(fixture.Owner.Party))
            return "Both captured player parties must be active on the map, outside settlements and map events.";
        if (!objectManager.TryGetObjectWithLogging(MountedBattleReceiverTroopId, out CharacterObject receiverTroop) ||
            !objectManager.TryGetObjectWithLogging(MountedBattleOwnerTroopId, out CharacterObject ownerTroop))
        {
            return "Unable to resolve the mounted battle fixture troops.";
        }
        fixture.HorsemanSlots = agentBudget.SlotsForEquipment(ownerTroop.Equipment);
        if (fixture.HorsemanSlots != 2)
            return "imperial_heavy_horseman no longer has mounted equipment with two battle-agent slots.";

        try
        {
            if (!objectManager.TryGetId(ownerTroop.Equipment, out _))
            {
                throw new InvalidOperationException(
                    "The mounted joust equipment was not registered.");
            }

            fixture.Receiver.MountedBattleEquipment = ownerTroop.Equipment;
            fixture.Receiver.Leader._battleEquipment =
                fixture.Receiver.MountedBattleEquipment;

            StageMountedBattleParty(fixture.Receiver, receiverTroop, MountedBattleReceiverTroops, fixture.Receiver.Party.Position);
            var ownerPosition = new CampaignVec2(
                new Vec2(fixture.Receiver.Party.Position.X - 0.2f, fixture.Receiver.Party.Position.Y),
                fixture.Receiver.Party.Position.IsOnLand);
            StageMountedBattleParty(fixture.Owner, ownerTroop, MountedBattleOwnerTroops, ownerPosition);

            fixture.Stance.ApplyFixtureWarState();
            if (!fixture.Stance.TryCreateFixtureWarMessage(
                    fixture.Token,
                    objectManager,
                    out NetworkRestoreMountedBattleStance fixtureWar))
            {
                throw new InvalidOperationException(
                    "Unable to create the mounted battle fixture stance message.");
            }
            network.SendAll(fixtureWar);
            fixture.MapEvent = MapEventBattleFactory.CreateMapEvent(
                fixture.Receiver.Party.Party,
                fixture.Owner.Party.Party,
                default);
            if (fixture.MapEvent == null)
            {
                throw new InvalidOperationException(
                    "The mounted battle fixture could not create a field map event.");
            }

            if (fixture.MapEvent == null || fixture.Owner.Party.MapEvent != fixture.MapEvent)
                throw new InvalidOperationException("The hostile encounter did not create a shared map event.");
            if (!objectManager.TryGetIdWithLogging(
                    fixture.MapEvent,
                    out string mapEventId))
            {
                throw new InvalidOperationException(
                    "The mounted battle fixture map event was not registered.");
            }
            network.SendAll(new NetworkPlayerPartyHostileEncounterStarted(
                "debug-2983-" + fixture.Token,
                fixture.Receiver.PartyBaseId,
                fixture.Owner.PartyBaseId,
                mapEventId));

            fixture.Begun = true;
            return FormatMountedBattleFixture("begun", fixture, null);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Failed to begin mounted battle fixture {FixtureToken}", fixture.Token);
            fixture.MapEvent ??= fixture.Receiver.Party.MapEvent ?? fixture.Owner.Party.MapEvent;
            bool restored = TryRestoreMountedBattleFixture(fixture, objectManager, out var restoreError);
            if (restored)
            {
                mountedBattleFixture = null;
                restoredMountedBattleFixture = fixture;
            }

            return FormatMountedBattleFixture(
                restored ? "begin-failed-restored" : "begin-failed-restore-pending",
                fixture,
                restoreError ?? exception.Message);
        }
    }

    // coop.debug.mapevent.mounted_battle_fixture_state <fixtureToken>
    /// <summary>Reports the staged roster counts, mount slot count, and shared map-event state.</summary>
    [CommandLineArgumentFunction("mounted_battle_fixture_state", "coop.debug.mapevent")]
    public static string GetMountedBattleFixtureState(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.mounted_battle_fixture_state <fixtureToken>";
        if (!TryGetMountedBattleFixture(args[0], out var fixture, out var error))
            return error;

        return FormatMountedBattleFixture(fixture.Begun ? "begun" : "captured", fixture, null);
    }

    // coop.debug.mapevent.mounted_battle_fixture_restore <fixtureToken>
    /// <summary>Finalizes the fixture map event and restores both captured player parties exactly.</summary>
    [CommandLineArgumentFunction("mounted_battle_fixture_restore", "coop.debug.mapevent")]
    public static string RestoreMountedBattleFixture(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.mounted_battle_fixture_restore <fixtureToken>";
        if (restoredMountedBattleFixture != null && restoredMountedBattleFixture.Token == args[0])
            return FormatMountedBattleFixture("restored", restoredMountedBattleFixture, null);
        if (!TryGetMountedBattleFixture(args[0], out var fixture, out var error))
            return error;
        if (!TryGetObjectManager(out var objectManager))
            return "Unable to resolve ObjectManager.";

        if (!TryRestoreMountedBattleFixture(fixture, objectManager, out error))
            return FormatMountedBattleFixture("restore-pending", fixture, error);

        mountedBattleFixture = null;
        restoredMountedBattleFixture = fixture;
        return FormatMountedBattleFixture("restored", fixture, null);
    }

    // coop.debug.mapevent.mounted_battle_fixture_verify <fixtureToken>
    /// <summary>Verifies the captured map state is back after the mounted battle fixture is restored.</summary>
    [CommandLineArgumentFunction("mounted_battle_fixture_verify", "coop.debug.mapevent")]
    public static string VerifyMountedBattleFixture(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.mounted_battle_fixture_verify <fixtureToken>";
        if (restoredMountedBattleFixture != null && restoredMountedBattleFixture.Token == args[0])
        {
            bool verified = IsMountedBattleFixtureRestored(restoredMountedBattleFixture);
            restoredMountedBattleFixture.Restored = verified;
            return FormatMountedBattleFixture(
                verified ? "verified" : "verification-failed",
                restoredMountedBattleFixture,
                verified ? null : "The current state no longer matches the captured baseline.");
        }
        if (mountedBattleFixture != null && mountedBattleFixture.Token == args[0])
            return FormatMountedBattleFixture("not-restored", mountedBattleFixture, "Restore the fixture before verifying it.");

        return "No mounted battle fixture exists for the supplied token.";
    }

    // coop.debug.mapevent.mounted_battle_stance_restore_state <fixtureToken>
    /// <summary>Verifies this peer applied the exact pre-fixture diplomatic snapshot.</summary>
    [CommandLineArgumentFunction(
        "mounted_battle_stance_restore_state",
        "coop.debug.mapevent")]
    public static string GetMountedBattleStanceRestoreState(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.mounted_battle_stance_restore_state " +
                   "<fixtureToken>";
        }
        if (!ContainerProvider.TryResolve<FactionStanceHandler>(out var handler))
            return "Unable to resolve the faction-stance handler.";

        bool applied = handler.TryGetMountedBattleStanceRestoreState(
            args[0],
            out NetworkRestoreMountedBattleStance restore,
            out bool matches);
        var result = new
        {
            token = args[0],
            applied,
            matches,
            faction1Id = restore?.Faction1Id,
            faction2Id = restore?.Faction2Id,
        };
        return "Mounted battle stance restore state." + Environment.NewLine +
               "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(result);
    }

    private static bool TryCaptureMountedBattleParty(
        string controllerId,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        out MountedBattlePartySnapshot snapshot,
        out string error)
    {
        snapshot = null;
        error = null;
        if (!playerManager.TryGetPlayer(controllerId, out var player) || !playerManager.IsConnected(player))
        {
            error = $"Player {controllerId} must be registered and connected.";
            return false;
        }
        if (!TryGetPlayerParty(controllerId, requireReady: true, out _, out var party, out error))
            return false;
        if (!CanStageMountedBattleParty(party))
        {
            error = $"Player {controllerId} must lead an active party on the map, outside settlements and map events.";
            return false;
        }
        if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
            party.LeaderHero != hero || hero.PartyBelongedTo != party)
        {
            error = $"Player {controllerId} must be leading their registered player party.";
            return false;
        }
        if (!objectManager.TryGetId(party.Party, out string partyBaseId) ||
            !objectManager.TryGetId(party, out string mobilePartyId) ||
            !behaviorSnapshot.TryCreate(party, out var behavior))
        {
            error = $"Unable to capture registered party state for {controllerId}.";
            return false;
        }

        var memberRoster = party.MemberRoster.GetTroopRoster().ToArray();
        var prisonRoster = party.PrisonRoster.GetTroopRoster().ToArray();
        var heroHitPoints = memberRoster
            .Concat(prisonRoster)
            .Where(element => element.Character.IsHero)
            .Select(element => element.Character.HeroObject)
            .Concat(new[] { hero })
            .Where(candidate => candidate != null)
            .Distinct()
            .ToDictionary(candidate => candidate, candidate => candidate.HitPoints);

        snapshot = new MountedBattlePartySnapshot
        {
            ControllerId = controllerId,
            MobilePartyId = mobilePartyId,
            PartyBaseId = partyBaseId,
            Party = party,
            Leader = hero,
            OriginalMapEvent = party.MapEvent,
            MemberRoster = memberRoster,
            PrisonRoster = prisonRoster,
            ItemRoster = party.ItemRoster.ToArray(),
            HeroHitPoints = heroHitPoints,
            Behavior = behavior,
            RecentEventsMorale = party.RecentEventsMorale,
            PartyTradeGold = party.PartyTradeGold,
            LeaderGold = hero.Gold,
            LeaderLevel = hero.Level,
            LeaderSkillLevels = Skills.All.ToDictionary(
                skill => skill,
                hero.GetSkillValue),
            LeaderSkillXps = hero.HeroDeveloper == null
                ? null
                : Skills.All.ToDictionary(
                    skill => skill,
                    hero.HeroDeveloper.GetSkillXp),
            LeaderTotalXp = hero.HeroDeveloper?._totalXp ?? 0,
            LeaderUnspentFocusPoints =
                hero.HeroDeveloper?.UnspentFocusPoints ?? 0,
            LeaderUnspentAttributePoints =
                hero.HeroDeveloper?.UnspentAttributePoints ?? 0,
            OriginalBattleEquipment = hero._battleEquipment,
        };
        return true;
    }

    private static bool TryGetMountedBattleFixture(string token, out MountedBattleFixture fixture, out string error)
    {
        fixture = mountedBattleFixture;
        error = null;
        if (fixture == null || fixture.Token != token)
        {
            error = "No active mounted battle fixture exists for the supplied token.";
            return false;
        }

        return true;
    }

    private static bool TryResolveMountedBattleParty(
        IObjectManager objectManager,
        MountedBattlePartySnapshot snapshot,
        out string error)
    {
        error = null;
        if (!objectManager.TryGetObjectWithLogging(snapshot.MobilePartyId, out MobileParty party))
        {
            error = $"The captured party for {snapshot.ControllerId} is no longer available.";
            return false;
        }

        snapshot.Party = party;
        return true;
    }

    private static bool CanStageMountedBattleParty(MobileParty party) =>
        party != null &&
        party.IsActive &&
        party.Party != null &&
        party.MapEvent == null &&
        party.CurrentSettlement == null &&
        party.MapFaction != null;

    private static void StageMountedBattleParty(
        MountedBattlePartySnapshot snapshot,
        CharacterObject troop,
        int healthyTroops,
        CampaignVec2 position)
    {
        RestoreTroopRoster(snapshot.Party.MemberRoster, Array.Empty<TroopRosterElement>());
        RestoreTroopRoster(snapshot.Party.PrisonRoster, Array.Empty<TroopRosterElement>());
        snapshot.Party.MemberRoster.AddToCounts(snapshot.Leader.CharacterObject, 1, insertAtFront: true);
        snapshot.Party.MemberRoster.AddToCounts(troop, healthyTroops);
        snapshot.Leader.HitPoints = snapshot.Leader.MaxHitPoints;
        snapshot.Party.Position = position;
        snapshot.Party.SetMoveModeHold();
        snapshot.Party.ResetNavigationToHold();
        MessageBroker.Instance.Publish(
            typeof(MapEventDebugCommands),
            new PartyBehaviorChangeAttempted(
                snapshot.Party,
                forcePosition: true,
                isCurrentlyAtSea: false,
                resetMovementToHold: true));
    }

    private static bool TryRestoreMountedBattleFixture(
        MountedBattleFixture fixture,
        IObjectManager objectManager,
        out string error)
    {
        if (!ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var missionMembership))
        {
            fixture.Restored = false;
            error = "Unable to resolve the mission-membership registry.";
            return false;
        }

        var activeMissionControllers = new[]
        {
            fixture.Receiver.ControllerId,
            fixture.Owner.ControllerId,
        }.Where(missionMembership.IsControllerInMission).ToArray();
        if (activeMissionControllers.Length > 0)
        {
            fixture.Restored = false;
            error = "End the fixture missions before restoring: " +
                    string.Join(", ", activeMissionControllers) + ".";
            return false;
        }

        var failures = new List<string>();
        TryFinalizeMountedBattleMapEvents(fixture, failures);
        TryRestoreMountedBattleEquipment(fixture, failures);
        if (TryResolveMountedBattleParty(objectManager, fixture.Receiver, out var receiverError))
        {
            try
            {
                RestoreMountedBattleParty(fixture.Receiver);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Failed to restore receiver mounted battle state for fixture {FixtureToken}", fixture.Token);
                failures.Add(exception.Message);
            }
        }
        else
        {
            failures.Add(receiverError);
        }
        if (TryResolveMountedBattleParty(objectManager, fixture.Owner, out var ownerError))
        {
            try
            {
                RestoreMountedBattleParty(fixture.Owner);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Failed to restore owner mounted battle state for fixture {FixtureToken}", fixture.Token);
                failures.Add(exception.Message);
            }
        }
        else
        {
            failures.Add(ownerError);
        }

        try
        {
                RestoreMountedBattleWarState(fixture, objectManager);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Failed to restore mounted battle war state for fixture {FixtureToken}", fixture.Token);
            failures.Add(exception.Message);
        }

        fixture.Restored = failures.Count == 0 && IsMountedBattleFixtureRestored(fixture);
        if (!fixture.Restored && failures.Count == 0)
            failures.Add("The restored state does not match the captured baseline.");

        error = failures.Count == 0 ? null : string.Join(" ", failures);
        return fixture.Restored;
    }

    private static void TryRestoreMountedBattleEquipment(
        MountedBattleFixture fixture,
        List<string> failures)
    {
        if (fixture.Receiver.MountedBattleEquipment == null)
            return;

        try
        {
            fixture.Receiver.Leader._battleEquipment =
                fixture.Receiver.OriginalBattleEquipment;
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Failed to restore joust equipment for mounted battle fixture {FixtureToken}",
                fixture.Token);
            failures.Add(exception.Message);
        }
    }

    private static void TryFinalizeMountedBattleMapEvents(MountedBattleFixture fixture, List<string> failures)
    {
        var mapEvents = new HashSet<MapEvent>();
        if (fixture.MapEvent != null) mapEvents.Add(fixture.MapEvent);
        if (fixture.Receiver.Party?.MapEvent != null) mapEvents.Add(fixture.Receiver.Party.MapEvent);
        if (fixture.Owner.Party?.MapEvent != null) mapEvents.Add(fixture.Owner.Party.MapEvent);

        foreach (var mapEvent in mapEvents)
        {
            try
            {
                if (!mapEvent.IsFinalized)
                    mapEvent.FinalizeEvent();
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Failed to finalize mounted battle fixture map event {MapEventId}", mapEvent.StringId);
                failures.Add(exception.Message);
            }
        }
    }

    private static void RestoreMountedBattleParty(MountedBattlePartySnapshot snapshot)
    {
        RestoreTroopRoster(snapshot.Party.MemberRoster, snapshot.MemberRoster);
        RestoreTroopRoster(snapshot.Party.PrisonRoster, snapshot.PrisonRoster);
        snapshot.Party.ItemRoster.Clear();
        foreach (var element in snapshot.ItemRoster)
            snapshot.Party.ItemRoster.AddToCounts(
                element.EquipmentElement,
                element.Amount);
        foreach (var hero in snapshot.HeroHitPoints)
        {
            if (!hero.Key.IsDead)
                hero.Key.HitPoints = hero.Value;
        }
        snapshot.Leader.Gold = snapshot.LeaderGold;
        RestoreMountedBattleHeroProgression(snapshot);
        snapshot.Party.RecentEventsMorale = snapshot.RecentEventsMorale;
        snapshot.Party.PartyTradeGold = snapshot.PartyTradeGold;
        if (snapshot.Party.LeaderHero != snapshot.Leader)
            snapshot.Party.ChangePartyLeader(snapshot.Leader);
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            throw new InvalidOperationException("Unable to resolve the mobile-party behavior snapshot service.");

        snapshot.Party.Position = snapshot.Behavior.PartyPosition;
        snapshot.Party.IsCurrentlyAtSea = snapshot.Behavior.IsCurrentlyAtSea;
        if (!behaviorSnapshot.TryApply(snapshot.Party, snapshot.Behavior, out _))
            throw new InvalidOperationException($"Unable to restore party behavior for {snapshot.ControllerId}.");

        MessageBroker.Instance.Publish(
            typeof(MapEventDebugCommands),
            new PartyBehaviorChangeAttempted(
                snapshot.Party,
                forcePosition: true,
                isCurrentlyAtSea: snapshot.Behavior.IsCurrentlyAtSea));
    }

    private static void RestoreMountedBattleHeroProgression(
        MountedBattlePartySnapshot snapshot)
    {
        foreach (var skill in snapshot.LeaderSkillLevels)
            snapshot.Leader.SetSkillValue(skill.Key, skill.Value);
        snapshot.Leader.Level = snapshot.LeaderLevel;

        if (snapshot.Leader.HeroDeveloper == null ||
            snapshot.LeaderSkillXps == null)
        {
            return;
        }

        foreach (var skillXp in snapshot.LeaderSkillXps)
        {
            snapshot.Leader.HeroDeveloper.SetSkillXp(
                skillXp.Key,
                skillXp.Value);
        }
        snapshot.Leader.HeroDeveloper._totalXp = snapshot.LeaderTotalXp;
        snapshot.Leader.HeroDeveloper.UnspentFocusPoints =
            snapshot.LeaderUnspentFocusPoints;
        snapshot.Leader.HeroDeveloper.UnspentAttributePoints =
            snapshot.LeaderUnspentAttributePoints;
    }

    private static void RestoreMountedBattleWarState(
        MountedBattleFixture fixture,
        IObjectManager objectManager)
    {
        if (!fixture.Stance.TryCreateRestoreMessage(
                fixture.Token,
                objectManager,
                out NetworkRestoreMountedBattleStance restore) ||
            !ContainerProvider.TryResolve<INetwork>(out var network))
        {
            throw new InvalidOperationException(
                "Unable to create the mounted battle stance restore message.");
        }

        fixture.Stance.Restore();
        network.SendAll(restore);
    }

    private static bool IsMountedBattleFixtureRestored(MountedBattleFixture fixture) =>
        IsMountedBattlePartyRestored(fixture.Receiver) &&
        IsMountedBattlePartyRestored(fixture.Owner) &&
        fixture.Receiver.Leader?._battleEquipment ==
            fixture.Receiver.OriginalBattleEquipment &&
        fixture.Receiver.Party?.MapEvent == fixture.Receiver.OriginalMapEvent &&
        fixture.Owner.Party?.MapEvent == fixture.Owner.OriginalMapEvent &&
        (fixture.MapEvent == null || fixture.MapEvent.IsFinalized || fixture.MapEvent == fixture.Receiver.OriginalMapEvent) &&
        fixture.Stance.IsRestored();

    private static bool IsMountedBattlePartyRestored(MountedBattlePartySnapshot snapshot)
    {
        if (snapshot.Party == null ||
            snapshot.Party.LeaderHero != snapshot.Leader ||
            !RosterMatches(snapshot.Party.MemberRoster, snapshot.MemberRoster) ||
            !RosterMatches(snapshot.Party.PrisonRoster, snapshot.PrisonRoster) ||
            !ItemRosterMatches(snapshot.Party.ItemRoster, snapshot.ItemRoster) ||
            snapshot.Party.RecentEventsMorale != snapshot.RecentEventsMorale ||
            snapshot.Party.PartyTradeGold != snapshot.PartyTradeGold ||
            snapshot.Leader.Gold != snapshot.LeaderGold ||
            !MountedBattleHeroProgressionMatches(snapshot) ||
            snapshot.HeroHitPoints.Any(hero => hero.Key.IsDead || hero.Key.HitPoints != hero.Value))
        {
            return false;
        }
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
            !behaviorSnapshot.TryCreate(snapshot.Party, out var behavior))
        {
            return false;
        }

        return BehaviorMatches(snapshot.Behavior, behavior);
    }

    private static bool MountedBattleHeroProgressionMatches(
        MountedBattlePartySnapshot snapshot)
    {
        if (snapshot.Leader.Level != snapshot.LeaderLevel ||
            snapshot.LeaderSkillLevels.Any(skill =>
                snapshot.Leader.GetSkillValue(skill.Key) != skill.Value))
        {
            return false;
        }
        if (snapshot.Leader.HeroDeveloper == null)
            return snapshot.LeaderSkillXps == null;
        if (snapshot.LeaderSkillXps == null ||
            snapshot.LeaderSkillXps.Any(skill =>
                snapshot.Leader.HeroDeveloper.GetSkillXp(skill.Key) !=
                skill.Value))
        {
            return false;
        }

        return snapshot.Leader.HeroDeveloper._totalXp ==
                   snapshot.LeaderTotalXp &&
               snapshot.Leader.HeroDeveloper.UnspentFocusPoints ==
                   snapshot.LeaderUnspentFocusPoints &&
               snapshot.Leader.HeroDeveloper.UnspentAttributePoints ==
                   snapshot.LeaderUnspentAttributePoints;
    }

    private static bool RosterMatches(TroopRoster roster, TroopRosterElement[] expected) =>
        RosterFingerprint(roster) == RosterFingerprint(expected);

    private static string RosterFingerprint(TroopRoster roster) =>
        RosterFingerprint(roster.GetTroopRoster().ToArray());

    private static string RosterFingerprint(IEnumerable<TroopRosterElement> roster) =>
        string.Join(";", roster
            .OrderBy(element => element.Character.StringId, StringComparer.Ordinal)
            .Select(element => $"{element.Character.StringId}|{element.Number}|{element.WoundedNumber}|{element.Xp}"));

    private static bool ItemRosterMatches(
        ItemRoster roster,
        IEnumerable<ItemRosterElement> expected) =>
        ItemRosterFingerprint(roster) == ItemRosterFingerprint(expected);

    private static string ItemRosterFingerprint(
        IEnumerable<ItemRosterElement> roster) =>
        string.Join(";", roster
            .OrderBy(element => element.EquipmentElement.Item?.StringId,
                StringComparer.Ordinal)
            .ThenBy(element =>
                element.EquipmentElement.ItemModifier?.StringId,
                StringComparer.Ordinal)
            .Select(element =>
                $"{element.EquipmentElement.Item?.StringId}|" +
                $"{element.EquipmentElement.ItemModifier?.StringId}|" +
                element.Amount));

    private static bool BehaviorMatches(PartyBehaviorUpdateData expected, PartyBehaviorUpdateData actual) =>
        expected.MobilePartyId == actual.MobilePartyId &&
        expected.NewAiBehavior == actual.NewAiBehavior &&
        expected.InteractablePointId == actual.InteractablePointId &&
        CampaignVec2Matches(expected.BestTargetPoint, actual.BestTargetPoint) &&
        CampaignVec2Matches(expected.PartyPosition, actual.PartyPosition) &&
        expected.DefaultBehavior == actual.DefaultBehavior &&
        CampaignVec2Matches(expected.TargetPosition, actual.TargetPosition) &&
        expected.DesiredAiNavigationType == actual.DesiredAiNavigationType &&
        expected.TargetPartyId == actual.TargetPartyId &&
        expected.TargetSettlementId == actual.TargetSettlementId &&
        CampaignVec2Matches(expected.MoveTargetPoint, actual.MoveTargetPoint) &&
        expected.IsTargetingPort == actual.IsTargetingPort &&
        expected.PartyMoveMode == actual.PartyMoveMode &&
        expected.MoveTargetPartyId == actual.MoveTargetPartyId &&
        expected.IsInteractableAnchor == actual.IsInteractableAnchor &&
        expected.IsCurrentlyAtSea == actual.IsCurrentlyAtSea;

    private static bool CampaignVec2Matches(CampaignVec2 first, CampaignVec2 second) =>
        first.X == second.X && first.Y == second.Y && first.IsOnLand == second.IsOnLand;

    private static int HealthyTroopCount(TroopRoster roster, string troopId)
    {
        TroopRosterElement element = roster.GetTroopRoster()
            .FirstOrDefault(candidate => candidate.Character.StringId == troopId);
        return element.Character == null ? 0 : element.Number - element.WoundedNumber;
    }

    private static string FormatMountedBattleFixture(string state, MountedBattleFixture fixture, string error)
    {
        var mapEvent = fixture.Receiver.Party?.MapEvent ?? fixture.Owner.Party?.MapEvent ?? fixture.MapEvent;
        string mapEventId = "none";
        if (mapEvent != null && TryGetObjectManager(out var objectManager) && objectManager.TryGetId(mapEvent, out string resolvedMapEventId))
            mapEventId = resolvedMapEventId;

        var receiver = fixture.Receiver.Party;
        var owner = fixture.Owner.Party;
        string[] activeMissionControllers = GetActiveMissionControllers(fixture);
        var result = new
        {
            token = fixture.Token,
            state,
            begun = fixture.Begun,
            restored = fixture.Restored,
            error,
            mapEventId,
            sharedMapEvent = receiver?.MapEvent != null && receiver.MapEvent == owner?.MapEvent,
            receiverControllerId = fixture.Receiver.ControllerId,
            receiverHealthyRecruits = receiver == null ? 0 : HealthyTroopCount(receiver.MemberRoster, MountedBattleReceiverTroopId),
            ownerControllerId = fixture.Owner.ControllerId,
            ownerHealthyHorsemen = owner == null ? 0 : HealthyTroopCount(owner.MemberRoster, MountedBattleOwnerTroopId),
            horsemanSlots = fixture.HorsemanSlots,
            joustEquipmentStaged =
                fixture.Receiver.MountedBattleEquipment != null &&
                fixture.Receiver.Leader?._battleEquipment ==
                    fixture.Receiver.MountedBattleEquipment,
            receiverBattleEquipmentRestored =
                fixture.Receiver.Leader?._battleEquipment ==
                    fixture.Receiver.OriginalBattleEquipment,
            activeMissionControllerIds = activeMissionControllers,
            receiverRosterRestored = IsMountedBattlePartyRestored(fixture.Receiver),
            ownerRosterRestored = IsMountedBattlePartyRestored(fixture.Owner),
            mapEventRestored = receiver?.MapEvent == null && owner?.MapEvent == null &&
                               (fixture.MapEvent == null || fixture.MapEvent.IsFinalized),
            diplomaticStateRestored = fixture.Stance.IsRestored(),
            receiverProgressionRestored =
                MountedBattleHeroProgressionMatches(fixture.Receiver),
            ownerProgressionRestored =
                MountedBattleHeroProgressionMatches(fixture.Owner),
        };
        return $"Mounted battle fixture {state}." + Environment.NewLine +
               "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(result);
    }

    private static string[] GetActiveMissionControllers(
        MountedBattleFixture fixture)
    {
        if (!ContainerProvider.TryResolve<IMissionMembershipRegistry>(
                out var missionMembership))
        {
            return Array.Empty<string>();
        }

        return new[]
        {
            fixture.Receiver.ControllerId,
            fixture.Owner.ControllerId,
        }.Where(missionMembership.IsControllerInMission).ToArray();
    }
#endif

    [CommandLineArgumentFunction("start_player_field_battle", "coop.debug.mapevent")]
    public static string StartPlayerFieldBattle(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";

        if (args.Count != 2)
            return "Usage: coop.debug.mapevent.start_player_field_battle <attackerMobilePartyId> <defenderMobilePartyId>";

        if (playerFieldBattleFixture != null)
            return "A player field-battle fixture is already pending restoration.";

        if (!TryGetObjectManager(out var objectManager))
            return "Unable to resolve ObjectManager.";

        var attackerError = string.Empty;
        if ((!objectManager.TryGetObject(args[0], out MobileParty attacker) &&
             !CommandHelpers.TryGetMobileParty(args[0], out attacker, out attackerError)) ||
            attacker?.Party == null)
            return "Unable to resolve attacker party: " + attackerError;

        var defenderError = string.Empty;
        if ((!objectManager.TryGetObject(args[1], out MobileParty defender) &&
             !CommandHelpers.TryGetMobileParty(args[1], out defender, out defenderError)) ||
            defender?.Party == null)
            return "Unable to resolve defender party: " + defenderError;

        if (attacker == defender)
            return "Attacker and defender parties must be distinct.";

        if (!attacker.IsActive || !defender.IsActive || attacker.MapEvent != null || defender.MapEvent != null)
            return "Both player parties must be active and outside a map event.";

        if (attacker.CurrentSettlement != null || defender.CurrentSettlement != null)
            return "Both player parties must be outside settlements.";

        var attackerFaction = attacker.MapFaction;
        var defenderFaction = defender.MapFaction;
        if (attackerFaction == null || defenderFaction == null || attackerFaction == defenderFaction)
            return "Player parties must belong to distinct map factions.";

        if (!objectManager.TryGetId(attacker, out var attackerMobilePartyId) ||
            !objectManager.TryGetId(defender, out var defenderMobilePartyId) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return "Unable to resolve the registered player-party identities.";

        var attackerPlayer = playerManager.Players.FirstOrDefault(player =>
            player.MobilePartyId == attackerMobilePartyId);
        var defenderPlayer = playerManager.Players.FirstOrDefault(player =>
            player.MobilePartyId == defenderMobilePartyId);
        if (attackerPlayer == null || defenderPlayer == null ||
            !playerManager.IsConnected(attackerPlayer) || !playerManager.IsConnected(defenderPlayer))
            return "Both parties must belong to connected players.";

        if (!objectManager.TryGetId(attacker.Party, out var attackerPartyBaseId) ||
            !objectManager.TryGetId(defender.Party, out var defenderPartyBaseId))
            return "Unable to resolve the registered PartyBase ids.";

        if (!ContainerProvider.TryResolve<IPlayerPartyHostileEncounterService>(out var encounterService))
            return "Unable to resolve the player hostile-encounter service.";

        var fixture = new PlayerFieldBattleFixture
        {
            AttackerParty = attacker,
            DefenderParty = defender,
            AttackerFaction = attackerFaction,
            DefenderFaction = defenderFaction,
            WasAtWar = AreFactionsAtWar(attackerFaction, defenderFaction),
        };
        playerFieldBattleFixture = fixture;

        var sessionId = "live-test-" + Guid.NewGuid().ToString("N");
        if (!encounterService.TryStartHostileEncounter(
                sessionId,
                attackerPartyBaseId,
                defenderPartyBaseId,
                responderSurrenders: false))
        {
            var partiallyCreatedMapEvent = attacker.MapEvent;
            if (partiallyCreatedMapEvent != null &&
                partiallyCreatedMapEvent == defender.MapEvent &&
                !partiallyCreatedMapEvent.IsFinalized)
                partiallyCreatedMapEvent.FinalizeEvent();

            var peaceRestored = RestoreFixtureWarState(fixture);
            playerFieldBattleFixture = null;
            return $"Failed to start the player field-battle fixture. PeaceRestored: {peaceRestored}";
        }

        var mapEvent = attacker.MapEvent;
        var mapEventId = mapEvent != null && objectManager.TryGetId(mapEvent, out var resolvedMapEventId)
            ? resolvedMapEventId
            : "<unresolved>";

        return
            "Player field-battle fixture started.\n" +
            $"MapEventId: {mapEventId}\n" +
            $"AttackerPartyId: {args[0]}\n" +
            $"DefenderPartyId: {args[1]}\n" +
            $"OriginalWarState: {fixture.WasAtWar}";
    }

    [CommandLineArgumentFunction("restore_player_field_battle", "coop.debug.mapevent")]
    public static string RestorePlayerFieldBattle(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.restore_player_field_battle";

        var fixture = playerFieldBattleFixture;
        if (fixture == null)
            return "No player field-battle fixture is pending restoration.";

        if (fixture.AttackerParty.MapEvent != null || fixture.DefenderParty.MapEvent != null)
            return "Cannot restore the player field-battle fixture while its map event is active.";

        var peaceRestored = RestoreFixtureWarState(fixture);

        playerFieldBattleFixture = null;
        return $"Player field-battle fixture restored. PeaceRestored: {peaceRestored}";
    }

    private static bool RestoreFixtureWarState(PlayerFieldBattleFixture fixture)
    {
        if (fixture.WasAtWar || !AreFactionsAtWar(fixture.AttackerFaction, fixture.DefenderFaction))
            return false;

        MakePeaceAction.Apply(fixture.AttackerFaction, fixture.DefenderFaction);
        return true;
    }

    [CommandLineArgumentFunction("request_player_field_battle", "coop.debug.mapevent")]
    public static string RequestPlayerFieldBattle(List<string> args)
    {
        if (!ModInformation.IsClient)
            return "Run this command on the attacking client.";

        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.request_player_field_battle <defenderMobilePartyId>";

        var attacker = MobileParty.MainParty;
        if (attacker?.Party == null || !attacker.IsActive || attacker.MapEvent != null)
            return "The local player must lead an active party outside a map event.";

        if (!TryGetObjectManager(out var objectManager))
            return "Unable to resolve ObjectManager.";

        var defenderError = string.Empty;
        if ((!objectManager.TryGetObject(args[0], out MobileParty defender) &&
             !CommandHelpers.TryGetMobileParty(args[0], out defender, out defenderError)) ||
            defender?.Party == null)
            return "Unable to resolve defender party: " + defenderError;

        if (defender == attacker || !defender.IsActive || defender.MapEvent != null)
            return "The defender must be a distinct active party outside a map event.";

        if (attacker.CurrentSettlement != null || defender.CurrentSettlement != null)
            return "Both player parties must be outside settlements.";

        if (attacker.MapFaction == null || defender.MapFaction == null ||
            attacker.MapFaction == defender.MapFaction)
            return "Player parties must belong to distinct map factions.";

        if (!objectManager.TryGetId(defender, out var defenderMobilePartyId) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.Players.Any(player => player.MobilePartyId == defenderMobilePartyId))
            return "The defender must belong to a registered player.";

        if (!objectManager.TryGetId(attacker.Party, out var attackerPartyId) ||
            !objectManager.TryGetId(defender.Party, out var defenderPartyId))
            return "Unable to resolve the registered PartyBase ids.";

        if (!ContainerProvider.TryResolve<INetwork>(out var network))
            return "Unable to resolve the client network.";

        network.SendAll(new NetworkRequestConversation(
            defenderPartyId,
            attackerPartyId,
            forcePlayerOutFromSettlement: false,
            ConversationRestartSource.PlayerEncounter,
            armyTalkEncounter: false));

        return
            "Player field-battle interaction requested through the production conversation path.\n" +
            $"AttackerPartyId: {attacker.StringId}\n" +
            $"DefenderPartyId: {defender.StringId}";
    }

    [CommandLineArgumentFunction("player_interaction_state", "coop.debug.mapevent")]
    public static string PlayerInteractionState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.player_interaction_state";

        return
            $"Active: {PlayerPartyInteractionDialogState.HasActiveState}\n" +
            $"SessionId: {PlayerPartyInteractionDialogState.SessionId ?? "none"}\n" +
            $"PartyId: {PlayerPartyInteractionDialogState.PartyId ?? "none"}\n" +
            $"OtherPartyId: {PlayerPartyInteractionDialogState.OtherPartyId ?? "none"}\n" +
            $"Phase: {PlayerPartyInteractionDialogState.Phase}\n" +
            $"Proposal: {PlayerPartyInteractionDialogState.Proposal}";
    }

    [CommandLineArgumentFunction("submit_player_interaction", "coop.debug.mapevent")]
    public static string SubmitPlayerInteraction(List<string> args)
    {
        if (!ModInformation.IsClient)
            return "Run this command on a player client.";

        if (args.Count != 1 ||
            !Enum.TryParse(args[0], ignoreCase: true, out PlayerPartyInteractionOption option) ||
            option == PlayerPartyInteractionOption.None)
            return "Usage: coop.debug.mapevent.submit_player_interaction <option>";

        if (!PlayerPartyInteractionDialogState.HasActiveState)
            return "No player-party interaction is active.";

        if (!PlayerPartyInteractionDialogState.IsOptionEnabled(option))
            return $"Player-party interaction option '{option}' is not enabled.";

        var sessionId = PlayerPartyInteractionDialogState.SessionId;
        PlayerPartyInteractionDialogState.Submit(option);
        return $"Submitted player-party interaction option '{option}' for session '{sessionId}'.";
    }

    private static bool AreFactionsAtWar(IFaction first, IFaction second)
    {
        try
        {
            return FactionManager.IsAtWarAgainstFaction(first, second);
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }

    /// <summary>
    /// Starts the current battle through the normal client/server mission-start gate.
    /// </summary>
    [CommandLineArgumentFunction("start_attack_mission", "coop.debug.mapevent")]
    public static string StartAttackMission(List<string> args)
    {
        if (ModInformation.IsServer)
        {
            return "Run this command on a client";
        }

        if (args.Count != 0)
        {
            return "Usage: coop.debug.mapevent.start_attack_mission";
        }

        var mainParty = MobileParty.MainParty;
        var mapEvent = mainParty?.MapEvent;
        if (mapEvent == null)
        {
            return "The main party has no replicated map event";
        }

        if (!TryGetObjectManager(out var objectManager)
            || !objectManager.TryGetId(mapEvent, out var mapEventId)
            || !objectManager.TryGetId(mainParty, out var partyId))
        {
            return "Unable to resolve the current battle ids";
        }

        var coordinator = BattleStartCoordinator.Instance;
        if (coordinator == null)
        {
            return "Battle start coordinator is unavailable";
        }

        return coordinator.RequestBlocking(BattleStartMode.Mission, mapEventId, partyId)
            ? $"Starting attack mission for {mapEventId}"
            : $"Server rejected attack mission for {mapEventId}";
    }

    // coop.debug.mapevent.start_looter
    /// <summary>
    /// Starts combat with looter
    /// </summary>
    [CommandLineArgumentFunction("start_looter", "coop.debug.mapevent")]
    public static string StartRandomLooterMapEvent(List<string> args)
    {
        //if (args.Count != 2)
        //{
        //    return "Usage: coop.debug.besiegercamp.set_number_of_troops_killed_on_side <besiegerCampId> <value> ";
        //}

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (!objectManager.TryGetObject("sea_raiders_1", out PartyBase partyBase))
        {
            return $"BesiegerCamp with ID: sea_raiders_1 not found";
        }

        EncounterManager.StartPartyEncounter(MobileParty.MainParty.Party, partyBase);


        return $"MapEvent Started";
    }

    // coop.debug.mapevent.start_nearest_looter
    /// <summary>
    /// Forces an encounter between the player's party and the nearest active bandit/looter party, so
    /// the bandit surrender/recruit dialogue can be reached without chasing one down. Run on a client
    /// (uses the player's main party). Bring a much larger party than the bandits so they offer to
    /// surrender or join.
    /// </summary>
    [CommandLineArgumentFunction("start_nearest_looter", "coop.debug.mapevent")]
    public static string StartNearestLooterMapEvent(List<string> args)
    {
        if (!TryGetObjectManager(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        var mainParty = MobileParty.MainParty;
        if (mainParty == null)
        {
            return "No main party — run this on a client with a player party.";
        }

        var mainPos = mainParty.Position.ToVec2();
        var nearest = MobileParty.All
            .Where(p => p.IsActive && p.IsBandit && p != mainParty
                        && p.MapEvent == null && p.CurrentSettlement == null && p.MemberRoster.TotalManCount > 0)
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(mainPos))
            .FirstOrDefault();

        if (nearest == null)
        {
            return "No active bandit/looter party found on the map.";
        }

        EncounterManager.StartPartyEncounter(mainParty.Party, nearest.Party);

        var partyId = objectManager.TryGetId(nearest, out string registryId) ? registryId : nearest.StringId;

        return $"Started encounter with {nearest.Name} (StringId {nearest.StringId}, registry id {partyId}), " +
               $"{nearest.MemberRoster.TotalManCount} troops, {nearest.Position.ToVec2().Distance(mainPos):0.0} away.";
    }

    // coop.debug.mapevent.start_nearest_bandit_attack PlayerOne [excludedPartyId]
    /// <summary>
    /// Starts a server-authoritative bandit attack encounter against a connected player.
    /// </summary>
    [CommandLineArgumentFunction("start_nearest_bandit_attack", "coop.debug.mapevent")]
    public static string StartNearestBanditAttack(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count < 1 || args.Count > 2)
        {
            return "Usage: coop.debug.mapevent.start_nearest_bandit_attack <controllerId> [excludedPartyId]";
        }

        if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
        {
            return error;
        }

        const int maximumFixtureTroops = 8;
        var remainingFixtureTroops = maximumFixtureTroops;
        var removedTroops = 0;
        for (var index = playerParty.MemberRoster.Count - 1; index >= 0; index--)
        {
            var element = playerParty.MemberRoster.GetElementCopyAtIndex(index);
            if (element.Character.IsHero)
                continue;

            var kept = Math.Min(element.Number, remainingFixtureTroops);
            var removed = element.Number - kept;
            if (removed > 0)
            {
                playerParty.MemberRoster.AddToCountsAtIndex(
                    index,
                    -removed,
                    -Math.Min(element.WoundedNumber, removed),
                    removeDepleted: false);
                removedTroops += removed;
            }
            remainingFixtureTroops -= kept;
        }
        playerParty.MemberRoster.RemoveZeroCounts();

        if (playerParty.CurrentSettlement != null)
        {
            LeaveSettlementAction.ApplyForParty(playerParty);
        }

        var excludedPartyId = args.Count == 2 ? args[1] : null;
        var playerPosition = playerParty.Position.ToVec2();
        var banditParty = MobileParty.All
            .Where(p => p.IsActive && p.IsBandit && p != playerParty
                        && p.MapEvent == null && p.CurrentSettlement == null && p.MemberRoster.TotalManCount > 0
                        && !MatchesPartyId(objectManager, p, excludedPartyId))
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(playerPosition))
            .FirstOrDefault();

        if (banditParty == null)
        {
            return "No active bandit/looter party found on the map.";
        }

        StartBattleAction.Apply(banditParty.Party, playerParty.Party);

        var partyId = objectManager.TryGetId(banditParty, out string registryId)
            ? registryId
            : banditParty.StringId;
        var partyBaseId = objectManager.TryGetId(banditParty.Party, out string partyBaseRegistryId)
            ? partyBaseRegistryId
            : "<unregistered>";

        return $"Started attack by {banditParty.Name} (StringId {banditParty.StringId}, " +
               $"registry id {partyId}, PartyBase id {partyBaseId}) " +
               $"against player {args[0]} after removing {removedTroops} excess fixture troops.";
    }

    // coop.debug.mapevent.bandit_attack_fixture_prepare PlayerOne mountain_bandits_24
    /// <summary>Prepares a reversible exact-bandit attack fixture for evidence capture.</summary>
    [CommandLineArgumentFunction("bandit_attack_fixture_prepare", "coop.debug.mapevent")]
    public static string PrepareBanditAttackFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 2)
            return "Usage: coop.debug.mapevent.bandit_attack_fixture_prepare <controllerId> <banditPartyId>";

        if (banditAttackFixture != null)
            return "A bandit attack fixture is already active.";

        if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
            return error;

        if ((!objectManager.TryGetObject(args[1], out MobileParty banditParty) &&
             !CommandHelpers.TryGetMobileParty(args[1], out banditParty, out error)) ||
            banditParty?.Party == null)
        {
            return $"Unable to resolve bandit party {args[1]}: {error}";
        }

        if (!banditParty.IsBandit || banditParty.MapEvent != null)
        {
            return $"Bandit party {args[1]} must be a bandit outside a map event.";
        }

        if (playerParty.Army?.LeaderParty == playerParty && playerParty.AttachedParties.Count > 0)
            return $"Player {args[0]} must not lead an army with attached parties.";

        if (banditParty.CurrentSettlement?.SettlementComponent is Hideout &&
            banditParty.CurrentSettlement.Parties.Count <= 1)
        {
            return $"Bandit party {args[1]} must not be the last party in its hideout.";
        }

        if (!objectManager.TryGetId(playerParty, out string playerPartyId) ||
            !objectManager.TryGetId(playerParty.Party, out string playerPartyBaseId) ||
            !objectManager.TryGetId(banditParty, out string banditPartyId) ||
            !objectManager.TryGetId(banditParty.Party, out string banditPartyBaseId))
        {
            return "The player and bandit parties must have registered MobileParty and PartyBase ids.";
        }

        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
            !behaviorSnapshot.TryCreate(playerParty, out PartyBehaviorUpdateData playerBehavior) ||
            !behaviorSnapshot.TryCreate(banditParty, out PartyBehaviorUpdateData banditBehavior))
        {
            return "Unable to capture the original party behavior.";
        }

        CharacterObject fixtureTroop = null;
        if (banditParty.MemberRoster.TotalManCount <= 0)
        {
            fixtureTroop = MobileParty.All
                .Where(party => party != banditParty && party.IsActive && party.IsBandit &&
                                party.MemberRoster.TotalManCount > 0)
                .SelectMany(party => party.MemberRoster.GetTroopRoster())
                .Where(element => !element.Character.IsHero && element.Number > 0)
                .OrderByDescending(element => element.Number)
                .Select(element => element.Character)
                .FirstOrDefault();
            if (fixtureTroop == null)
                return "No active bandit party has a regular troop for the fixture.";
        }

        var fixture = new BanditAttackFixture
        {
            ControllerId = args[0],
            PlayerParty = playerParty,
            BanditParty = banditParty,
            PlayerSettlement = playerParty.CurrentSettlement,
            BanditSettlement = banditParty.CurrentSettlement,
            BanditWasActive = banditParty.IsActive,
            BanditMemberRoster = banditParty.MemberRoster.GetTroopRoster().ToArray(),
            PlayerBehavior = playerBehavior,
            BanditBehavior = banditBehavior,
        };
        banditAttackFixture = fixture;

        try
        {
            if (playerParty.CurrentSettlement != null)
                LeaveSettlementAction.ApplyForParty(playerParty);
            if (banditParty.CurrentSettlement != null)
                LeaveSettlementAction.ApplyForParty(banditParty);

            if (fixtureTroop != null)
                banditParty.MemberRoster.AddToCounts(fixtureTroop, 1);
            banditParty.IsActive = true;
            banditParty.Position = new CampaignVec2(
                new Vec2(playerParty.Position.X - 0.4f, playerParty.Position.Y),
                isOnLand: true);
            banditParty.SetMoveModeHold();
            banditParty.ResetNavigationToHold();

            MessageBroker.Instance.Publish(
                typeof(MapEventDebugCommands),
                new PartyBehaviorChangeAttempted(
                    banditParty,
                    forcePosition: true,
                    isCurrentlyAtSea: false,
                    resetMovementToHold: true));

            return $"Bandit attack fixture prepared: controller={args[0]}, playerParty={playerPartyId}, " +
                   $"playerPartyBase={playerPartyBaseId}, banditParty={banditPartyId}, " +
                   $"banditPartyBase={banditPartyBaseId}, banditStringId={banditParty.StringId}, " +
                   $"originalSettlement={fixture.PlayerSettlement?.StringId ?? "none"}, " +
                   $"originalBanditSettlement={fixture.BanditSettlement?.StringId ?? "none"}, " +
                   $"originalBanditActive={fixture.BanditWasActive}, " +
                   $"originalBanditTroops={fixture.BanditMemberRoster.Sum(element => element.Number)}.";
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to prepare bandit attack fixture");
            if (TryRestoreBanditAttackFixture(fixture, out var restoreError))
                banditAttackFixture = null;
            else
                return $"Fixture preparation failed: {e.Message}. Cleanup failed: {restoreError}. Run the restore command.";

            return $"Fixture preparation failed: {e.Message}";
        }
    }

    // coop.debug.mapevent.bandit_attack_fixture_start PlayerOne mountain_bandits_24
    /// <summary>Starts the prepared server-authoritative attack by the exact bandit party.</summary>
    [CommandLineArgumentFunction("bandit_attack_fixture_start", "coop.debug.mapevent")]
    public static string StartBanditAttackFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 2)
            return "Usage: coop.debug.mapevent.bandit_attack_fixture_start <controllerId> <banditPartyId>";

        if (!TryGetObjectManager(out var objectManager))
            return "Unable to resolve ObjectManager";

        var fixture = banditAttackFixture;
        if (fixture == null || fixture.ControllerId != args[0] ||
            !MatchesPartyId(objectManager, fixture.BanditParty, args[1]))
        {
            return $"Prepare the bandit attack fixture for {args[0]} and {args[1]} first.";
        }

        if (fixture.MapEvent != null)
            return "The bandit attack fixture was already started.";

        var playerParty = fixture.PlayerParty;
        var banditParty = fixture.BanditParty;
        if (!banditParty.IsActive || banditParty.MemberRoster.TotalManCount <= 0 ||
            playerParty.CurrentSettlement != null || banditParty.CurrentSettlement != null ||
            playerParty.MapEvent != null || banditParty.MapEvent != null)
        {
            return "The prepared bandit attack fixture is no longer ready.";
        }

        if (!objectManager.TryGetId(playerParty, out string playerPartyId) ||
            !objectManager.TryGetId(playerParty.Party, out string playerPartyBaseId) ||
            !objectManager.TryGetId(banditParty, out string banditPartyId) ||
            !objectManager.TryGetId(banditParty.Party, out string banditPartyBaseId))
        {
            return "The player and bandit parties must have registered MobileParty and PartyBase ids.";
        }

        try
        {
            StartBattleAction.Apply(banditParty.Party, playerParty.Party);
            fixture.MapEvent = playerParty.MapEvent;
            if (fixture.MapEvent == null || banditParty.MapEvent != fixture.MapEvent)
                throw new InvalidOperationException("The bandit attack did not create a shared map event.");

            fixture.InvolvedParties = fixture.MapEvent.InvolvedParties.ToArray();

            objectManager.TryGetId(fixture.MapEvent, out string mapEventId);
            return $"Bandit attack fixture started: controller={args[0]}, playerParty={playerPartyId}, " +
                   $"playerPartyBase={playerPartyBaseId}, banditParty={banditPartyId}, " +
                   $"banditPartyBase={banditPartyBaseId}, banditStringId={banditParty.StringId}, " +
                   $"mapEvent={mapEventId ?? "unregistered"}.";
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to start bandit attack fixture");
            fixture.MapEvent ??= playerParty.MapEvent ?? banditParty.MapEvent;
            fixture.InvolvedParties ??= fixture.MapEvent?.InvolvedParties.ToArray();
            if (TryRestoreBanditAttackFixture(fixture, out var restoreError))
                banditAttackFixture = null;
            else
                return $"Fixture setup failed: {e.Message}. Cleanup failed: {restoreError}. Run the restore command.";

            return $"Fixture setup failed: {e.Message}";
        }
    }

    // coop.debug.mapevent.bandit_attack_fixture_state PlayerOne mountain_bandits_24
    /// <summary>Reports the exact bandit attack state on the server or a client.</summary>
    [CommandLineArgumentFunction("bandit_attack_fixture_state", "coop.debug.mapevent")]
    public static string GetBanditAttackFixtureState(List<string> args)
    {
        if (args.Count != 2)
            return "Usage: coop.debug.mapevent.bandit_attack_fixture_state <controllerId> <banditPartyId>";

        if (!TryGetPlayerParty(
                args[0],
                requireReady: false,
                out var objectManager,
                out var playerParty,
                out var error,
                allowActiveMapEvent: true))
        {
            return error;
        }

        if ((!objectManager.TryGetObject(args[1], out MobileParty banditParty) &&
             !CommandHelpers.TryGetMobileParty(args[1], out banditParty, out error)) ||
            banditParty == null)
        {
            return $"Unable to resolve bandit party {args[1]}: {error}";
        }

        objectManager.TryGetId(playerParty, out string playerPartyId);
        objectManager.TryGetId(banditParty, out string banditPartyId);
        objectManager.TryGetId(playerParty.MapEvent, out string playerMapEventId);
        objectManager.TryGetId(banditParty.MapEvent, out string banditMapEventId);

        return $"Bandit attack fixture state: controller={args[0]}, local={playerParty == MobileParty.MainParty}, " +
               $"playerParty={playerPartyId ?? "unregistered"}, banditParty={banditPartyId ?? "unregistered"}, " +
               $"banditStringId={banditParty.StringId}, playerMapEvent={playerMapEventId ?? "none"}, " +
               $"banditMapEvent={banditMapEventId ?? "none"}, " +
               $"sharedMapEvent={playerParty.MapEvent != null && playerParty.MapEvent == banditParty.MapEvent}, " +
               $"playerSettlement={playerParty.CurrentSettlement?.StringId ?? "none"}, " +
               $"banditSettlement={banditParty.CurrentSettlement?.StringId ?? "none"}, " +
               $"banditActive={banditParty.IsActive}, banditTroops={banditParty.MemberRoster.TotalManCount}, " +
               $"menu={Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "none"}.";
    }

    // coop.debug.mapevent.bandit_attack_fixture_restore PlayerOne
    /// <summary>Finalizes the bandit attack and restores both parties' original behavior.</summary>
    [CommandLineArgumentFunction("bandit_attack_fixture_restore", "coop.debug.mapevent")]
    public static string RestoreBanditAttackFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.bandit_attack_fixture_restore <controllerId>";

        if (banditAttackFixture == null || banditAttackFixture.ControllerId != args[0])
            return $"No active bandit attack fixture exists for {args[0]}.";

        var fixture = banditAttackFixture;
        if (!TryRestoreBanditAttackFixture(fixture, out var error))
            return $"Fixture restore failed: {error}. Retry the restore command.";

        banditAttackFixture = null;
        return $"Bandit attack fixture restored: controller={args[0]}, banditStringId={fixture.BanditParty.StringId}.";
    }

    private static bool TryRestoreBanditAttackFixture(BanditAttackFixture fixture, out string error)
    {
        try
        {
            if (fixture.MapEvent != null && !fixture.MapEvent.IsFinalized)
                fixture.MapEvent.FinalizeEvent();

            if (HasAttachedParties(fixture.MapEvent, fixture.InvolvedParties))
                RecoverPartiallyFinalizedMapEvent(fixture.MapEvent, fixture.InvolvedParties);

            if (fixture.PlayerParty.MapEvent != null || fixture.BanditParty.MapEvent != null)
                throw new InvalidOperationException("The fixture parties are still attached to a map event.");

            if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
                !RestorePartyBehavior(fixture.PlayerParty, fixture.PlayerBehavior, behaviorSnapshot) ||
                !RestorePartyBehavior(fixture.BanditParty, fixture.BanditBehavior, behaviorSnapshot))
            {
                throw new InvalidOperationException("Unable to restore the original party behavior.");
            }

            MessageBroker.Instance.Publish(
                typeof(MapEventDebugCommands),
                new PartyBehaviorChangeAttempted(
                    fixture.PlayerParty,
                    forcePosition: true,
                    isCurrentlyAtSea: fixture.PlayerParty.IsCurrentlyAtSea));
            MessageBroker.Instance.Publish(
                typeof(MapEventDebugCommands),
                new PartyBehaviorChangeAttempted(
                    fixture.BanditParty,
                    forcePosition: true,
                    isCurrentlyAtSea: fixture.BanditParty.IsCurrentlyAtSea));

            RestoreTroopRoster(fixture.BanditParty.MemberRoster, fixture.BanditMemberRoster);
            fixture.BanditParty.IsActive = fixture.BanditWasActive;

            if (fixture.PlayerSettlement != null)
                EnterSettlementAction.ApplyForParty(fixture.PlayerParty, fixture.PlayerSettlement);
            if (fixture.BanditSettlement != null)
                EnterSettlementAction.ApplyForParty(fixture.BanditParty, fixture.BanditSettlement);

            error = null;
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to restore bandit attack fixture");
            error = e.Message;
            return false;
        }
    }

    [CommandLineArgumentFunction("finish_non_battle_encounter", "coop.debug.mapevent")]
    public static string FinishNonBattleEncounter(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.finish_non_battle_encounter";

        if (PlayerEncounter.Current == null)
            return "No player encounter is active.";
        if (PlayerEncounter.Battle != null || MobileParty.MainParty?.MapEvent != null)
            return "Refusing to finish a battle encounter.";

        PlayerEncounter.Finish();
        return "Finished the current non-battle encounter.";
    }

    [CommandLineArgumentFunction("join_existing", "coop.debug.mapevent")]
    public static string JoinExistingBattle(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 2 ||
            !Enum.TryParse(args[1], true, out BattleSideEnum side) ||
            (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender))
        {
            return "Usage: coop.debug.mapevent.join_existing <mapEventId> <Attacker|Defender>";
        }

        if (PlayerEncounter.Current != null)
            return "A player encounter is already active.";
        if (!TryGetObjectManager(out var objectManager))
            return "Unable to resolve ObjectManager";
        if (!objectManager.TryGetObjectWithLogging<MapEvent>(args[0], out var mapEvent))
            return $"Unable to resolve map event {args[0]}.";
        if (mapEvent.IsFinalized || mapEvent.BattleState != BattleState.None)
            return $"Map event {args[0]} is already concluded.";

        var opposingParty = mapEvent.GetLeaderParty(
            side == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
        if (opposingParty == null)
            return $"Map event {args[0]} has no opposing leader party.";

        PlayerEncounter.Start();
        if (side == BattleSideEnum.Attacker)
            PlayerEncounter.Current.SetupFields(MobileParty.MainParty.Party, opposingParty);
        else
            PlayerEncounter.Current.SetupFields(opposingParty, MobileParty.MainParty.Party);
        PlayerEncounter.JoinBattle(side);

        return $"Started the {side} join encounter for map event {args[0]}.";
    }

    // coop.debug.mapevent.battle_reward_fixture_prepare testclient testclient2
    /// <summary>Closes the unfinished idle player encounter loaded by the #2308 live-test save.</summary>
    [CommandLineArgumentFunction("battle_reward_fixture_prepare", "coop.debug.mapevent")]
    public static string PrepareBattleRewardFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 2)
            return "Usage: coop.debug.mapevent.battle_reward_fixture_prepare <initiatorControllerId> <lateJoinerControllerId>";

        if (args[0] == args[1])
            return "The initiator and late joiner must be different players.";

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPlayer(args[0], out var initiatorPlayer) ||
            !playerManager.TryGetPlayer(args[1], out var lateJoinerPlayer) ||
            !playerManager.IsConnected(initiatorPlayer) ||
            !playerManager.IsConnected(lateJoinerPlayer))
            return "Both fixture players must be connected.";

        if (!TryGetPlayerParty(args[0], requireReady: false, out var objectManager, out var initiatorParty, out var error))
            return error;

        if (!TryGetPlayerParty(args[1], requireReady: false, out _, out var lateJoinerParty, out error))
            return error;

        var mapEvent = initiatorParty.MapEvent;
        if (mapEvent == null && lateJoinerParty.MapEvent == null)
            return "Battle reward fixture preflight is already clean.";

        if (mapEvent == null || lateJoinerParty.MapEvent != mapEvent)
            return "The fixture players must share the same saved map event.";

        if (mapEvent.IsFinalized)
            return "The saved map event is already finalized.";

        if (mapEvent.BattleState != BattleState.None)
            return $"Refusing to finalize saved map event with battle state {mapEvent.BattleState}.";

        if (mapEvent.MapEventSettlement != null || mapEvent.BattleObserver != null)
            return "Refusing to finalize a settlement or active simulation map event.";

        var mapEventId = objectManager.TryGetId(mapEvent, out string resolvedMapEventId)
            ? resolvedMapEventId
            : "<unregistered>";
        mapEvent.FinalizeEvent();

        if (!mapEvent.IsFinalized || initiatorParty.MapEvent != null || lateJoinerParty.MapEvent != null)
            return $"Saved map event {mapEventId} did not finalize cleanly.";

        return $"Battle reward fixture preflight prepared: finalized={mapEventId}, battleState=None.";
    }

    // coop.debug.mapevent.battle_reward_fixture_start testclient testclient2
    /// <summary>Creates the two-player late-join field battle from #2308.</summary>
    [CommandLineArgumentFunction("battle_reward_fixture_start", "coop.debug.mapevent")]
    public static string StartBattleRewardFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 2)
            return "Usage: coop.debug.mapevent.battle_reward_fixture_start <initiatorControllerId> <lateJoinerControllerId>";

        if (args[0] == args[1])
            return "The initiator and late joiner must be different players.";

        if (battleRewardFixture != null)
            return "A battle reward fixture is already active.";

        if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var initiatorParty, out var error))
            return error;

        if (!TryGetPlayerParty(args[1], requireReady: true, out _, out var lateJoinerParty, out error))
            return error;

        if (VillageHostileFactionStanceHelper.HasWarStance(initiatorParty.MapFaction, lateJoinerParty.MapFaction))
            return "The fixture players must be allied.";

        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            return "Unable to resolve the mobile-party behavior snapshot service.";

        if (!TryCreateBattleRewardPlayerSnapshot(
                args[0],
                objectManager,
                initiatorParty,
                behaviorSnapshot,
                out var initiator,
                out error))
            return error;

        if (!TryCreateBattleRewardPlayerSnapshot(
                args[1],
                objectManager,
                lateJoinerParty,
                behaviorSnapshot,
                out var lateJoiner,
                out error))
            return error;

        var danustica = Settlement.All.FirstOrDefault(settlement => settlement.StringId == "town_ES1");
        if (danustica == null)
            return "Danustica (town_ES1) was not found.";

        var referenceBandit = MobileParty.All.FirstOrDefault(party =>
            party.IsActive &&
            party.IsBandit &&
            party.ActualClan != null &&
            party.PartyComponent is BanditPartyComponent &&
            party.MemberRoster.TotalManCount > 0);
        if (referenceBandit == null)
            return "No active bandit party is available as a fixture template.";

        var banditTroop = referenceBandit.MemberRoster.GetTroopRoster()
            .Where(element => !element.Character.IsHero)
            .OrderByDescending(element => element.Number)
            .Select(element => element.Character)
            .FirstOrDefault();
        if (banditTroop == null)
            return "The bandit fixture template has no regular troop.";

        var fixture = new BattleRewardFixture
        {
            Initiator = initiator,
            LateJoiner = lateJoiner,
            BanditTroop = banditTroop,
        };
        battleRewardFixture = fixture;

        try
        {
            var fixturePosition = new CampaignVec2(
                new Vec2(danustica.GatePosition.X - 1.5f, danustica.GatePosition.Y),
                isOnLand: true);
            fixture.FixturePosition = fixturePosition;
            PrepareBattleRewardPlayer(initiator, totalTroops: 60, fixturePosition);
            PrepareBattleRewardPlayer(
                lateJoiner,
                totalTroops: 20,
                new CampaignVec2(new Vec2(fixturePosition.X - 0.2f, fixturePosition.Y), isOnLand: true));

            var banditComponent = (BanditPartyComponent)referenceBandit.PartyComponent;
            fixture.BanditParty = BanditPartyComponent.CreateBanditParty(
                $"debug_2308_reward_bandits_{Guid.NewGuid():N}",
                referenceBandit.ActualClan,
                banditComponent.Hideout,
                isBossParty: false,
                pt: null,
                new CampaignVec2(new Vec2(fixturePosition.X - 0.4f, fixturePosition.Y), isOnLand: true));
            fixture.BanditParty.MemberRoster.AddToCounts(banditTroop, 30);
            fixture.BanditParty.PrisonRoster.AddToCounts(banditTroop, 120);
            fixture.BanditParty.ItemRoster.AddToCounts(DefaultItems.Grain, 600);
            fixture.BanditParty.SetMoveModeHold();

            fixture.MapEvent = MapEventBattleFactory.CreateMapEvent(
                fixture.BanditParty.Party,
                initiator.Party.Party,
                default);
            if (fixture.MapEvent == null)
                throw new InvalidOperationException("The fixture battle did not create a map event.");

            fixture.InitiatorMapEventParty = fixture.MapEvent.DefenderSide.Parties
                .FirstOrDefault(party => party.Party == initiator.Party.Party);
            if (fixture.InitiatorMapEventParty == null)
                throw new InvalidOperationException("The initiating party was not added to the fixture battle.");

            if (!ContainerProvider.TryResolve<INetwork>(out var network) ||
                !objectManager.TryGetId(fixture.BanditParty.Party, out string banditPartyId) ||
                !objectManager.TryGetId(initiator.Party.Party, out string initiatorPartyId) ||
                !objectManager.TryGetId(fixture.MapEvent, out string mapEventId))
            {
                throw new InvalidOperationException("Unable to resolve the fixture's network ids.");
            }

            network.SendAll(new NetworkPlayerPartyHostileEncounterStarted(
                $"debug-2308-initiator-{Guid.NewGuid():N}",
                banditPartyId,
                initiatorPartyId,
                mapEventId));

            return $"Battle reward fixture started: mapEvent={mapEventId}, initiator={args[0]}, " +
                   $"initiatorTroops={initiator.Party.MemberRoster.TotalManCount}, lateJoiner={args[1]}, " +
                   $"lateJoinerTroops={lateJoiner.Party.MemberRoster.TotalManCount}, " +
                   $"bandit={fixture.BanditParty.StringId}, banditTroops={fixture.BanditParty.MemberRoster.TotalManCount}, " +
                   $"position={fixturePosition.X:R}|{fixturePosition.Y:R}.";
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to create battle reward fixture");
            if (TryRestoreBattleRewardFixture(fixture, out var restoreError))
                battleRewardFixture = null;
            else
                return $"Fixture setup failed: {e.Message}. Cleanup failed: {restoreError}. Run the restore command.";

            return $"Fixture setup failed: {e.Message}";
        }
    }

    [CommandLineArgumentFunction("battle_reward_fixture_reinforce", "coop.debug.mapevent")]
    public static string ReinforceBattleRewardFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.battle_reward_fixture_reinforce";

        var fixture = battleRewardFixture;
        if (fixture == null)
            return "No battle reward fixture is active.";
        if (fixture.MapEvent.IsFinalized)
            return "The fixture battle is already finalized.";
        if (fixture.ReinforcementAdded)
            return "The fixture reinforcement was already added.";

        var banditSide = fixture.BanditParty?.Party?.MapEventSide;
        var banditComponent = fixture.BanditParty?.PartyComponent as BanditPartyComponent;
        if (banditSide == null || banditComponent == null || fixture.BanditTroop == null)
            return "The fixture bandit side is no longer available.";

        fixture.ReinforcementParty = BanditPartyComponent.CreateBanditParty(
            $"debug_2423_reward_reinforcement_{Guid.NewGuid():N}",
            fixture.BanditParty.ActualClan,
            banditComponent.Hideout,
            isBossParty: false,
            pt: null,
            new CampaignVec2(
                new Vec2(fixture.FixturePosition.X - 0.6f, fixture.FixturePosition.Y),
                isOnLand: true));
        fixture.ReinforcementParty.MemberRoster.AddToCounts(fixture.BanditTroop, 12);
        fixture.ReinforcementParty.SetMoveModeHold();
        fixture.ReinforcementParty.Party.MapEventSide = banditSide;
        fixture.ReinforcementAdded = true;

        return $"Battle reward fixture reinforced: party={fixture.ReinforcementParty.StringId}, " +
               $"troops={fixture.ReinforcementParty.MemberRoster.TotalManCount}, " +
               $"enemyParties={banditSide.Parties.Count}.";
    }

    // coop.debug.mapevent.battle_reward_fixture_join
    /// <summary>Adds the second player to the active #2308 battle and opens its encounter.</summary>
    [CommandLineArgumentFunction("battle_reward_fixture_join", "coop.debug.mapevent")]
    public static string JoinBattleRewardFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.battle_reward_fixture_join";

        var fixture = battleRewardFixture;
        if (fixture == null)
            return "No battle reward fixture is active.";

        if (fixture.LateJoinerAdded)
            return $"Late joiner {fixture.LateJoiner.ControllerId} is already in the fixture battle.";

        if (fixture.MapEvent.IsFinalized)
            return "The fixture battle is already finalized.";

        var joiningParty = fixture.LateJoiner.Party.Party;
        if (joiningParty.MapEventSide != null)
            return $"Late joiner {fixture.LateJoiner.ControllerId} is already in a map event.";

        var joiningSide = fixture.Initiator.Party.Party.MapEventSide;
        if (joiningSide == null)
            return "The initiating party is no longer in the fixture battle.";

        joiningParty.MapEventSide = joiningSide;
        fixture.LateJoinerMapEventParty = joiningSide.Parties
            .FirstOrDefault(party => party.Party == joiningParty);
        if (fixture.LateJoinerMapEventParty == null)
            return "The late joiner was not added to the fixture battle.";

        if (!TryGetObjectManager(out var objectManager) ||
            !ContainerProvider.TryResolve<INetwork>(out var network) ||
            !objectManager.TryGetId(fixture.BanditParty.Party, out string banditPartyId) ||
            !objectManager.TryGetId(joiningParty, out string joiningPartyId) ||
            !objectManager.TryGetId(fixture.MapEvent, out string mapEventId))
        {
            return "Unable to resolve the late join encounter ids.";
        }

        fixture.LateJoinerAdded = true;
        network.SendAll(new NetworkPlayerPartyHostileEncounterStarted(
            $"debug-2308-late-join-{Guid.NewGuid():N}",
            banditPartyId,
            joiningPartyId,
            mapEventId));

        return $"Battle reward fixture late join opened: mapEvent={mapEventId}, " +
               $"controller={fixture.LateJoiner.ControllerId}, party={fixture.LateJoiner.Party.StringId}.";
    }

    [CommandLineArgumentFunction("battle_reward_fixture_begin_rout", "coop.debug.mapevent")]
    public static string BeginBattleRewardFixtureRout(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.battle_reward_fixture_begin_rout";

        var fixture = battleRewardFixture;
        if (fixture == null)
            return "No battle reward fixture is active.";
        if (fixture.MapEvent.IsFinalized)
            return "The fixture battle is already finalized.";
        if (!fixture.LateJoinerAdded)
            return "Add the late joiner before routing the fixture enemies.";
        if (!fixture.ReinforcementAdded)
            return "Add the fixture reinforcement before routing enemies.";
        if (fixture.PartialRoutIssued)
            return "The fixture partial rout was already issued.";
        if (!TryGetObjectManager(out var objectManager) ||
            !objectManager.TryGetId(fixture.MapEvent, out string mapEventId) ||
            !ContainerProvider.TryResolve<INetwork>(out var network))
        {
            return "Unable to resolve the fixture battle network state.";
        }

        fixture.PartialRoutIssued = true;
        network.SendAll(new NetworkRouteBattleEnemies(mapEventId, enemiesToLeaveFighting: 20));
        return $"Ordered fixture enemies to retreat while leaving up to 20 fighting: mapEvent={mapEventId}.";
    }

    [CommandLineArgumentFunction("battle_reward_fixture_route_enemies", "coop.debug.mapevent")]
    public static string RouteBattleRewardFixtureEnemies(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.battle_reward_fixture_route_enemies";

        var fixture = battleRewardFixture;
        if (fixture == null)
            return "No battle reward fixture is active.";
        if (fixture.MapEvent.IsFinalized)
            return "The fixture battle is already finalized.";
        if (!fixture.PartialRoutIssued)
            return "Begin the fixture rout before routing the final enemy.";
        if (fixture.EnemiesRouted)
            return "The fixture enemies were already ordered to retreat.";
        if (!TryGetObjectManager(out var objectManager) ||
            !objectManager.TryGetId(fixture.MapEvent, out string mapEventId) ||
            !ContainerProvider.TryResolve<INetwork>(out var network))
        {
            return "Unable to resolve the fixture battle network state.";
        }

        fixture.EnemiesRouted = true;
        network.SendAll(new NetworkRouteBattleEnemies(mapEventId, enemiesToLeaveFighting: 0));
        return $"Ordered the battle authority to route fixture enemies: mapEvent={mapEventId}.";
    }

    // coop.debug.mapevent.battle_reward_fixture_state
    /// <summary>Reports contributions and roster reward deltas for the active #2308 fixture.</summary>
    [CommandLineArgumentFunction("battle_reward_fixture_state", "coop.debug.mapevent")]
    public static string GetBattleRewardFixtureState(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.battle_reward_fixture_state";

        var fixture = battleRewardFixture;
        if (fixture == null)
            return "No battle reward fixture is active.";

        TryGetObjectManager(out var objectManager);
        string mapEventId = null;
        objectManager?.TryGetId(fixture.MapEvent, out mapEventId);

        return $"Battle reward fixture state: mapEvent={mapEventId ?? "unregistered"}, " +
               $"finalized={fixture.MapEvent.IsFinalized}, lateJoinerAdded={fixture.LateJoinerAdded}, " +
               $"reinforcementAdded={fixture.ReinforcementAdded}, partialRoutIssued={fixture.PartialRoutIssued}, " +
               $"enemiesRouted={fixture.EnemiesRouted}, " +
               FormatBattleRewardPlayerState("initiator", fixture.Initiator, fixture.InitiatorMapEventParty) + ", " +
               FormatBattleRewardPlayerState("lateJoiner", fixture.LateJoiner, fixture.LateJoinerMapEventParty) + ".";
    }

    // coop.debug.mapevent.battle_reward_client_state
    /// <summary>Reports the local player's staged or already-applied native battle rewards.</summary>
    [CommandLineArgumentFunction("battle_reward_client_state", "coop.debug.mapevent")]
    public static string GetBattleRewardClientState(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.battle_reward_client_state";

        var encounter = PlayerEncounter.Current;
        var mainParty = PartyBase.MainParty;
        return $"Battle reward client state: encounter={encounter != null}, " +
               $"encounterState={(encounter == null ? "none" : encounter.EncounterState.ToString())}, " +
               $"activeState={GameStateManager.Current?.ActiveState?.GetType().Name ?? "none"}, " +
               $"pendingItems={encounter?.RosterToReceiveLootItems.Sum(element => element.Amount) ?? 0}, " +
               $"pendingMembers={encounter?.RosterToReceiveLootMembers.TotalManCount ?? 0}, " +
               $"pendingPrisoners={encounter?.RosterToReceiveLootPrisoners.TotalManCount ?? 0}, " +
               $"partyItems={mainParty?.ItemRoster.Sum(element => element.Amount) ?? 0}, " +
               $"partyPrisoners={mainParty?.PrisonRoster.TotalManCount ?? 0}.";
    }

    // coop.debug.mapevent.battle_reward_fixture_restore
    /// <summary>Finalizes the #2308 battle, removes its bandits, and restores both players.</summary>
    [CommandLineArgumentFunction("battle_reward_fixture_restore", "coop.debug.mapevent")]
    public static string RestoreBattleRewardFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.battle_reward_fixture_restore";

        var fixture = battleRewardFixture;
        if (fixture == null)
            return "No battle reward fixture is active.";

        if (!TryRestoreBattleRewardFixture(fixture, out var error))
            return $"Fixture restore failed: {error}. Retry the restore command.";

        battleRewardFixture = null;
        return $"Battle reward fixture restored: initiator={fixture.Initiator.ControllerId}, " +
               $"lateJoiner={fixture.LateJoiner.ControllerId}.";
    }

    private static bool TryCreateBattleRewardPlayerSnapshot(
        string controllerId,
        IObjectManager objectManager,
        MobileParty party,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        out BattleRewardPlayerSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        error = null;

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPlayer(controllerId, out var player) ||
            !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero))
        {
            error = $"Unable to resolve player hero for {controllerId}.";
            return false;
        }

        if (hero.PartyBelongedTo != party || party.LeaderHero != hero)
        {
            error = $"Player {controllerId} must be leading their active party.";
            return false;
        }

        if (!behaviorSnapshot.TryCreate(party, out var behavior))
        {
            error = $"Unable to snapshot party behavior for {controllerId}.";
            return false;
        }

        snapshot = new BattleRewardPlayerSnapshot
        {
            ControllerId = controllerId,
            Hero = hero,
            Party = party,
            MemberRoster = party.MemberRoster.GetTroopRoster().ToArray(),
            PrisonRoster = party.PrisonRoster.GetTroopRoster().ToArray(),
            ItemRoster = party.ItemRoster.ToArray(),
            Behavior = behavior,
            HitPoints = hero.HitPoints,
            RecentEventsMorale = party.RecentEventsMorale,
        };
        return true;
    }

    private static void PrepareBattleRewardPlayer(
        BattleRewardPlayerSnapshot snapshot,
        int totalTroops,
        CampaignVec2 position)
    {
        RestoreTroopRoster(snapshot.Party.MemberRoster, Array.Empty<TroopRosterElement>());
        RestoreTroopRoster(snapshot.Party.PrisonRoster, Array.Empty<TroopRosterElement>());
        snapshot.Party.ItemRoster.Clear();

        snapshot.Party.MemberRoster.AddToCounts(snapshot.Hero.CharacterObject, 1, insertAtFront: true);
        var basicTroop = snapshot.Hero.Culture?.BasicTroop;
        if (basicTroop == null)
            throw new InvalidOperationException($"Player {snapshot.ControllerId} has no culture basic troop.");

        snapshot.Party.MemberRoster.AddToCounts(basicTroop, totalTroops - 1);
        snapshot.Hero.HitPoints = snapshot.Hero.MaxHitPoints;
        snapshot.Party.Position = position;
        snapshot.Party.SetMoveModeHold();
        snapshot.Party.ResetNavigationToHold();
        MessageBroker.Instance.Publish(
            typeof(MapEventDebugCommands),
            new PartyBehaviorChangeAttempted(
                snapshot.Party,
                forcePosition: true,
                isCurrentlyAtSea: false,
                resetMovementToHold: true));
    }

    private static string FormatBattleRewardPlayerState(
        string role,
        BattleRewardPlayerSnapshot snapshot,
        MapEventParty mapEventParty)
    {
        return $"{role}Controller={snapshot.ControllerId}, {role}Party={snapshot.Party.StringId}, " +
               $"{role}Contribution={mapEventParty?.ContributionToBattle ?? 0}, " +
               $"{role}ItemsDelta={snapshot.Party.ItemRoster.Sum(element => element.Amount)}, " +
               $"{role}PrisonersDelta={snapshot.Party.PrisonRoster.TotalManCount}, " +
               $"{role}MapEvent={(snapshot.Party.MapEvent == null ? "none" : "attached")}";
    }

    private static bool TryRestoreBattleRewardFixture(BattleRewardFixture fixture, out string error)
    {
        try
        {
            if (fixture.MapEvent != null && !fixture.MapEvent.IsFinalized)
                fixture.MapEvent.FinalizeEvent();

            if (fixture.BanditParty?.IsActive == true && fixture.BanditParty.MapEvent == null)
                DestroyPartyAction.Apply(null, fixture.BanditParty);
            if (fixture.ReinforcementParty?.IsActive == true && fixture.ReinforcementParty.MapEvent == null)
                DestroyPartyAction.Apply(null, fixture.ReinforcementParty);

            if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
                throw new InvalidOperationException("Unable to resolve the mobile-party behavior snapshot service.");

            RestoreBattleRewardPlayer(fixture.Initiator, behaviorSnapshot);
            RestoreBattleRewardPlayer(fixture.LateJoiner, behaviorSnapshot);
            error = null;
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to restore battle reward fixture");
            error = e.Message;
            return false;
        }
    }

    private static void RestoreBattleRewardPlayer(
        BattleRewardPlayerSnapshot snapshot,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        RestoreTroopRoster(snapshot.Party.MemberRoster, snapshot.MemberRoster);
        RestoreTroopRoster(snapshot.Party.PrisonRoster, snapshot.PrisonRoster);
        snapshot.Party.ItemRoster.Clear();
        foreach (var element in snapshot.ItemRoster)
            snapshot.Party.ItemRoster.Add(element);

        snapshot.Hero.HitPoints = snapshot.HitPoints;
        snapshot.Party.RecentEventsMorale = snapshot.RecentEventsMorale;
        snapshot.Party.Position = snapshot.Behavior.PartyPosition;
        snapshot.Party.IsCurrentlyAtSea = snapshot.Behavior.IsCurrentlyAtSea;
        if (!behaviorSnapshot.TryApply(snapshot.Party, snapshot.Behavior, out _))
            throw new InvalidOperationException($"Unable to restore party behavior for {snapshot.ControllerId}.");

        MessageBroker.Instance.Publish(
            typeof(MapEventDebugCommands),
            new PartyBehaviorChangeAttempted(
                snapshot.Party,
                forcePosition: true,
                isCurrentlyAtSea: snapshot.Party.IsCurrentlyAtSea,
                resetMovementToHold: false));
    }

    private static void RestoreTroopRoster(TroopRoster roster, TroopRosterElement[] elements)
    {
        for (int i = roster.Count - 1; i >= 0; i--)
        {
            var element = roster.GetElementCopyAtIndex(i);
            roster.AddToCountsAtIndex(i, -element.Number, -element.WoundedNumber, 0, false);
        }

        foreach (var element in elements)
            roster.AddToCounts(element.Character, element.Number, false, element.WoundedNumber, element.Xp, true);
    }

    // coop.debug.mapevent.wounded_allied_fixture_start PlayerOne
    /// <summary>Creates the wounded, troop-less player plus healthy allied force field encounter from #2097.</summary>
    [CommandLineArgumentFunction("wounded_allied_fixture_start", "coop.debug.mapevent")]
    public static string StartWoundedAlliedFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.wounded_allied_fixture_start <controllerId>";

        if (woundedAlliedFixture != null)
            return $"Fixture already active for {woundedAlliedFixture.ControllerId}.";

        if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
            return error;

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPlayer(args[0], out var player) ||
            !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero))
        {
            return $"Unable to resolve player hero for {args[0]}.";
        }

        if (!ContainerProvider.TryResolve<INetwork>(out var network))
            return "Unable to resolve network.";

        if (playerParty.PartyMoveMode != MoveModeType.Hold)
            return $"Player party {playerParty.StringId} must be holding before the fixture starts.";

        var playerPosition = playerParty.Position.ToVec2();
        var banditParty = MobileParty.All
            .Where(p => p.IsActive && p.IsBandit && p.MapEvent == null && p.CurrentSettlement == null &&
                        p.MemberRoster.TotalHealthyCount > 0)
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(playerPosition))
            .FirstOrDefault();
        if (banditParty == null)
            return "No active healthy bandit party is available.";

        var alliedParty = MobileParty.All
            .Where(p => p.IsActive && !p.IsBandit && !p.IsPlayerParty() && p != playerParty &&
                        p.MapEvent == null && p.CurrentSettlement == null && p.MemberRoster.TotalHealthyCount > 0 &&
                        p.MapFaction != null &&
                        !VillageHostileFactionStanceHelper.HasWarStance(playerParty.MapFaction, p.MapFaction) &&
                        VillageHostileFactionStanceHelper.HasWarStance(banditParty.MapFaction, p.MapFaction))
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(playerPosition))
            .FirstOrDefault();
        if (alliedParty == null)
            return "No active healthy AI party is available for the allied side.";

        var fixture = new WoundedAlliedFixture
        {
            ControllerId = args[0],
            PlayerHero = playerHero,
            PlayerParty = playerParty,
            OriginalHitPoints = playerHero.HitPoints,
            OriginalRecentEventsMorale = playerParty.RecentEventsMorale,
            OriginalRoster = playerParty.MemberRoster.GetTroopRoster().ToArray(),
            OriginalPosition = playerParty.Position,
        };

        try
        {
            playerHero.HitPoints = 1;
            RemoveHealthyPlayerTroops(fixture);
            playerParty.RecentEventsMorale = -1000f;

            fixture.MapEvent = MapEventBattleFactory.CreateMapEvent(
                banditParty.Party,
                playerParty.Party,
                default);
            if (fixture.MapEvent == null)
                throw new InvalidOperationException("The bandit encounter did not create a map event.");

            alliedParty.Party.MapEventSide = playerParty.Party.MapEventSide;
            fixture.InvolvedParties = fixture.MapEvent.InvolvedParties.ToArray();

            if (!objectManager.TryGetId(banditParty.Party, out string banditPartyId) ||
                !objectManager.TryGetId(playerParty.Party, out string playerPartyId) ||
                !objectManager.TryGetId(fixture.MapEvent, out string fixtureMapEventId))
            {
                throw new InvalidOperationException("Unable to resolve the fixture's network ids.");
            }

            network.SendAll(new NetworkPlayerPartyHostileEncounterStarted(
                $"debug-2097-{Guid.NewGuid():N}",
                banditPartyId,
                playerPartyId,
                fixtureMapEventId));
            woundedAlliedFixture = fixture;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to create wounded allied force fixture");
            woundedAlliedFixture = fixture;
            if (TryRestoreWoundedAlliedFixture(fixture, out var restoreError))
                woundedAlliedFixture = null;
            else
                return $"Fixture setup failed: {e.Message}. Cleanup failed: {restoreError}. Run the restore command.";

            return $"Fixture setup failed: {e.Message}";
        }

        objectManager.TryGetId(fixture.MapEvent, out string mapEventId);
        return $"Wounded allied fixture started: controller={args[0]}, mapEvent={mapEventId}, " +
               $"playerHealthy={playerParty.Party.NumberOfHealthyMembers}, alliedParty={alliedParty.StringId}, " +
               $"alliedHealthy={alliedParty.Party.NumberOfHealthyMembers}, banditParty={banditParty.StringId}.";
    }

    // coop.debug.mapevent.wounded_allied_fixture_state PlayerOne
    /// <summary>Reports the #2097 fixture state and the local patched order-attack option when applicable.</summary>
    [CommandLineArgumentFunction("wounded_allied_fixture_state", "coop.debug.mapevent")]
    public static string GetWoundedAlliedFixtureState(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.wounded_allied_fixture_state <controllerId>";

        if (!TryGetPlayerParty(args[0], requireReady: false, out var objectManager, out var playerParty, out var error))
            return error;

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPlayer(args[0], out var player) ||
            !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero))
        {
            return $"Unable to resolve player hero for {args[0]}.";
        }

        var mapEvent = playerParty.MapEvent;
        var side = playerParty.Party.MapEventSide;
        var alliedHealthy = side?.Parties
            .Where(p => p.Party != playerParty.Party)
            .Sum(p => p.Party.NumberOfHealthyMembers) ?? 0;

        var option = "not-local";
        if (ModInformation.IsClient && playerParty == MobileParty.MainParty && PlayerEncounter.Current != null)
        {
            var callbackArgs = new MenuCallbackArgs((MenuContext)null, null);
            var shown = new EncounterGameMenuBehavior()
                .game_menu_encounter_order_attack_on_condition(callbackArgs);
            var renderedOption = Campaign.Current?.CurrentMenuContext?.GameMenu?.MenuOptions
                .FirstOrDefault(menuOption => menuOption.IdString == "str_order_attack");
            option = $"conditionShown={shown},conditionEnabled={callbackArgs.IsEnabled}," +
                     $"leaveType={callbackArgs.optionLeaveType},renderedRegistered={renderedOption != null}," +
                     $"renderedEnabled={renderedOption?.IsEnabled ?? false}";
        }

        objectManager.TryGetId(mapEvent, out string mapEventId);
        return $"Wounded allied fixture state: controller={args[0]}, local={playerParty == MobileParty.MainParty}, " +
               $"hitPoints={playerHero.HitPoints}, wounded={playerHero.IsWounded}, " +
               $"roster={playerParty.MemberRoster.TotalManCount}, playerHealthy={playerParty.Party.NumberOfHealthyMembers}, " +
               $"morale={playerParty.Morale:0.##}, recentEventsMorale={playerParty.RecentEventsMorale:0.##}, " +
               $"position={playerParty.Position.X:R}|{playerParty.Position.Y:R}, moveMode={playerParty.PartyMoveMode}, " +
               $"alliedHealthy={alliedHealthy}, mapEvent={mapEventId ?? "none"}, " +
               $"menu={Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "none"}, option={option}.";
    }

    // coop.debug.mapevent.wounded_allied_fixture_restore PlayerOne
    /// <summary>Finalizes the #2097 fixture and restores the player's original hero, morale, and roster state.</summary>
    [CommandLineArgumentFunction("wounded_allied_fixture_restore", "coop.debug.mapevent")]
    public static string RestoreWoundedAlliedFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.wounded_allied_fixture_restore <controllerId>";

        if (woundedAlliedFixture == null || woundedAlliedFixture.ControllerId != args[0])
            return $"No active fixture exists for {args[0]}.";

        var fixture = woundedAlliedFixture;
        if (!TryRestoreWoundedAlliedFixture(fixture, out var error))
            return $"Fixture restore failed: {error}. Retry the restore command.";

        woundedAlliedFixture = null;

        return $"Wounded allied fixture restored: controller={args[0]}, hitPoints={fixture.PlayerHero.HitPoints}, " +
               $"roster={fixture.PlayerParty.MemberRoster.TotalManCount}.";
    }

    private static void RemoveHealthyPlayerTroops(WoundedAlliedFixture fixture)
    {
        var roster = fixture.PlayerParty.MemberRoster;
        for (int i = roster.Count - 1; i >= 0; i--)
        {
            var element = roster.GetElementCopyAtIndex(i);
            if (element.Character == fixture.PlayerHero.CharacterObject)
            {
                var woundedToAdd = element.Number - element.WoundedNumber;
                if (woundedToAdd > 0)
                    roster.AddToCounts(element.Character, 0, false, woundedToAdd);
                continue;
            }

            roster.AddToCountsAtIndex(i, -element.Number, -element.WoundedNumber, 0, false);
        }
    }

    private static void RestoreWoundedAlliedFixture(WoundedAlliedFixture fixture)
    {
        if (fixture.MapEvent != null)
        {
            if (!fixture.MapEvent.IsFinalized)
                fixture.MapEvent.FinalizeEvent();

            if (HasAttachedFixtureParties(fixture))
                RecoverPartiallyFinalizedMapEvent(fixture);
        }

        fixture.PlayerHero.HitPoints = fixture.OriginalHitPoints;
        fixture.PlayerParty.RecentEventsMorale = fixture.OriginalRecentEventsMorale;
        fixture.PlayerParty.Position = fixture.OriginalPosition;
        fixture.PlayerParty.SetMoveModeHold();
        fixture.PlayerParty.ResetNavigationToHold();
        MessageBroker.Instance.Publish(
            typeof(MapEventDebugCommands),
            new PartyBehaviorChangeAttempted(
                fixture.PlayerParty,
                forcePosition: true,
                isCurrentlyAtSea: fixture.PlayerParty.IsCurrentlyAtSea,
                resetMovementToHold: true));

        var roster = fixture.PlayerParty.MemberRoster;
        for (int i = roster.Count - 1; i >= 0; i--)
        {
            var element = roster.GetElementCopyAtIndex(i);
            roster.AddToCountsAtIndex(i, -element.Number, -element.WoundedNumber, 0, false);
        }

        foreach (var element in fixture.OriginalRoster)
        {
            roster.AddToCounts(element.Character, element.Number, false, element.WoundedNumber, element.Xp, true);
        }
    }

    private static bool HasAttachedFixtureParties(WoundedAlliedFixture fixture) =>
        HasAttachedParties(fixture.MapEvent, fixture.InvolvedParties);

    private static bool HasAttachedParties(MapEvent mapEvent, PartyBase[] involvedParties) =>
        mapEvent != null &&
        (involvedParties?.Any(p => p?._mapEventSide?.MapEvent == mapEvent) == true ||
         mapEvent.AttackerSide?.Parties.Count > 0 ||
         mapEvent.DefenderSide?.Parties.Count > 0);

    private static void RecoverPartiallyFinalizedMapEvent(WoundedAlliedFixture fixture)
    {
        RecoverPartiallyFinalizedMapEvent(fixture.MapEvent, fixture.InvolvedParties);
    }

    private static void RecoverPartiallyFinalizedMapEvent(MapEvent mapEvent, PartyBase[] involvedParties)
    {
        foreach (var party in involvedParties ?? Array.Empty<PartyBase>())
        {
            if (party?._mapEventSide?.MapEvent != mapEvent) continue;

            party._mapEventSide = null;
            if (party.MobileParty != null)
                party.MobileParty.EventPositionAdder = TaleWorlds.Library.Vec2.Zero;
            party.SetVisualAsDirty();
        }

        mapEvent.AttackerSide?.Clear();
        mapEvent.DefenderSide?.Clear();
        if (HasAttachedParties(mapEvent, involvedParties))
            throw new InvalidOperationException("The partially finalized fixture still has attached parties.");

        MessageBroker.Instance.Publish(mapEvent, new MapEventFinalized(mapEvent));
        MessageBroker.Instance.Publish(mapEvent, new InstanceDestroyed<MapEvent>(mapEvent));
    }

    private static bool TryRestoreWoundedAlliedFixture(WoundedAlliedFixture fixture, out string error)
    {
        try
        {
            RestoreWoundedAlliedFixture(fixture);
            error = null;
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to restore wounded allied force fixture");
            error = e.Message;
            return false;
        }
    }

    [CommandLineArgumentFunction("leave_settlement", "coop.debug.mapevent")]
    public static string LeaveSettlement(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.leave_settlement <controllerId>";

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return "Unable to resolve PlayerManager";

        if (!playerManager.TryGetPlayer(args[0], out var player))
            return $"No registered player has controller id {args[0]}.";

        if (!playerManager.IsConnected(player))
            return $"Player {args[0]} is not connected.";

        if (!TryGetObjectManager(out var objectManager) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty))
            return $"Unable to resolve player party {player.MobilePartyId}.";

        var settlement = playerParty.CurrentSettlement;
        if (settlement == null)
            return $"Player {args[0]} is already outside a settlement.";

        LeaveSettlementAction.ApplyForParty(playerParty);
        return $"Moved player {args[0]} out of {settlement.Name}.";
    }

    [CommandLineArgumentFunction("finish_current_encounter", "coop.debug.mapevent")]
    public static string FinishCurrentEncounter(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.finish_current_encounter";

        if (PlayerEncounter.Current == null)
            return "No active encounter.";

        PlayerEncounter.Finish();
        return "Finished the current local encounter.";
    }

    [CommandLineArgumentFunction("enter_current_battle", "coop.debug.mapevent")]
    public static string EnterCurrentBattle(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.enter_current_battle";

        if (PlayerEncounter.Current == null)
            return "No active encounter.";

        if (PlayerEncounter.Battle == null)
        {
            if (PlayerEncounter.StartBattle() == null)
                return "Unable to start the current battle.";

            GameMenu.SwitchToMenu("encounter");
        }

        var menuContext = Campaign.Current?.CurrentMenuContext;
        if (menuContext == null)
            return "No active encounter menu.";

        MenuHelper.EncounterAttackConsequence(new MenuCallbackArgs(menuContext, null));
        return "Requested entry into the current battle.";
    }

    // coop.debug.mapevent.finish_player_encounter PlayerOne
    /// <summary>
    /// Closes the connected player's encounter through the existing authoritative leave path.
    /// </summary>
    [CommandLineArgumentFunction("finish_player_encounter", "coop.debug.mapevent")]
    public static string FinishPlayerEncounter(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.finish_player_encounter <controllerId>";
        }

        if (!TryGetPlayerParty(
                args[0],
                requireReady: true,
                out var objectManager,
                out var playerParty,
                out var error,
                allowActiveMapEvent: true))
        {
            return error;
        }

        if (!objectManager.TryGetIdWithLogging(playerParty.Party, out var partyBaseId))
        {
            return $"Unable to resolve PartyBase for player {args[0]}.";
        }

        MessageBroker.Instance.Publish(
            playerParty.Party,
            new PlayerLeaveBattleAttempted(playerParty.Party));
        return $"Requested encounter finish for player {args[0]} (PartyBase id {partyBaseId}).";
    }

    // coop.debug.mapevent.conversation_hold_state <partyBaseId>
    /// <summary>
    /// Reports whether the server currently holds an AI PartyBase for a conversation.
    /// </summary>
    [CommandLineArgumentFunction("conversation_hold_state", "coop.debug.mapevent")]
    public static string ConversationHoldState(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.conversation_hold_state <partyBaseId>";
        }

        var held = ConversationPartyTracker.Instance?.TryGetEngagement(args[0], out _) == true;
        return $"Conversation hold for PartyBase id {args[0]}: {(held ? "held" : "released")}.";
    }

    // coop.debug.mapevent.late_join_mode_fixture PlayerOne PlayerTwo
    /// <summary>
    /// Creates a server-authoritative battle, claims mission mode before the second player joins, then routes the
    /// second player's join through the real request handler.
    /// </summary>
    [CommandLineArgumentFunction("late_join_mode_fixture", "coop.debug.mapevent")]
    public static string StartLateJoinModeFixture(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.mapevent.late_join_mode_fixture <firstControllerId> <joiningControllerId>";
        }

        if (lateJoinModeFixture != null)
        {
            return $"A late-join mode fixture is already active for map event {lateJoinModeFixture.MapEventId}.";
        }

        if (args[0] == args[1])
        {
            return "The fixture requires two different connected players.";
        }

        if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var firstParty, out var error))
        {
            return error;
        }

        if (!TryGetPlayerParty(args[1], requireReady: true, out _, out var joiningParty, out error))
        {
            return error;
        }

        if (firstParty.CurrentSettlement != null || joiningParty.CurrentSettlement != null)
        {
            return "Both players must be on the campaign map, outside settlements.";
        }

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPeer(args[0], out var firstPeer) ||
            !playerManager.TryGetPeer(args[1], out _))
        {
            return "Unable to resolve both connected player peers.";
        }

        if (!ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
        {
            return "Unable to resolve the late-join mode fixture services.";
        }

        if (!behaviorSnapshot.TryCreate(firstParty, out var firstPlayerBehavior) ||
            !behaviorSnapshot.TryCreate(joiningParty, out var joiningPlayerBehavior))
        {
            return "Unable to capture both players' original movement state.";
        }

        var firstFaction = firstParty.MapFaction?.MapFaction ?? firstParty.MapFaction;
        var joiningFaction = joiningParty.MapFaction?.MapFaction ?? joiningParty.MapFaction;
        var firstPosition = firstParty.Position.ToVec2();
        var opponentParty = MobileParty.All
            .Where(p => p.IsActive && p.IsBandit && p.MapEvent == null && p.CurrentSettlement == null &&
                        p.MemberRoster.TotalHealthyCount > 0 && p.MapFaction != null &&
                        VillageHostileFactionStanceHelper.HasWarStance(firstFaction, p.MapFaction) &&
                        VillageHostileFactionStanceHelper.HasWarStance(joiningFaction, p.MapFaction))
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(firstPosition))
            .FirstOrDefault();

        if (opponentParty == null)
        {
            return "No active healthy bandit party hostile to both players was found.";
        }

        if (!behaviorSnapshot.TryCreate(opponentParty, out var opponentBehavior))
        {
            return $"Unable to capture the opponent movement state for {opponentParty.Name}.";
        }

        if (!objectManager.TryGetId(firstParty.Party, out string firstPartyId) ||
            !objectManager.TryGetId(firstParty, out string firstMobilePartyId) ||
            !objectManager.TryGetId(joiningParty.Party, out string joiningPartyId) ||
            !objectManager.TryGetId(joiningParty, out string joiningMobilePartyId) ||
            !objectManager.TryGetId(opponentParty, out string opponentMobilePartyId))
        {
            return "Unable to resolve fixture party ids.";
        }

        var mapEvent = MapEventBattleFactory.CreateMapEvent(firstParty.Party, opponentParty.Party, default);
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out string mapEventId))
        {
            if (mapEvent != null && !mapEvent.IsFinalized)
                mapEvent.FinalizeEvent();

            RestorePartyBehavior(firstParty, firstPlayerBehavior, behaviorSnapshot);
            RestorePartyBehavior(joiningParty, joiningPlayerBehavior, behaviorSnapshot);
            RestorePartyBehavior(opponentParty, opponentBehavior, behaviorSnapshot);
            return "Unable to create or resolve the fixture map event.";
        }

        lateJoinModeFixture = new LateJoinModeFixture
        {
            MapEventId = mapEventId,
            FirstControllerId = args[0],
            FirstPlayerPartyId = firstPartyId,
            FirstPlayerMobilePartyId = firstMobilePartyId,
            FirstPlayerBehavior = firstPlayerBehavior,
            JoiningControllerId = args[1],
            JoiningPlayerPartyId = joiningPartyId,
            JoiningPlayerMobilePartyId = joiningMobilePartyId,
            JoiningPlayerBehavior = joiningPlayerBehavior,
            OpponentMobilePartyId = opponentMobilePartyId,
            OpponentBehavior = opponentBehavior,
        };

        var hasFieldBattleOpponent = mapEvent.EventType == MapEvent.BattleTypes.FieldBattle &&
                                     mapEvent.MapEventSettlement == null &&
                                     mapEvent.DefenderSide?.Parties.Any(
                                         p => p.Party == opponentParty.Party) == true;
        if (!hasFieldBattleOpponent)
        {
            CleanupLateJoinModeFixture(messageBroker, behaviorSnapshot, objectManager);
            return $"Late-join fixture {mapEventId} did not create the required field battle.";
        }

        // Route the first player's Attack through the real server handler. The resulting mission-start and mode
        // broadcasts reach PlayerTwo before its party belongs to the event, reproducing the missed-claim timing.
        messageBroker.Publish(firstPeer, new NetworkBattleStartRequest(
            Guid.NewGuid().ToString(),
            (int)BattleStartMode.Mission,
            mapEventId,
            firstMobilePartyId));

        return $"Late-join field-battle fixture created and first mission requested: mapEvent={mapEventId}, " +
               $"eventType={mapEvent.EventType}, opponent={opponentParty.Name} ({opponentParty.StringId}), " +
               $"firstPlayer={args[0]}, joiningPlayer={args[1]}, firstSide=Attacker.";
    }

    // coop.debug.mapevent.late_join_mode_join
    /// <summary>Routes the waiting player's attacker-side join after the first player has entered the mission.</summary>
    [CommandLineArgumentFunction("late_join_mode_join", "coop.debug.mapevent")]
    public static string JoinLateJoinModeFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.late_join_mode_join";

        var fixture = lateJoinModeFixture;
        if (fixture == null)
            return "No late-join mode fixture is active.";
        if (fixture.JoiningPartyJoined)
            return $"Player {fixture.JoiningControllerId} already joined fixture map event {fixture.MapEventId}.";

        if (!TryGetObjectManager(out var objectManager) ||
            !ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var missionMembership) ||
            !playerManager.TryGetPeer(fixture.JoiningControllerId, out var joiningPeer))
        {
            return "Unable to resolve the late-join fixture services.";
        }

        if (!missionMembership.IsControllerInMission(fixture.FirstControllerId))
            return $"Player {fixture.FirstControllerId} has not entered the field battle mission.";
        if (missionMembership.IsControllerInMission(fixture.JoiningControllerId))
            return $"Player {fixture.JoiningControllerId} is already in a mission.";
        if (!ServerBattleModeArbiter.TryGetMode(fixture.MapEventId, out var mode) ||
            mode != BattleStartMode.Mission)
        {
            return $"Fixture map event {fixture.MapEventId} is not claimed for Mission mode.";
        }
        if (!objectManager.TryGetObjectWithLogging<MapEvent>(fixture.MapEventId, out var mapEvent) ||
            !objectManager.TryGetObjectWithLogging<PartyBase>(fixture.JoiningPlayerPartyId, out var joiningParty))
        {
            return "Unable to resolve the fixture map event or joining party.";
        }

        messageBroker.Publish(joiningPeer, new NetworkRequestJoinBattle(
            Guid.NewGuid().ToString(),
            fixture.MapEventId,
            fixture.JoiningPlayerPartyId,
            BattleSideEnum.Attacker));

        if (joiningParty.MapEvent != mapEvent)
            return $"Player {fixture.JoiningControllerId} did not join fixture map event {fixture.MapEventId}.";

        fixture.JoiningPartyJoined = true;
        return $"Late join accepted: mapEvent={fixture.MapEventId}, joiningPlayer={fixture.JoiningControllerId}, " +
               "side=Attacker, replayedMode=Mission, firstPlayerInMission=True, joiningPlayerInMission=False.";
    }

    // coop.debug.mapevent.late_join_mode_enter
    /// <summary>Routes the late joiner's Attack request through the real mission-start handler.</summary>
    [CommandLineArgumentFunction("late_join_mode_enter", "coop.debug.mapevent")]
    public static string EnterLateJoinModeFixtureMission(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.late_join_mode_enter";

        var fixture = lateJoinModeFixture;
        if (fixture == null)
            return "No late-join mode fixture is active.";
        if (!fixture.JoiningPartyJoined)
            return $"Player {fixture.JoiningControllerId} has not joined fixture map event {fixture.MapEventId}.";

        if (!ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var missionMembership) ||
            !playerManager.TryGetPeer(fixture.JoiningControllerId, out var joiningPeer))
        {
            return "Unable to resolve the late-join mission-entry services.";
        }

        if (!missionMembership.IsControllerInMission(fixture.FirstControllerId))
            return $"Player {fixture.FirstControllerId} is no longer in the field battle mission.";
        if (missionMembership.IsControllerInMission(fixture.JoiningControllerId))
            return $"Player {fixture.JoiningControllerId} already entered the field battle mission.";

        messageBroker.Publish(joiningPeer, new NetworkBattleStartRequest(
            Guid.NewGuid().ToString(),
            (int)BattleStartMode.Mission,
            fixture.MapEventId,
            fixture.JoiningPlayerMobilePartyId));

        return $"Late joiner mission requested: mapEvent={fixture.MapEventId}, " +
               $"joiningPlayer={fixture.JoiningControllerId}, mode=Mission.";
    }

#if DEBUG
    // coop.debug.mapevent.late_join_mode_begin_field_battle
    /// <summary>Finishes the local deployment phase so live evidence shows the active field battle.</summary>
    [CommandLineArgumentFunction("late_join_mode_begin_field_battle", "coop.debug.mapevent")]
    public static string BeginLateJoinModeFixtureFieldBattle(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.late_join_mode_begin_field_battle";

        var mission = Mission.Current;
        if (mission == null)
            return "No mission is active.";

        var deploymentController = mission.GetMissionBehavior<DeploymentMissionController>();
        if (deploymentController?.TeamSetupOver != true)
            return "Local deployment is not ready.";

        var deploymentHandler = mission.GetMissionBehavior<DeploymentHandler>();
        if (deploymentHandler == null)
            return "The field battle is already active.";

        mission.DisableDying = true;
        deploymentHandler.FinishDeployment();
        if (!ProtectLateJoinModeFixturePlayer(mission))
            return "Local deployment finished, but the local player agent was not assigned.";

        return "Local deployment finished; the field battle is active and the local player is protected.";
    }

    // coop.debug.mapevent.late_join_mode_disable_dying
    /// <summary>Prevents the live-test battle from resolving before both client views are captured.</summary>
    [CommandLineArgumentFunction("late_join_mode_disable_dying", "coop.debug.mapevent")]
    public static string DisableLateJoinModeFixtureDying(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.late_join_mode_disable_dying";

        var mission = Mission.Current;
        if (mission == null)
            return "No mission is active.";

        mission.DisableDying = true;
        var playerProtected = ProtectLateJoinModeFixturePlayer(mission);
        return playerProtected
            ? "Dying disabled for the local fixture mission; the local player is protected."
            : "Dying disabled for the local fixture mission; the local player is not assigned yet.";
    }

    private static bool ProtectLateJoinModeFixturePlayer(Mission mission)
    {
        var mainAgent = mission.MainAgent;
        if (mainAgent == null)
            return false;

        mainAgent.SetMortalityState(Agent.MortalityState.Immortal);
        mainAgent.Health = mainAgent.HealthLimit;
        return true;
    }

    // coop.debug.mapevent.late_join_mode_exit_missions
    /// <summary>Asks every fixture mission member to return to campaign before authoritative cleanup.</summary>
    [CommandLineArgumentFunction("late_join_mode_exit_missions", "coop.debug.mapevent")]
    public static string ExitLateJoinModeFixtureMissions(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.late_join_mode_exit_missions";

        var fixture = lateJoinModeFixture;
        if (fixture == null)
            return "No late-join mode fixture is active.";

        if (!ContainerProvider.TryResolve<INetwork>(out var network) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var missionMembership))
        {
            return "Unable to resolve the late-join mission-exit services.";
        }

        var requested = 0;
        foreach (var controllerId in new[] { fixture.FirstControllerId, fixture.JoiningControllerId })
        {
            if (!missionMembership.IsControllerInMission(controllerId) ||
                !playerManager.TryGetPeer(controllerId, out var peer))
                continue;

            network.Send(peer, new NetworkEndLateJoinModeFixtureMission(fixture.MapEventId));
            requested++;
        }

        return $"Late-join fixture mission exit requested for {requested} player(s).";
    }
#endif

    // coop.debug.mapevent.late_join_mode_state PlayerTwo
    /// <summary>Reports a player's map-event membership and known authoritative battle mode.</summary>
    [CommandLineArgumentFunction("late_join_mode_state", "coop.debug.mapevent")]
    public static string GetLateJoinModeState(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.late_join_mode_state <controllerId>";
        }

        if (!TryGetPlayerParty(args[0], requireReady: false, out var objectManager, out var playerParty, out var error))
        {
            return error;
        }

        var mapEvent = playerParty.MapEvent;
        var mapEventId = mapEvent != null && objectManager.TryGetId(mapEvent, out string resolvedId)
            ? resolvedId
            : "none";
        var eventType = mapEvent?.EventType.ToString() ?? "none";
        var settlement = mapEvent?.MapEventSettlement;
        var settlementName = settlement != null ? $"{settlement.Name} ({settlement.StringId})" : "none";
        var opponentParties = mapEvent?.DefenderSide?.Parties.Count ?? 0;
        var side = playerParty.MapEventSide?.MissionSide.ToString() ?? "none";
        var mode = "Unclaimed";
        if (mapEventId != "none")
        {
            if (ModInformation.IsServer && ServerBattleModeArbiter.TryGetMode(mapEventId, out var serverMode))
                mode = serverMode.ToString();
            else if (BattleModeRegistry.IsMission(mapEventId))
                mode = BattleStartMode.Mission.ToString();
            else if (BattleModeRegistry.IsSimulation(mapEventId))
                mode = BattleStartMode.Simulation.ToString();
        }

        var missionActive = ModInformation.IsServer
            ? ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var missionMembership) &&
              missionMembership.IsControllerInMission(args[0])
            : MissionState.Current != null || Mission.Current != null;
        var missionAgents = ModInformation.IsClient && Mission.Current != null
            ? Mission.Current.Agents.Count
            : 0;
        var deploymentActive = ModInformation.IsClient &&
                               Mission.Current?.HasMissionBehavior<DeploymentHandler>() == true;

        return $"Late-join mode state: controller={args[0]}, mapEvent={mapEventId}, eventType={eventType}, " +
               $"settlement={settlementName}, opponentParties={opponentParties}, side={side}, mode={mode}, " +
               $"missionActive={missionActive}, missionAgents={missionAgents}, deploymentActive={deploymentActive}.";
    }

    // coop.debug.mapevent.late_join_mode_cleanup
    /// <summary>Removes the fixture field battle and restores each party's movement state.</summary>
    [CommandLineArgumentFunction("late_join_mode_cleanup", "coop.debug.mapevent")]
    public static string CleanupLateJoinModeFixture(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count != 0)
        {
            return "Usage: coop.debug.mapevent.late_join_mode_cleanup";
        }

        if (lateJoinModeFixture == null)
        {
            return "No late-join mode fixture is active.";
        }

        if (!TryGetObjectManager(out var objectManager) ||
            !ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
        {
            return "Unable to resolve the late-join mode cleanup services.";
        }

        var mapEventId = lateJoinModeFixture.MapEventId;
        var restored = CleanupLateJoinModeFixture(messageBroker, behaviorSnapshot, objectManager);
        return restored
            ? $"Late-join field-battle fixture {mapEventId} cleaned up and party movement restored."
            : $"Late-join field-battle fixture {mapEventId} cleaned up, but its original state could not be fully restored.";
    }

    private static bool CleanupLateJoinModeFixture(
        IMessageBroker messageBroker,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        IObjectManager objectManager)
    {
        var fixture = lateJoinModeFixture;
        if (fixture == null) return true;

        messageBroker.Publish(typeof(MapEventDebugCommands), new NetworkRequestLeaveBattle(fixture.JoiningPlayerPartyId));
        messageBroker.Publish(typeof(MapEventDebugCommands), new NetworkRequestLeaveBattle(fixture.FirstPlayerPartyId));
        if (objectManager.TryGetObject<MapEvent>(fixture.MapEventId, out var mapEvent) && !mapEvent.IsFinalized)
            mapEvent.FinalizeEvent();
        ServerBattleModeArbiter.Release(fixture.MapEventId);

        var restored = RestorePartyBehavior(
            fixture.FirstPlayerMobilePartyId,
            fixture.FirstPlayerBehavior,
            behaviorSnapshot,
            objectManager);
        restored = RestorePartyBehavior(
            fixture.JoiningPlayerMobilePartyId,
            fixture.JoiningPlayerBehavior,
            behaviorSnapshot,
            objectManager) && restored;
        restored = RestorePartyBehavior(
            fixture.OpponentMobilePartyId,
            fixture.OpponentBehavior,
            behaviorSnapshot,
            objectManager) && restored;

        lateJoinModeFixture = null;
        return restored;
    }

    private static bool RestorePartyBehavior(
        string mobilePartyId,
        PartyBehaviorUpdateData behavior,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        IObjectManager objectManager)
    {
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(mobilePartyId, out var mobileParty))
            return false;

        return RestorePartyBehavior(mobileParty, behavior, behaviorSnapshot);
    }

    private static bool RestorePartyBehavior(
        MobileParty mobileParty,
        PartyBehaviorUpdateData behavior,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        mobileParty.Position = behavior.PartyPosition;
        return behaviorSnapshot.TryApply(mobileParty, behavior, out _);
    }

    // coop.debug.mapevent.peace_pursuit_fixture PlayerOne
    /// <summary>
    /// Finds a neutral AI party that can be used without changing its original movement state.
    /// </summary>
    [CommandLineArgumentFunction("peace_pursuit_fixture", "coop.debug.mapevent")]
    public static string GetPeacePursuitFixture(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.peace_pursuit_fixture <controllerId>";
        }

        if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
        {
            return error;
        }

        var neutralParty = FindPeacePursuitFixture(playerParty);
        if (neutralParty == null)
        {
            return "No active neutral AI party already holding on the map.";
        }

        return FormatPeacePursuitState("Peace pursuit fixture", objectManager, neutralParty, playerParty);
    }

    // coop.debug.mapevent.peace_pursuit_state PlayerOne mobileParty_1
    /// <summary>
    /// Reports the pursuit-test party state on the current machine.
    /// </summary>
    [CommandLineArgumentFunction("peace_pursuit_state", "coop.debug.mapevent")]
    public static string GetPeacePursuitState(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.mapevent.peace_pursuit_state <controllerId> <partyStringId>";
        }

        if (!TryGetPlayerParty(args[0], requireReady: false, out var objectManager, out var playerParty, out var error))
        {
            return error;
        }

        var neutralParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[1]);
        if (neutralParty == null)
        {
            return $"Party {args[1]} was not found.";
        }

        return FormatPeacePursuitState("Peace pursuit state", objectManager, neutralParty, playerParty);
    }

    // coop.debug.mapevent.test_peace_stops_pursuit PlayerOne mobileParty_1
    /// <summary>
    /// Makes a selected neutral AI party pursue a connected player, then makes peace.
    /// </summary>
    [CommandLineArgumentFunction("test_peace_stops_pursuit", "coop.debug.mapevent")]
    public static string TestPeaceStopsPursuit(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.mapevent.test_peace_stops_pursuit <controllerId> <partyStringId>";
        }

        if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
        {
            return error;
        }

        var neutralParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[1]);
        if (neutralParty == null)
        {
            return $"Party {args[1]} was not found.";
        }

        if (!IsPeacePursuitFixture(neutralParty, playerParty))
        {
            return $"Party {args[1]} is not a neutral AI party already holding on the map.";
        }

        DeclareWarAction.ApplyByDefault(neutralParty.MapFaction, playerParty.MapFaction);
        if (!FactionManager.IsAtWarAgainstFaction(neutralParty.MapFaction, playerParty.MapFaction))
        {
            return $"Unable to establish war between {neutralParty.MapFaction.Name} and {playerParty.MapFaction.Name}.";
        }

        neutralParty.SetMoveGoAroundParty(playerParty, MobileParty.NavigationType.Default);
        MakePeaceAction.Apply(neutralParty.MapFaction, playerParty.MapFaction);

        var stopped = neutralParty.DefaultBehavior == AiBehavior.Hold &&
                      neutralParty.PartyMoveMode == MoveModeType.Hold &&
                      neutralParty.TargetParty == null &&
                      !FactionManager.IsAtWarAgainstFaction(neutralParty.MapFaction, playerParty.MapFaction);

        return FormatPeacePursuitState($"Peace pursuit test {(stopped ? "passed" : "failed")}",
            objectManager,
            neutralParty,
            playerParty);
    }

    private static bool TryGetPlayerParty(
        string controllerId,
        bool requireReady,
        out IObjectManager objectManager,
        out MobileParty playerParty,
        out string error,
        bool allowActiveMapEvent = false)
    {
        objectManager = null;
        playerParty = null;
        error = null;

        if (!TryGetObjectManager(out objectManager))
        {
            error = "Unable to resolve ObjectManager";
            return false;
        }

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            error = "Unable to resolve PlayerManager";
            return false;
        }

        if (!playerManager.TryGetPlayer(controllerId, out var player))
        {
            error = $"No registered player has controller id {controllerId}.";
            return false;
        }

        if (requireReady && ModInformation.IsServer && !playerManager.IsConnected(player))
        {
            error = $"Player {controllerId} is not connected.";
            return false;
        }

        if (!objectManager.TryGetObjectWithLogging(player.MobilePartyId, out playerParty))
        {
            error = $"Unable to resolve player party {player.MobilePartyId}.";
            return false;
        }

        if (requireReady && !allowActiveMapEvent && playerParty.MapEvent != null)
        {
            error = $"Player {controllerId} is already in a map event.";
            return false;
        }

        if (playerParty.MapFaction == null)
        {
            error = $"Player {controllerId} has no map faction.";
            return false;
        }

        return true;
    }

    private static MobileParty FindPeacePursuitFixture(MobileParty playerParty)
    {
        var playerPosition = playerParty.Position.ToVec2();
        return MobileParty.All
            .Where(p => IsPeacePursuitFixture(p, playerParty))
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(playerPosition))
            .FirstOrDefault();
    }

    private static bool IsPeacePursuitFixture(MobileParty party, MobileParty playerParty)
    {
        return party.IsActive &&
               !party.IsBandit &&
               !party.IsPlayerParty() &&
               party != playerParty &&
               party.MapEvent == null &&
               party.CurrentSettlement == null &&
               party.MemberRoster.TotalManCount > 0 &&
               party.MapFaction != null &&
               party.MapFaction != playerParty.MapFaction &&
               !FactionManager.IsAtWarAgainstFaction(party.MapFaction, playerParty.MapFaction) &&
               party.DefaultBehavior == AiBehavior.Hold &&
               party.PartyMoveMode == MoveModeType.Hold &&
               party.TargetParty == null;
    }

    private static string FormatPeacePursuitState(
        string prefix,
        IObjectManager objectManager,
        MobileParty party,
        MobileParty playerParty)
    {
        var registryId = objectManager.TryGetId(party, out string partyId) ? partyId : "none";
        var target = party.TargetParty == null ? "none" : party.TargetParty.StringId;
        var atWar = FactionManager.IsAtWarAgainstFaction(party.MapFaction, playerParty.MapFaction);
        var mapEvent = party.MapEvent == null ? "none" : party.MapEvent.ToString();

        return $"{prefix}: party={party.StringId}, registryId={registryId}, behavior={party.DefaultBehavior}, " +
               $"moveMode={party.PartyMoveMode}, target={target}, atWar={atWar}, mapEvent={mapEvent}.";
    }

    /// <summary>
    /// Kills a random troop from the enemy side of the current map event.
    /// </summary>
    [CommandLineArgumentFunction("kill_random_troop", "coop.debug.mapevent")]
    public static string KillRandomTroop(List<string> args)
    {
        var mapEvent = MobileParty.MainParty.MapEvent;
        if (mapEvent is null)
        {
            return "Main party is not in a map event";
        }

        var mainPartySide = MobileParty.MainParty.MapEventSide;
        if (mainPartySide is null)
        {
            return "Main party has no map event side";
        }

        var enemySide = mapEvent._sides
            .SingleOrDefault(side => side != mainPartySide);

        if (enemySide is null)
        {
            return "Failed to get enemy map event side";
        }

        var party = enemySide.Parties[MBRandom.RandomInt(enemySide.Parties.Count)];
        if (party is null)
        {
            return "Enemy side has no parties";
        }

        var troops = party.Troops;
        if (troops is null || troops.Count() == 0)
        {
            return "Enemy party has no troops";
        }

        var entries = troops._elementDictionary.ToArray();

        if (entries.Length == 0)
        {
            return "Enemy party has no troops";
        }

        var randomEntry = entries[MBRandom.RandomInt(entries.Length)];

        UniqueTroopDescriptor descriptor = randomEntry.Key;
        FlattenedTroopRosterElement troopElement = randomEntry.Value;

        try
        {
            enemySide.OnTroopKilled(descriptor);
        }
        catch (Exception ex)
        {
            return $"Failed to kill random troop: {ex.Message}";
        }

        return $"Killed random troop: {troopElement.Troop?.Name}";
    }

    /// <summary>
    /// Kills all but one troop from the enemy side of the current map event.
    /// </summary>
    [CommandLineArgumentFunction("kill_all_but_one", "coop.debug.mapevent")]
    public static string KillAllButOneTroop(List<string> args)
    {
        var mapEvent = MobileParty.MainParty.MapEvent;
        if (mapEvent is null)
        {
            return "Main party is not in a map event";
        }

        var mainPartySide = MobileParty.MainParty.MapEventSide;
        if (mainPartySide is null)
        {
            return "Main party has no map event side";
        }

        var enemySide = mapEvent._sides
            .SingleOrDefault(side => side != mainPartySide);

        if (enemySide is null)
        {
            return "Failed to get enemy map event side";
        }

        if (enemySide.Parties is null || enemySide.Parties.Count == 0)
        {
            return "Enemy side has no parties";
        }

        var allTroops = new List<(MapEventParty Party, UniqueTroopDescriptor Descriptor, FlattenedTroopRosterElement Element)>();

        foreach (var party in enemySide.Parties)
        {
            if (party?.Troops?._elementDictionary is null)
                continue;

            foreach (var entry in party.Troops._elementDictionary)
            {
                var descriptor = entry.Key;
                var element = entry.Value;

                allTroops.Add((party, descriptor, element));
            }
        }

        if (allTroops.Count == 0)
        {
            return "Enemy side has no troops";
        }

        if (allTroops.Count == 1)
        {
            return $"Enemy side already has only one troop: {allTroops[0].Element.Troop?.Name}";
        }

        var survivorIndex = MBRandom.RandomInt(allTroops.Count);
        var survivor = allTroops[survivorIndex];

        var killedCount = 0;

        for (var i = 0; i < allTroops.Count; i++)
        {
            if (i == survivorIndex)
                continue;

            try
            {
                enemySide.OnTroopKilled(allTroops[i].Descriptor);
                killedCount++;
            }
            catch (Exception ex)
            {

            }
        }

        return $"Killed {killedCount} troops. Survivor: {survivor.Element.Troop?.Name}";
    }

    /// <summary>
    /// Lists the fields and properties of the current PlayerEncounter.
    /// </summary>
    [CommandLineArgumentFunction("list_player_encounter", "coop.debug.mapevent")]
    public static string ListPlayerEncounter(List<string> args)
    {
        var playerEncounter = PlayerEncounter.Current;
        if (playerEncounter == null)
        {
            return "No current PlayerEncounter";
        }

        var sb = new StringBuilder();

        sb.AppendLine("PlayerEncounter:");
        AppendObjectDetails(sb, playerEncounter, "\t", "PlayerEncounter Details");

        var result = sb.ToString();

        Logger.Debug("{PlayerEncounter}", result);

        return result;
    }

    /// <summary>
    /// Prints a compact, teardown-focused snapshot of the current <see cref="PlayerEncounter"/> and the main
    /// party's map-event state. Run on each client after a battle to spot an encounter that did not tear down —
    /// e.g. PlayerEncounter.Current still PRESENT, or MainParty.MapEvent lingering on an already-finalized event.
    /// Unlike <c>list_player_encounter</c> (full reflection dump) this is short enough to diff across clients.
    /// </summary>
    [CommandLineArgumentFunction("encounter_state", "coop.debug.mapevent")]
    public static string EncounterState(List<string> args)
    {
        TryGetObjectManager(out var objectManager);

        var sb = new StringBuilder();

        var encounter = PlayerEncounter.Current;
        sb.AppendLine($"PlayerEncounter.Current: {(encounter == null ? "<null> (torn down)" : "PRESENT")}");
        if (encounter != null)
        {
            sb.AppendLine($"\tBattle:           {FormatMapEvent(PlayerEncounter.Battle, objectManager)}");
            sb.AppendLine($"\t_mapEvent:        {FormatMapEvent(encounter._mapEvent, objectManager)}");
            sb.AppendLine($"\tEncounteredParty: {FormatPartyBaseWithId(PlayerEncounter.EncounteredParty, objectManager)}");
            sb.AppendLine($"\t_attackerParty:   {FormatPartyBaseWithId(encounter._attackerParty, objectManager)}");
            sb.AppendLine($"\t_defenderParty:   {FormatPartyBaseWithId(encounter._defenderParty, objectManager)}");
        }

        var mainParty = MobileParty.MainParty;
        sb.AppendLine($"MainParty.MapEvent:      {FormatMapEvent(mainParty?.MapEvent, objectManager)}");

        var side = mainParty?.Party?.MapEventSide;
        if (side == null)
            sb.AppendLine("MainParty.MapEventSide:  <null>");
        else
            sb.AppendLine($"MainParty.MapEventSide:  leader={FormatPartyBaseWithId(side.LeaderParty, objectManager)} mainPartyIsLeader={side.LeaderParty == mainParty?.Party}");

        sb.AppendLine($"CurrentMenu:             {Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>"}");
        sb.AppendLine($"CurrentBattleSimulation: {(PlayerEncounter.CurrentBattleSimulation == null ? "<null>" : "PRESENT")}");
        sb.AppendLine($"MissionState.Current:    {(MissionState.Current == null ? "<null>" : "PRESENT")}");

        var result = sb.ToString();
        Logger.Debug("{EncounterState}", result);
        return result;
    }

    /// <summary>Shows, closes, or reports the live retreat confirmation for automated battle-exit testing.</summary>
    [CommandLineArgumentFunction("retreat_confirmation", "coop.debug.mapevent")]
    public static string RetreatConfirmation(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client in a battle mission.";

        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.retreat_confirmation <show|accept|cancel|state>";

        var handler = Mission.Current?.GetMissionBehavior<BasicMissionHandler>();
        if (handler == null)
            return "No active battle retreat handler.";

        switch (args[0].ToLowerInvariant())
        {
            case "show":
                if (handler.IsWarningWidgetOpened)
                    return "Retreat confirmation already open: true";

                handler.CreateWarningWidgetForResult(BattleEndLogic.ExitResult.NeedsPlayerConfirmation);
                return $"Retreat confirmation open: {handler.IsWarningWidgetOpened}";
            case "accept":
                if (!handler.IsWarningWidgetOpened)
                    return "Retreat confirmation is not open.";

                InformationManager.HideInquiry();
                handler.OnEventAcceptSelectionWidget();
                return "Retreat confirmation accepted.";
            case "cancel":
                if (!handler.IsWarningWidgetOpened)
                    return "Retreat confirmation is not open.";

                InformationManager.HideInquiry();
                handler.OnEventCancelSelectionWidget();
                return $"Retreat confirmation open: {handler.IsWarningWidgetOpened}";
            case "state":
                return $"Retreat confirmation open: {handler.IsWarningWidgetOpened}";
            default:
                return "Usage: coop.debug.mapevent.retreat_confirmation <show|accept|cancel|state>";
        }
    }

    /// <summary>Closes the current encounter conversation so vanilla can advance to battle choices.</summary>
    [CommandLineArgumentFunction("complete_encounter_meeting", "coop.debug.mapevent")]
    public static string CompleteEncounterMeeting(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client at the encounter meeting.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.complete_encounter_meeting";

        if (Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId != "encounter_meeting")
            return "The encounter meeting is not active.";

        var conversationManager = Campaign.Current.ConversationManager;
        if (!conversationManager.IsConversationInProgress)
            return "The encounter conversation is not active.";

        conversationManager.EndConversation();
        return "Encounter meeting completed.";
    }

    /// <summary>Runs the encounter menu's mission or simulation consequence for automated battle testing.</summary>
    [CommandLineArgumentFunction("choose_battle_mode", "coop.debug.mapevent")]
    public static string ChooseBattleMode(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client at the encounter menu.";

        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.choose_battle_mode <mission|simulation>";

        if (PlayerEncounter.Current == null)
            return "No active player encounter.";

        var behavior = Campaign.Current?.GetCampaignBehavior<EncounterGameMenuBehavior>();
        if (behavior == null)
            return "Encounter menu behavior is unavailable.";

        switch (args[0].ToLowerInvariant())
        {
            case "mission":
                behavior.game_menu_encounter_attack_on_consequence(null);
                return "Mission battle requested.";
            case "simulation":
                behavior.game_menu_encounter_order_attack_on_consequence(null);
                return "Battle simulation requested.";
            default:
                return "Usage: coop.debug.mapevent.choose_battle_mode <mission|simulation>";
        }
    }

    private static string FormatMapEvent(MapEvent mapEvent, IObjectManager objectManager)
    {
        if (mapEvent == null) return "<null>";

        var id = "<no id>";
        if (!mapEvent.IsFinalized && objectManager != null && objectManager.TryGetId(mapEvent, out var resolved))
            id = resolved;

        return $"id={id} finalized={mapEvent.IsFinalized} state={mapEvent.BattleState} winner={mapEvent.WinningSide}";
    }

    [CommandLineArgumentFunction("get_events", "coop.debug.mapevent")]
    public static string GetEvents(List<string> args)
    {
        var sb = new StringBuilder();

        if(!TryGetObjectManager(out var objectManager))
        {
            return "Failed to get object manager";
        }

        foreach(var mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            if (objectManager.TryGetIdWithLogging(mapEvent, out var id))
            {
                sb.AppendLine($"Map event id: {id}");
            }

            var partyNames = mapEvent.AttackerSide.Parties?
                .Select(party => party?.Party?.Name?.ToString() ?? "<null>")
                .ToArray() ?? Array.Empty<string>();
            sb.AppendLine($"\tAttacker: {string.Join(",", FormatSideNames(mapEvent.AttackerSide))}");
            sb.AppendLine($"\tDefender: {string.Join(",", FormatSideNames(mapEvent.DefenderSide))}");
        }

        return sb.ToString();
    }

    private static string[] FormatSideNames(MapEventSide side)
    {
        if (side == null)
            return new string[] { "<null>" };

        return side.Parties?
            .Select(party => party?.Party?.Name?.ToString() ?? "<null>")
            .ToArray() ?? Array.Empty<string>();
    }

    [CommandLineArgumentFunction("get_event", "coop.debug.mapevent")]
    public static string GetEvent(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.get_event <mapEventId>";
        }

        if (!TryGetObjectManager(out var objectManager))
        {
            return "Failed to get object manager";
        }

        var mapEventId = args[0];

        if (!objectManager.TryGetObjectWithLogging<MapEvent>(mapEventId, out var mapEvent))
        {
            return $"Failed to find MapEvent with id: {mapEventId}";
        }

        var sb = new StringBuilder();

        sb.AppendLine($"Map event id: {mapEventId}");
        sb.AppendLine();

        AppendMapEventSummary(sb, mapEvent);
        sb.AppendLine();

        var result = sb.ToString();

        Logger.Debug("{MapEvent}", result);

        return result;
    }

    private static void AppendMapEventSummary(StringBuilder sb, MapEvent mapEvent)
    {
        sb.AppendLine("Summary:");

        AppendSideSummary(sb, "Attacker", mapEvent.AttackerSide);
        AppendSideSummary(sb, "Defender", mapEvent.DefenderSide);
    }

    private static void AppendSideSummary(StringBuilder sb, string sideName, MapEventSide side)
    {
        if (side == null)
        {
            sb.AppendLine($"\t{sideName}: <null>");
            return;
        }

        sb.AppendLine($"\t{sideName}: {string.Join(", ", FormatSideNames(side))}");

        AppendObjectDetails(sb, side, "\t\t", "Side Details");

        sb.AppendLine("\t\tParties:");

        var parties = side.Parties;
        if (parties == null)
        {
            sb.AppendLine("\t\t\t<null>");
            return;
        }

        var index = 0;
        foreach (var party in parties)
        {
            sb.AppendLine($"\t\t\tParty[{index}]:");

            if (party == null)
            {
                sb.AppendLine("\t\t\t\t<null>");
            }
            else
            {
                AppendMapEventPartyDetails(sb, party, "\t\t\t\t");
            }

            index++;
        }
    }
    private static void AppendMapEventPartyDetails(StringBuilder sb, MapEventParty party, string indent)
    {
        var partyName = party.Party?.Name?.ToString() ?? "<null>";
        sb.AppendLine($"{indent}Party: {partyName}");

        AppendObjectDetails(sb, party, indent, "MapEventParty Details");
    }

    private static void AppendObjectDetails(StringBuilder sb, object obj, string indent, string title)
    {
        if (obj == null)
        {
            sb.AppendLine($"{indent}{title}: <null>");
            return;
        }

        var type = obj.GetType();

        sb.AppendLine($"{indent}{title}: {GetFriendlyTypeName(type)}");

        AppendFields(sb, obj, type, indent + "\t");
        AppendProperties(sb, obj, type, indent + "\t");
    }

    private static void AppendFields(StringBuilder sb, object obj, Type type, string indent)
    {
        sb.AppendLine($"{indent}Fields:");

        var fields = type.GetFields(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);

        if (fields.Length == 0)
        {
            sb.AppendLine($"{indent}\t<none>");
            return;
        }

        foreach (var field in fields.OrderBy(f => f.Name))
        {
            object value;

            try
            {
                value = field.GetValue(obj);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}\t{field.Name}: <failed: {ex.GetType().Name}>");
                continue;
            }

            sb.AppendLine($"{indent}\t{field.Name}: {FormatValue(value)}");
        }
    }

    private static void AppendProperties(StringBuilder sb, object obj, Type type, string indent)
    {
        sb.AppendLine($"{indent}Properties:");

        var properties = type.GetProperties(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);

        if (properties.Length == 0)
        {
            sb.AppendLine($"{indent}\t<none>");
            return;
        }

        foreach (var property in properties.OrderBy(p => p.Name))
        {
            if (property.GetIndexParameters().Length != 0)
            {
                sb.AppendLine($"{indent}\t{property.Name}: <indexed property>");
                continue;
            }

            object value;

            try
            {
                value = property.GetValue(obj, null);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}\t{property.Name}: <failed: {ex.GetType().Name}>");
                continue;
            }

            sb.AppendLine($"{indent}\t{property.Name}: {FormatValue(value)}");
        }
    }

    private static string FormatValue(object value)
    {
        if (value == null)
            return "<null>";

        if (value is string str)
            return str;

        if (value is TextObject textObject)
            return textObject.ToString();

        if (value is CharacterObject character)
            return FormatCharacter(character);

        if (value is MobileParty mobileParty)
            return FormatMobileParty(mobileParty);

        if (value is PartyBase partyBase)
            return FormatPartyBase(partyBase);

        if (value is IFaction faction)
            return faction.Name?.ToString() ?? faction.StringId ?? "<unnamed faction>";

        if (value is UniqueTroopDescriptor descriptor)
            return descriptor.ToString();

        if (value is IEnumerable enumerable && !(value is string))
            return FormatEnumerable(enumerable);

        return value.ToString();
    }

    private static string FormatEnumerable(IEnumerable enumerable)
    {
        var values = new List<string>();
        var count = 0;

        foreach (var item in enumerable)
        {
            if (count >= 20)
            {
                values.Add("...");
                break;
            }

            values.Add(FormatValue(item));
            count++;
        }

        return "[" + string.Join(", ", values) + "]";
    }

    private static string FormatCharacter(CharacterObject character)
    {
        if (character == null)
            return "<null>";

        var id = character.StringId ?? "<no id>";
        var name = character.Name?.ToString() ?? "<no name>";

        return $"{name} ({id})";
    }

    private static string FormatMobileParty(MobileParty party)
    {
        if (party == null)
            return "<null>";

        var id = party.StringId ?? "<no id>";
        var name = party.Name?.ToString() ?? "<no name>";

        return $"{name} ({id})";
    }

    private static string FormatPartyBase(PartyBase party)
    {
        if (party == null)
            return "<null>";

        var name = party.Name?.ToString() ?? "<no name>";

        return name;
    }

    private static string FormatPartyBaseWithId(PartyBase party, IObjectManager objectManager)
    {
        if (party == null)
            return "<null>";

        var partyBaseId = objectManager != null && objectManager.TryGetId(party, out string resolvedPartyBaseId)
            ? resolvedPartyBaseId
            : "<unregistered>";

        return $"{FormatPartyBase(party)} (PartyBase id {partyBaseId})";
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (type == null)
            return "<null>";

        if (!type.IsGenericType)
            return type.FullName ?? type.Name;

        var genericTypeName = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var tickIndex = genericTypeName.IndexOf('`');

        if (tickIndex >= 0)
            genericTypeName = genericTypeName.Substring(0, tickIndex);

        var genericArguments = type.GetGenericArguments()
            .Select(GetFriendlyTypeName)
            .ToArray();

        return genericTypeName + "<" + string.Join(", ", genericArguments) + ">";
    }
}

#if DEBUG
/// <summary>[Server -&gt; Client] Ends a live-test fixture mission without resolving its campaign battle.</summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkEndLateJoinModeFixtureMission : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkEndLateJoinModeFixtureMission(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}

/// <summary>Applies the server's live-test fixture mission-exit request on participating clients.</summary>
internal sealed class LateJoinModeFixtureMissionExitHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public LateJoinModeFixtureMissionExitHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<NetworkEndLateJoinModeFixtureMission>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkEndLateJoinModeFixtureMission>(Handle);
    }

    private void Handle(MessagePayload<NetworkEndLateJoinModeFixtureMission> payload)
    {
        if (ModInformation.IsServer)
            return;

        var mapEventId = payload.What.MapEventId;
        GameThread.RunSafe(() =>
        {
            var mapEvent = MobileParty.MainParty?.MapEvent;
            if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var localMapEventId) ||
                localMapEventId != mapEventId)
                return;

            var mission = Mission.Current ?? MissionState.Current?.CurrentMission;
            mission?.EndMission();
        }, context: nameof(NetworkEndLateJoinModeFixtureMission));
    }
}
#endif
