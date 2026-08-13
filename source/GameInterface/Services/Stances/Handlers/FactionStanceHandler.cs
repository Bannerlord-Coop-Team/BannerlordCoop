using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Stances.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Stances.Handlers
{
    /// <summary>
    /// Applies replicated faction stance changes (war / peace) on the receiving machine by
    /// re-running the vanilla action under AllowedThread, which re-fires the campaign events
    /// client-side without re-announcing.
    /// </summary>
    public class FactionStanceHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<FactionStanceHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
#if DEBUG
        private NetworkRestoreMountedBattleStance lastMountedBattleStanceRestore;
#endif

        public FactionStanceHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<DeclareWarChanged>(HandleDeclareWar);
            messageBroker.Subscribe<MakePeaceChanged>(HandleMakePeace);
#if DEBUG
            messageBroker.Subscribe<NetworkRestoreMountedBattleStance>(HandleMountedBattleStanceRestore);
#endif
        }

        private void HandleDeclareWar(MessagePayload<DeclareWarChanged> obj)
        {
            var payload = obj.What;
            if (!TryGetFaction(payload.Faction1Id, out var faction1)) return;
            if (!TryGetFaction(payload.Faction2Id, out var faction2)) return;

            // ApplyInternal is the funnel for every war cause; calling it directly (publicized)
            // preserves the original DeclareWarDetail so detail-sensitive client listeners match the server.
            GameThread.Run(() =>
            {
                using (new AllowedThread())
                {
                    DeclareWarAction.ApplyInternal(faction1, faction2, (DeclareWarAction.DeclareWarDetail)payload.Detail);
                }
            }, true);
        }

        private void HandleMakePeace(MessagePayload<MakePeaceChanged> obj)
        {
            var payload = obj.What;
            if (!TryGetFaction(payload.Faction1Id, out var faction1)) return;
            if (!TryGetFaction(payload.Faction2Id, out var faction2)) return;

            // ApplyInternal is the funnel for every peace cause; calling it directly (publicized)
            // preserves the original MakePeaceDetail and the daily tribute.
            GameThread.Run(() =>
            {
                using (new AllowedThread())
                {
                    MakePeaceAction.ApplyInternal(faction1, faction2, payload.DailyTribute, payload.DailyTributeDuration, (MakePeaceAction.MakePeaceDetail)payload.Detail);
                }
            }, true);
        }

        private bool TryGetFaction(string id, out IFaction faction)
        {
            if (objectManager.TryGetObject(id, out Kingdom kingdom))
            {
                faction = kingdom;
                return true;
            }
            if (objectManager.TryGetObject(id, out Clan clan))
            {
                faction = clan;
                return true;
            }
            Logger.Debug("Faction not found in FactionStanceHandler with id: {id}", id);
            faction = null;
            return false;
        }

