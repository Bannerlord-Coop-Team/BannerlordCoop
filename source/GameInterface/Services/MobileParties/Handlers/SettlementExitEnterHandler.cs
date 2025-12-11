using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.ItemRosters;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
 

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Handles changes to parties for settlement entry and exit.
/// </summary>
internal class SettlementExitEnterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementExitEnterHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

        public SettlementExitEnterHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;

            messageBroker.Subscribe<PartyEnterSettlement>(Handle);
            messageBroker.Subscribe<PartyLeaveSettlement>(Handle);
            messageBroker.Subscribe<StartSettlementEncounter>(Handle);
            messageBroker.Subscribe<EndSettlementEncounter>(Handle);
            messageBroker.Subscribe<CampaignReady>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PartyEnterSettlement>(Handle);
            messageBroker.Unsubscribe<PartyLeaveSettlement>(Handle);
            messageBroker.Unsubscribe<StartSettlementEncounter>(Handle);
            messageBroker.Unsubscribe<EndSettlementEncounter>(Handle);
            messageBroker.Unsubscribe<CampaignReady>(Handle);
        }

    private void Handle(MessagePayload<PartyEnterSettlement> obj)
    {
        var payload = obj.What;

        Logger.Information("PartyEnterSettlement reçu party={partyId} settlement={settlementId}", payload.PartyId, payload.SettlementId);

        var mobileParty = ResolveAndRegister<MobileParty>(payload.PartyId);
        if (mobileParty == null) { Logger.Error("PartyId not found: {id}", payload.PartyId); return; }

        var settlement = ResolveAndRegister<Settlement>(payload.SettlementId) ?? Settlement.Find(payload.SettlementId);
        if (settlement == null) { Logger.Error("SettlementId not found: {id}", payload.SettlementId); return; }

        if (mobileParty.IsPlayerParty() == false) return;

        if (mobileParty.IsPlayerParty() == false) return;

        if (mobileParty.CurrentSettlement == settlement)
        {
            Logger.Information("Déjà dans le settlement, ignore entrée party={partyId} settlement={settlementId}", mobileParty.StringId, settlement.StringId);
            return;
        }

        if (mobileParty.Party == null)
        {
            Logger.Information("MobileParty.Party null avant entrée, tentative OnFinishLoadState party={partyId}", mobileParty.StringId);
            GameLoopRunner.RunOnMainThread(() =>
            {
                mobileParty.OnFinishLoadState();
            }, blocking: true);
            Logger.Information("OnFinishLoadState exécuté party={partyId} partyNull={isNull}", mobileParty.StringId, mobileParty.Party == null);
        }

        if (settlement.Party == null)
        {
            Logger.Information("Settlement.Party null avant entrée, tentative InitSettlement settlement={settlementId}", settlement.StringId);
            GameLoopRunner.RunOnMainThread(() =>
            {
                settlement.InitSettlement();
            }, blocking: true);
            Logger.Information("InitSettlement exécuté settlement={settlementId} partyNull={isNull}", settlement.StringId, settlement.Party == null);
        }

        Logger.Information("Appel OverrideApplyForParty party={partyId} settlement={settlementId} currentSettlement={current}", mobileParty.StringId, settlement.StringId, mobileParty.CurrentSettlement?.StringId ?? "none");
        try
        {
            EnterSettlementActionPatches.OverrideApplyForParty(mobileParty, settlement);
        }
        catch (System.Exception ex)
        {
            Logger.Error(ex, "Erreur pendant OverrideApplyForParty");
            return;
        }
        Logger.Information("OverrideApplyForParty terminé party={partyId} settlement={settlementId} currentSettlement={current}", mobileParty.StringId, settlement.StringId, mobileParty.CurrentSettlement?.StringId ?? "none");
    }

    private void Handle(MessagePayload<PartyLeaveSettlement> obj)
    {
        var payload = obj.What;

        Logger.Information("PartyLeaveSettlement reçu party={partyId}", payload.PartyId);

        var mobileParty = ResolveAndRegister<MobileParty>(payload.PartyId);
        if (mobileParty == null) { Logger.Error("PartyId not found: {id}", payload.PartyId); return; }

        if (mobileParty.Party == null)
        {
            Logger.Information("MobileParty.Party null avant sortie, tentative OnFinishLoadState party={partyId}", mobileParty.StringId);
            GameLoopRunner.RunOnMainThread(() =>
            {
                mobileParty.OnFinishLoadState();
            }, blocking: true);
            Logger.Information("OnFinishLoadState exécuté party={partyId} partyNull={isNull}", mobileParty.StringId, mobileParty.Party == null);
        }

        Logger.Information("Appel OverrideApplyForParty(Leave) party={partyId} currentSettlement={current}", mobileParty.StringId, mobileParty.CurrentSettlement?.StringId ?? "none");
        LeaveSettlementActionPatches.OverrideApplyForParty(mobileParty);
        Logger.Information("OverrideApplyForParty(Leave) terminé party={partyId} currentSettlement={current}", mobileParty.StringId, mobileParty.CurrentSettlement?.StringId ?? "none");
    }

    private void Handle(MessagePayload<StartSettlementEncounter> obj)
    {
        var payload = obj.What;

        Logger.Information("StartSettlementEncounter reçu party={partyId} settlement={settlementId}", payload.PartyId, payload.SettlementId);

        

        // Limiter aux actions de la party du joueur
        try
        {
            var maybeParty = MobileParty.All.FirstOrDefault(p => p.StringId == payload.PartyId);
            if (maybeParty != null && maybeParty.IsPlayerParty() == false)
            {
                Logger.Information("StartSettlementEncounter ignoré: party non joueur {partyId}", payload.PartyId);
                return;
            }
        }
        catch { }

        var mobileParty = ResolveAndRegister<MobileParty>(payload.PartyId);
        if (mobileParty == null) { Logger.Error("PartyId not found: {id}", payload.PartyId); return; }

        var settlement = ResolveAndRegister<Settlement>(payload.SettlementId);
        if (settlement == null) { Logger.Error("SettlementId not found: {id}", payload.SettlementId); return; }

        if (settlement.Party == null)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    settlement.InitSettlement();
                }
            }, blocking: true);
        }

        if (mobileParty.Party == null)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    mobileParty.OnFinishLoadState();
                }
            }, blocking: true);
        }

        var settlementParty = settlement.Party;
        if (settlementParty == null)
        {
            Logger.Error("Settlement {settlementName} did not have a party value", settlement.Name);
            return;
        }

        if (mobileParty?.Party == null)
        {
            Logger.Error("MobileParty {partyId} did not have a Party value", mobileParty?.StringId ?? payload.PartyId);
            return;
        }

        try
        {
            var mpRoster = mobileParty.ItemRoster;
            if (mpRoster != null)
            {
                ItemRosterLookup.Set(mpRoster, mobileParty.Party);
            }
            var stRoster = settlement.ItemRoster;
            if (stRoster != null)
            {
                ItemRosterLookup.Set(stRoster, settlement.Party);
            }
        }
        catch { }

        

        if (PlayerEncounter.Current != null)
        {
            var activeSettlement = PlayerEncounter.EncounterSettlement;
            var needRelink = activeSettlement == null || activeSettlement != settlement;
            if (needRelink)
            {
                Logger.Information("PlayerEncounter actif mais mal lié, re-init party={partyId} settlement={settlementId}", mobileParty.StringId, settlement.StringId);
                GameLoopRunner.RunOnMainThread(() =>
                {
                    using (new AllowedThread())
                    {
                        PlayerEncounter.Current.Init(mobileParty.Party, settlement.Party, settlement);
                    }
                }, blocking: true);
                Logger.Information("Re-init PlayerEncounter terminé party={partyId} settlement={settlementId}", mobileParty.StringId, settlement.StringId);
            }
            else
            {
                Logger.Information("PlayerEncounter déjà actif et correctement lié, skip start");
            }
            return;
        }

        // Vérifier état de jeu
        if (GameStateManager.Current == null || GameStateManager.Current.ActiveState is not MapState)
        {
            Logger.Information("StartSettlementEncounter ignoré: état de jeu non MapState");
            return;
        }

        Logger.Information("Démarrage EncounterManager party={partyId} settlement={settlementId}", mobileParty.StringId, settlement.StringId);
        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                EncounterManager.StartSettlementEncounter(mobileParty, settlement);
                Logger.Information("EncounterManager.StartSettlementEncounter ok");
            }
            catch (System.Exception ex)
            {
                Logger.Error(ex, "EncounterManager failed, fallback PlayerEncounter.Start");
                try
                {
                    using (new AllowedThread())
                    {
                        PlayerEncounter.Start();
                        if (PlayerEncounter.Current == null)
                        {
                            Logger.Error("PlayerEncounter.Current est null après Start");
                            return;
                        }
                        PlayerEncounter.Current.Init(mobileParty.Party, settlement.Party, settlement);
                        Logger.Information("PlayerEncounter.Init ok party={partyId} settlement={settlementId}", mobileParty.StringId, settlement.StringId);
                    }
                }
                catch (System.Exception ex2)
                {
                    Logger.Error(ex2, "Fallback PlayerEncounter failed");
                }
            }
        }, blocking: true);
        Logger.Information("StartSettlementEncounter terminé party={partyId} settlement={settlementId}", mobileParty.StringId, settlement.StringId);
    }

    private void Handle(MessagePayload<EndSettlementEncounter> obj)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            PlayerEncounter.Finish(true);
            Campaign.Current.SaveHandler.SignalAutoSave();
        }, blocking: true);
    }

    private void Handle(MessagePayload<CampaignReady> obj)
    {
        try
        {
            var settlements = TaleWorlds.CampaignSystem.Settlements.Settlement.All;
            int stAdded = 0, stDup = 0, spAdded = 0, spDup = 0;
            foreach (var s in settlements)
            {
                if (s == null) continue;
                if (objectManager.Contains(s.StringId)) stDup++; else { objectManager.AddExisting(s.StringId, s); stAdded++; }
                var sp = s.Party;
                if (sp != null)
                {
                    var sid = $"{nameof(PartyBase)}_{s.StringId}";
                    if (objectManager.Contains(sid)) spDup++; else { objectManager.AddExisting(sid, sp); spAdded++; }
                }
            }
            var parties = TaleWorlds.CampaignSystem.Party.MobileParty.All;
            int mpAdded = 0, mpDup = 0, pbAdded = 0, pbDup = 0;
            foreach (var p in parties)
            {
                if (p == null) continue;
                if (objectManager.Contains(p.StringId)) mpDup++; else { objectManager.AddExisting(p.StringId, p); mpAdded++; }
                var pb = p.Party;
                if (pb != null)
                {
                    var pid = $"{nameof(PartyBase)}_{p.StringId}";
                    if (objectManager.Contains(pid)) pbDup++; else { objectManager.AddExisting(pid, pb); pbAdded++; }
                }
            }
            Logger.Information("Hydratation CampaignReady: Settlements ajoutés={stAdded} doublons={stDup} | SettlementParty ajoutés={spAdded} doublons={spDup} | MobileParty ajoutés={mpAdded} doublons={mpDup} | PartyBase ajoutés={pbAdded} doublons={pbDup}");
        }
        catch { }
    }

    private T ResolveAndRegister<T>(string id) where T : class
    {
        if (objectManager.TryGetObject<T>(id, out var obj)) return obj;
        object found = null;
        var com = Campaign.Current?.CampaignObjectManager;
        var mbo = MBObjectManager.Instance;
        try
        {
            if (com != null)
            {
                var mi = com.GetType().GetMethod("Find")?.MakeGenericMethod(typeof(T));
                if (mi != null) found = mi.Invoke(com, new object[] { id });
            }
        }
        catch { }
        if (found == null)
        {
            try
            {
                if (mbo != null)
                {
                    var containsMi = mbo.GetType().GetMethod("ContainsObject")?.MakeGenericMethod(typeof(T));
                    var getMi = mbo.GetType().GetMethod("GetObject")?.MakeGenericMethod(typeof(T));
                    if (containsMi != null && getMi != null)
                    {
                        var has = (bool)containsMi.Invoke(mbo, new object[] { id });
                        if (has) found = getMi.Invoke(mbo, new object[] { id });
                    }
                }
            }
            catch { }
        }
        if (found is T t)
        {
            if (objectManager.Contains(id) == false)
                objectManager.AddExisting(id, t);
            return t;
        }
        return null;
    }
}
