using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Villages.Interfaces;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

internal class PlayerPartyHostileEncounterService : IPlayerPartyHostileEncounterService
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerPartyHostileEncounterService>();

    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly IMapEventInitializationBarrier initializationBarrier;

    public PlayerPartyHostileEncounterService(
        IObjectManager objectManager,
        INetwork network,
        IMessageBroker messageBroker,
        IPlayerManager playerManager,
        IMapEventInitializationBarrier initializationBarrier)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.initializationBarrier = initializationBarrier;
    }

    public bool CanStartHostileEncounter(PartyBase initiatorParty, PartyBase responderParty)
    {
        return HasValidHostileEncounterParties(initiatorParty, responderParty);
    }

    public bool TryStartHostileEncounter(string sessionId, string initiatorPartyId, string responderPartyId, bool responderSurrenders)
    {
        if (ModInformation.IsClient) return false;

        var started = false;
        RunOnGameThread(
            () => started = TryStartHostileEncounterOnGameThread(sessionId, initiatorPartyId, responderPartyId, responderSurrenders),
            "Start player-party hostile encounter");

        return started;
    }

    private bool TryStartHostileEncounterOnGameThread(string sessionId, string initiatorPartyId, string responderPartyId, bool responderSurrenders)
    {
        if (!TryResolveParties(initiatorPartyId, responderPartyId, out var initiatorParty, out var responderParty))
            return false;

        if (!CanStartHostileEncounter(initiatorParty, responderParty))
        {
            Logger.Warning(
                "Unable to start player-party hostile demand encounter. SessionId={SessionId}, InitiatorPartyId={InitiatorPartyId}, ResponderPartyId={ResponderPartyId}",
                sessionId,
                initiatorPartyId,
                responderPartyId);
            return false;
        }

        DeclareWar(initiatorParty, responderParty);
        if (responderSurrenders)
            return TrySurrenderResponder(sessionId, initiatorPartyId, responderPartyId, initiatorParty, responderParty);

        return TryCreateAndStartEncounter(sessionId, initiatorPartyId, responderPartyId, initiatorParty, responderParty);
    }

    private bool TryResolveParties(string initiatorPartyId, string responderPartyId, out PartyBase initiatorParty, out PartyBase responderParty)
    {
        initiatorParty = null;
        responderParty = null;

        if (!objectManager.TryGetObjectWithLogging<PartyBase>(initiatorPartyId, out initiatorParty))
            return false;

        return objectManager.TryGetObjectWithLogging<PartyBase>(responderPartyId, out responderParty);
    }

    private bool TryCreateAndStartEncounter(
        string sessionId,
        string initiatorPartyId,
        string responderPartyId,
        PartyBase initiatorParty,
        PartyBase responderParty)
    {
        var mapEvent = MapEventBattleFactory.CreateMapEvent(initiatorParty, responderParty, DefaultFieldBattleFlags());
        if (mapEvent == null)
        {
            Logger.Error(
                "Unable to start player-party hostile encounter: field map event was not created. SessionId={SessionId}, InitiatorPartyId={InitiatorPartyId}, ResponderPartyId={ResponderPartyId}",
                sessionId,
                initiatorPartyId,
                responderPartyId);
            return false;
        }

        if (!objectManager.TryGetIdWithLogging(mapEvent, out var mapEventId))
            return false;

        initializationBarrier.CommitServer(mapEvent);

        network.SendAll(new NetworkPlayerPartyHostileEncounterStarted(
            sessionId,
            initiatorPartyId,
            responderPartyId,
            mapEventId));

        return true;
    }

    private bool TrySurrenderResponder(
        string sessionId,
        string initiatorPartyId,
        string responderPartyId,
        PartyBase initiatorParty,
        PartyBase responderParty)
    {
        try
        {
            if (!TryResolvePlayerHero(responderParty, out var responderHero))
                return false;

            if (!responderHero.IsPrisoner)
            {
                PrepareHeroForCapture(responderHero, responderParty.MobileParty);
                TakePrisonerAction.Apply(initiatorParty, responderHero);
            }

            PvpEncounterCloseSender.Send(network, messageBroker, this, new[] { initiatorPartyId, responderPartyId }, responderPartyId);
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(
                e,
                "Failed to auto-surrender player-party hostile demand. SessionId={SessionId}, InitiatorPartyId={InitiatorPartyId}, ResponderPartyId={ResponderPartyId}",
                sessionId,
                initiatorPartyId,
                responderPartyId);
            return false;
        }
    }

    private bool TryResolvePlayerHero(PartyBase party, out Hero hero)
    {
        hero = null;

        if (party?.MobileParty == null)
            return false;

        if (!objectManager.TryGetIdWithLogging(party.MobileParty, out var mobilePartyId))
            return false;

        var player = playerManager.Players.FirstOrDefault(player => player.MobilePartyId == mobilePartyId);
        if (player == null)
        {
            Logger.Warning("Could not resolve a registered player for surrendered party {PartyId}", mobilePartyId);
            return false;
        }

        return objectManager.TryGetObjectWithLogging(player.HeroId, out hero);
    }

    private static void PrepareHeroForCapture(Hero hero, MobileParty playerParty)
    {
        using (new AllowedThread())
        {
            if (hero.PartyBelongedTo != playerParty)
                hero.PartyBelongedTo = playerParty;

            if (!playerParty.MemberRoster.Contains(hero.CharacterObject))
                playerParty.MemberRoster.AddToCounts(hero.CharacterObject, 1);
        }
    }

    private static bool HasValidHostileEncounterParties(PartyBase initiatorParty, PartyBase responderParty)
    {
        if (initiatorParty?.MobileParty == null || responderParty?.MobileParty == null)
            return false;

        if (initiatorParty.MapEvent != null || responderParty.MapEvent != null)
            return false;

        var initiatorFaction = GetMapFaction(initiatorParty);
        var responderFaction = GetMapFaction(responderParty);
        return initiatorFaction != null && responderFaction != null && initiatorFaction != responderFaction;
    }

    private static bool AreHostile(PartyBase initiatorParty, PartyBase responderParty)
    {
        var initiatorFaction = GetMapFaction(initiatorParty);
        var responderFaction = GetMapFaction(responderParty);

        if (initiatorFaction == null || responderFaction == null || initiatorFaction == responderFaction)
            return false;

        return VillageHostileFactionStanceHelper.HasWarStance(initiatorFaction, responderFaction) ||
               IsAtWarAgainstFaction(initiatorFaction, responderFaction) ||
               HasFactionWar(initiatorFaction, responderFaction) ||
               HasFactionWar(responderFaction, initiatorFaction);
    }

    private static bool IsAtWarAgainstFaction(IFaction faction, IFaction otherFaction)
    {
        try
        {
            return FactionManager.IsAtWarAgainstFaction(faction, otherFaction);
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }

    private static bool HasFactionWar(IFaction faction, IFaction otherFaction)
    {
        try
        {
            return faction.FactionsAtWarWith?.Contains(otherFaction) == true;
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }

    private static void DeclareWar(PartyBase initiatorParty, PartyBase responderParty)
    {
        var initiatorFaction = GetMapFaction(initiatorParty);
        var responderFaction = GetMapFaction(responderParty);
        if (initiatorFaction == null || responderFaction == null || initiatorFaction == responderFaction)
            return;

        if (AreHostile(initiatorParty, responderParty))
            return;

        Logger.Debug(
            "Applying player-party hostile demand war between {InitiatorFaction} and {ResponderFaction}",
            initiatorFaction.Name,
            responderFaction.Name);
        DeclareWarAction.ApplyByPlayerHostility(initiatorFaction, responderFaction);
        VillageHostileFactionStanceHelper.ApplyWarStance(initiatorFaction, responderFaction);
    }

    private static IFaction GetMapFaction(PartyBase party)
        => party?.MapFaction?.MapFaction ?? party?.MapFaction;

    private static BattleCreationFlags DefaultFieldBattleFlags()
        => new BattleCreationFlags(
            forceRaid: false,
            forceSallyOut: false,
            forceVolunteers: false,
            forceSupplies: false,
            isSallyOutAmbush: false,
            forceBlockadeAttack: false,
            forceBlockadeSallyOutAttack: false,
            forceHideoutSendTroops: false);

    private static void RunOnGameThread(Action action, string context)
    {
        if (!GameThread.Instance.IsInitialized || GameThread.Instance.IsGameThread)
        {
            action();
            return;
        }

        GameThread.RunSafe(action, blocking: true, context: context);
    }
}