#if DEBUG
        private void HandleMountedBattleStanceRestore(
            MessagePayload<NetworkRestoreMountedBattleStance> payload)
        {
            if (ModInformation.IsServer) return;

            NetworkRestoreMountedBattleStance restore = payload.What;
            GameThread.RunSafe(() =>
            {
                if (!TryGetFaction(restore.Faction1Id, out var faction1) ||
                    !TryGetFaction(restore.Faction2Id, out var faction2))
                {
                    return;
                }

                using (new AllowedThread())
                {
                    if (restore.RestoreExactSnapshot)
                    {
                        ApplyMountedBattleStanceRestore(
                            restore,
                            faction1,
                            faction2);
                    }
                    else
                    {
                        ApplyMountedBattleStanceType(
                            restore,
                            faction1,
                            faction2);
                    }
                }
                if (restore.RestoreExactSnapshot)
                    lastMountedBattleStanceRestore = restore;
            }, blocking: true, context: nameof(NetworkRestoreMountedBattleStance));
        }

        public bool TryGetMountedBattleStanceRestoreState(
            string fixtureToken,
            out NetworkRestoreMountedBattleStance restore,
            out bool matches)
        {
            restore = lastMountedBattleStanceRestore;
            matches = false;
            if (restore == null || restore.FixtureToken != fixtureToken ||
                !TryGetFaction(restore.Faction1Id, out var faction1) ||
                !TryGetFaction(restore.Faction2Id, out var faction2))
            {
                return false;
            }

            matches = MountedBattleStanceMatches(restore, faction1, faction2);
            return true;
        }

        private static void ApplyMountedBattleStanceRestore(
            NetworkRestoreMountedBattleStance restore,
            IFaction faction1,
            IFaction faction2)
        {
            StanceLink stance = FactionManager.Instance.GetStanceLinkInternal(
                faction1,
                faction2);
            ApplyMountedBattleStanceFields(restore, stance, faction1, faction2);
            if (restore.HasFaction1PoliticalStagnation &&
                faction1 is Kingdom kingdom1)
            {
                kingdom1.PoliticalStagnation = restore.Faction1PoliticalStagnation;
            }
            if (restore.HasFaction2PoliticalStagnation &&
                faction2 is Kingdom kingdom2)
            {
                kingdom2.PoliticalStagnation = restore.Faction2PoliticalStagnation;
            }

            faction1.UpdateFactionsAtWarWith();
            faction2.UpdateFactionsAtWarWith();
        }

        private static void ApplyMountedBattleStanceType(
            NetworkRestoreMountedBattleStance restore,
            IFaction faction1,
            IFaction faction2)
        {
            StanceLink stance = FactionManager.Instance.GetStanceLinkInternal(
                faction1,
                faction2);
            stance._stanceType = (StanceType)restore.StanceType;
            faction1.UpdateFactionsAtWarWith();
            faction2.UpdateFactionsAtWarWith();
        }

        internal static void ApplyMountedBattleStanceFields(
            NetworkRestoreMountedBattleStance restore,
            StanceLink stance,
            IFaction faction1,
            IFaction faction2)
        {
            if (!TryGetMountedBattleStanceOrientation(
                    stance,
                    faction1,
                    faction2,
                    out bool reversed))
            {
                throw new InvalidOperationException(
                    "The mounted battle stance does not match the restore factions.");
            }

            stance._stanceType = (StanceType)restore.StanceType;
            stance.BehaviorPriority = restore.BehaviorPriority;
            stance._warStartDate = new CampaignTime(restore.WarStartDateTicks);
            stance._peaceDeclarationDate =
                new CampaignTime(restore.PeaceDeclarationDateTicks);
            stance._troopCasualties1 = reversed
                ? restore.TroopCasualties2
                : restore.TroopCasualties1;
            stance._troopCasualties2 = reversed
                ? restore.TroopCasualties1
                : restore.TroopCasualties2;
            stance.ShipCasualties1 = reversed
                ? restore.ShipCasualties2
                : restore.ShipCasualties1;
            stance.ShipCasualties2 = reversed
                ? restore.ShipCasualties1
                : restore.ShipCasualties2;
            stance._successfulSieges1 = reversed
                ? restore.SuccessfulSieges2
                : restore.SuccessfulSieges1;
            stance._successfulSieges2 = reversed
                ? restore.SuccessfulSieges1
                : restore.SuccessfulSieges2;
            stance._successfulRaids1 = reversed
                ? restore.SuccessfulRaids2
                : restore.SuccessfulRaids1;
            stance._successfulRaids2 = reversed
                ? restore.SuccessfulRaids1
                : restore.SuccessfulRaids2;
            stance._totalTributePaidFrom1To2 =
                reversed
                    ? -restore.TotalTributePaidFrom1To2
                    : restore.TotalTributePaidFrom1To2;
            stance._dailyTributeFrom1To2 = reversed
                ? -restore.DailyTributeFrom1To2
                : restore.DailyTributeFrom1To2;
            stance._dailyTributeInstallments = restore.DailyTributeInstallments;
            stance._successfulTownSieges1 = reversed
                ? restore.SuccessfulTownSieges2
                : restore.SuccessfulTownSieges1;
            stance._successfulTownSieges2 = reversed
                ? restore.SuccessfulTownSieges1
                : restore.SuccessfulTownSieges2;
        }

        private static bool MountedBattleStanceMatches(
            NetworkRestoreMountedBattleStance restore,
            IFaction faction1,
            IFaction faction2)
        {
            StanceLink stance = FactionManager.Instance.GetStanceLinkInternal(
                faction1,
                faction2);
            if (!TryGetMountedBattleStanceOrientation(
                    stance,
                    faction1,
                    faction2,
                    out bool reversed))
            {
                return false;
            }

            return (int)stance._stanceType == restore.StanceType &&
                   stance.BehaviorPriority == restore.BehaviorPriority &&
                   stance._warStartDate.NumTicks == restore.WarStartDateTicks &&
                   stance._peaceDeclarationDate.NumTicks ==
                       restore.PeaceDeclarationDateTicks &&
                   stance._troopCasualties1 == (reversed
                       ? restore.TroopCasualties2
                       : restore.TroopCasualties1) &&
                   stance._troopCasualties2 == (reversed
                       ? restore.TroopCasualties1
                       : restore.TroopCasualties2) &&
                   stance.ShipCasualties1 == (reversed
                       ? restore.ShipCasualties2
                       : restore.ShipCasualties1) &&
                   stance.ShipCasualties2 == (reversed
                       ? restore.ShipCasualties1
                       : restore.ShipCasualties2) &&
                   stance._successfulSieges1 == (reversed
                       ? restore.SuccessfulSieges2
                       : restore.SuccessfulSieges1) &&
                   stance._successfulSieges2 == (reversed
                       ? restore.SuccessfulSieges1
                       : restore.SuccessfulSieges2) &&
                   stance._successfulRaids1 == (reversed
                       ? restore.SuccessfulRaids2
                       : restore.SuccessfulRaids1) &&
                   stance._successfulRaids2 == (reversed
                       ? restore.SuccessfulRaids1
                       : restore.SuccessfulRaids2) &&
                   stance._totalTributePaidFrom1To2 ==
                       (reversed
                           ? -restore.TotalTributePaidFrom1To2
                           : restore.TotalTributePaidFrom1To2) &&
                   stance._dailyTributeFrom1To2 ==
                       (reversed
                           ? -restore.DailyTributeFrom1To2
                           : restore.DailyTributeFrom1To2) &&
                   stance._dailyTributeInstallments ==
                       restore.DailyTributeInstallments &&
                   stance._successfulTownSieges1 ==
                       (reversed
                           ? restore.SuccessfulTownSieges2
                           : restore.SuccessfulTownSieges1) &&
                   stance._successfulTownSieges2 ==
                       (reversed
                           ? restore.SuccessfulTownSieges1
                           : restore.SuccessfulTownSieges2) &&
                   PoliticalStagnationMatches(
                       faction1,
                       restore.HasFaction1PoliticalStagnation,
                       restore.Faction1PoliticalStagnation) &&
                   PoliticalStagnationMatches(
                       faction2,
                       restore.HasFaction2PoliticalStagnation,
                       restore.Faction2PoliticalStagnation);
        }

        private static bool TryGetMountedBattleStanceOrientation(
            StanceLink stance,
            IFaction faction1,
            IFaction faction2,
            out bool reversed)
        {
            reversed = false;
            if (stance.Faction1 == faction1 && stance.Faction2 == faction2)
                return true;
            if (stance.Faction1 != faction2 || stance.Faction2 != faction1)
                return false;

            reversed = true;
            return true;
        }

        private static bool PoliticalStagnationMatches(
            IFaction faction,
            bool hasValue,
            int value) =>
            hasValue
                ? faction is Kingdom kingdom && kingdom.PoliticalStagnation == value
                : !(faction is Kingdom);
#endif

        public void Dispose()
        {
            messageBroker.Unsubscribe<DeclareWarChanged>(HandleDeclareWar);
            messageBroker.Unsubscribe<MakePeaceChanged>(HandleMakePeace);
#if DEBUG
            messageBroker.Unsubscribe<NetworkRestoreMountedBattleStance>(HandleMountedBattleStanceRestore);
#endif
        }
    }
}
