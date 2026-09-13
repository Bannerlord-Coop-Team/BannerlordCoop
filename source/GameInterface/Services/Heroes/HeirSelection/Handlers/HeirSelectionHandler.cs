using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Network.Messages;
using Common.Util;
using GameInterface.CoopSessionData;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.HeirSelection.Interfaces;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Players.Handlers;
using GameInterface.Services.Players.Messages;
using GameInterface.Services.UI.Cutscenes.Handlers;
using LiteNetLib;
using SandBox.View.Map;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Heroes.HeirSelection.Handlers;

internal class HeirSelectionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeirSelectionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IPlayerPartyRestorer playerPartyRestorer;
    private readonly IApplyHeirSelectionActionInterface applyHeirSelectionActionInterface;
    private readonly IHeirSelectionCampaignBehaviorInterface heirSelectionCampaignBehaviorInterface;
    private readonly ICoopSessionMigrator coopSessionMigrator;
    private readonly PlayerDeathCutsceneHandler cutscenesHandler;
    private readonly PlayerDeletionHandler playerDeletionHandler;
    private readonly ISendCoalescer sendCoalescer;
    private readonly Dictionary<string, IMessage> sentSelections = new();
    private double lastCheckDays = -1;
    private int presentationVersion;
    private bool showingWaitInquiry;

    public bool IsAppointingClanLeader { get; private set; }

    public HeirSelectionHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IPlayerPartyRestorer playerPartyRestorer,
        IApplyHeirSelectionActionInterface applyHeirSelectionActionInterface,
        IHeirSelectionCampaignBehaviorInterface heirSelectionCampaignBehaviorInterface,
        ICoopSessionMigrator coopSessionMigrator,
        PlayerDeathCutsceneHandler cutscenesHandler,
        PlayerDeletionHandler playerDeletionHandler,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.playerPartyRestorer = playerPartyRestorer;
        this.applyHeirSelectionActionInterface = applyHeirSelectionActionInterface;
        this.heirSelectionCampaignBehaviorInterface = heirSelectionCampaignBehaviorInterface;
        this.coopSessionMigrator = coopSessionMigrator;
        this.cutscenesHandler = cutscenesHandler;
        this.playerDeletionHandler = playerDeletionHandler;
        this.sendCoalescer = sendCoalescer;

        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
        messageBroker.Subscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);

        messageBroker.Subscribe<PlayerHeirSelectionRequested>(Handle_PlayerHeirSelectionRequested);
        messageBroker.Subscribe<NetworkClientSelectHeir>(Handle_NetworkClientSelectHeir);

        messageBroker.Subscribe<HeirSelectionOver>(Handle_HeirSelectionOver);
        messageBroker.Subscribe<NetworkHeirSelectionOver>(Handle_NetworkHeirSelectionOver);

        messageBroker.Subscribe<ChangePlayerCharacterAfterHeirSelection>(Handle_ChangePlayerCharacterAfterHeirSelection);
        messageBroker.Subscribe<NetworkChangePlayerCharacterAfterHeirSelection>(Handle_NetworkChangePlayerCharacterAfterHeirSelection);

        messageBroker.Subscribe<PlayerCharacterChangedAfterHeirSelection>(Handle_PlayerCharacterChangedAfterHeirSelection);
        messageBroker.Subscribe<NetworkPlayerCharacterChangedAfterHeirSelection>(Handle_NetworkPlayerCharacterChangedAfterHeirSelection);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
        messageBroker.Unsubscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<PlayerHeirSelectionRequested>(Handle_PlayerHeirSelectionRequested);
        messageBroker.Unsubscribe<NetworkClientSelectHeir>(Handle_NetworkClientSelectHeir);

        messageBroker.Unsubscribe<HeirSelectionOver>(Handle_HeirSelectionOver);
        messageBroker.Unsubscribe<NetworkHeirSelectionOver>(Handle_NetworkHeirSelectionOver);

        messageBroker.Unsubscribe<ChangePlayerCharacterAfterHeirSelection>(Handle_ChangePlayerCharacterAfterHeirSelection);
        messageBroker.Unsubscribe<NetworkChangePlayerCharacterAfterHeirSelection>(Handle_NetworkChangePlayerCharacterAfterHeirSelection);

        messageBroker.Unsubscribe<PlayerCharacterChangedAfterHeirSelection>(Handle_PlayerCharacterChangedAfterHeirSelection);
        messageBroker.Unsubscribe<NetworkPlayerCharacterChangedAfterHeirSelection>(Handle_NetworkPlayerCharacterChangedAfterHeirSelection);
    }

    private void Handle_PlayerHeirSelectionRequested(MessagePayload<PlayerHeirSelectionRequested> obj)
    {
        var playerHero = obj.What.PlayerHero;
        if (playerHero == null || !playerHero.IsDead) return;

        if (objectManager.TryGetId(playerHero, out var heroId)) sentSelections.Remove(heroId);
        // Finish the current batch of deaths before giving either spouse a choice.
        GameThread.EnqueueSafe(() => RefreshSuccessions(playerHero.Clan));
    }

    private void Handle_CampaignTick(MessagePayload<CampaignTick> obj)
    {
        if (ModInformation.IsClient || Campaign.Current == null) return;
        var days = CampaignTime.Now.ToDays;
        if (days >= lastCheckDays && days - lastCheckDays < 1d / 24) return;
        lastCheckDays = days;
        RefreshSuccessions();
    }

    private void Handle_PlayerConnectionStateChanged(MessagePayload<PlayerConnectionStateChanged> obj)
    {
        if (ModInformation.IsServer) GameThread.EnqueueSafe(() => RefreshSuccessions());
    }

    private void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        presentationVersion++;
        IsAppointingClanLeader = false;
        showingWaitInquiry = false;
        sentSelections.Clear();
        lastCheckDays = -1;
    }

    private void RefreshSuccessions(Clan clan = null)
    {
        foreach (var player in playerManager.Players)
        {
            if (!objectManager.TryGetObject<Hero>(player.HeroId, out var hero) || !hero.IsDead ||
                (clan != null && hero.Clan != clan)) continue;
            RefreshSuccession(player, hero);
        }
    }

    private void RefreshSuccession(Player player, Hero hero, bool selectionRejected = false)
    {
        heirSelectionCampaignBehaviorInterface.PrepareSuccession(hero);
        var state = heirSelectionCampaignBehaviorInterface.GetSuccession(hero);
        if (state == null) return;
        if (state.GameOver)
        {
            CompleteGameOver(player, hero, state);
            return;
        }

        var candidates = GetCandidates(hero, out var appoint, out var waiting);
        bool connected = playerManager.IsConnected(player);
        if (appoint && !connected)
        {
            state.AppointmentDeadlineDays ??= CampaignTime.YearsFromNow(1).ToDays;
            if (candidates.Count > 0 && CampaignTime.Now.ToDays >= state.AppointmentDeadlineDays.Value)
            {
                var bestScore = candidates.Values.Max();
                var tied = candidates.Where(pair => pair.Value == bestScore).Select(pair => pair.Key).ToArray();
                var leadership = tied.Max(candidate => candidate.GetSkillValue(DefaultSkills.Leadership));
                var successor = tied.Where(candidate => candidate.GetSkillValue(DefaultSkills.Leadership) == leadership)
                    .ToList().GetRandomElement();
                AppointClanLeader(player, hero, successor);
                return;
            }
        }

        if (!waiting && candidates.Count == 0)
        {
            CompleteGameOver(player, hero, state);
            return;
        }

        if (!connected || !playerManager.TryGetPeer(player.ControllerId, out var peer)) return;
        var ids = new Dictionary<string, int>();
        foreach (var candidate in candidates)
            if (objectManager.TryGetIdWithLogging(candidate.Key, out var id)) ids[id] = candidate.Value;

        if (!selectionRejected && sentSelections.TryGetValue(player.HeroId, out var sent) && sent is NetworkClientSelectHeir previous &&
            previous.AppointClanLeader == appoint && previous.WaitingForHeirSelection == waiting &&
            previous.HeirIdApparents.SequenceEqual(ids)) return;

        var message = new NetworkClientSelectHeir(ids, appoint, waiting, selectionRejected);
        sentSelections[player.HeroId] = message;
        sendCoalescer?.Flush(network);
        network.Send(peer, message);
    }

    private Dictionary<Hero, int> GetCandidates(Hero hero, out bool appoint, out bool waiting)
    {
        waiting = heirSelectionCampaignBehaviorInterface.IsWaitingForLeader(hero);
        var candidates = waiting ? new Dictionary<Hero, int>() : heirSelectionCampaignBehaviorInterface.GetHeirs(hero);
        appoint = !waiting && candidates.Count == 0 && hero.Clan?.Leader == hero;
        if (!appoint) return candidates;

        candidates = heirSelectionCampaignBehaviorInterface.GetPlayerSuccessors(hero);
        waiting = candidates.Count == 0 && hero.Clan.Heroes.Any(member => member != hero && playerManager.Contains(member) &&
            (member.IsDead || member.DeathMark != KillCharacterAction.KillCharacterActionDetail.None) &&
            heirSelectionCampaignBehaviorInterface.GetSuccession(member)?.GameOver != true &&
            heirSelectionCampaignBehaviorInterface.GetHeirs(member).Count > 0);
        return candidates;
    }

    private void AppointClanLeader(Player player, Hero hero, Hero successor)
    {
        if (!objectManager.TryGetIdWithLogging(successor, out var successorId)) return;
        applyHeirSelectionActionInterface.AppointClanLeader(hero, successor);
        var state = heirSelectionCampaignBehaviorInterface.GetSuccession(hero);
        state.AppointedLeaderId = successorId;
        CompleteGameOver(player, hero, state);
        GameThread.EnqueueSafe(() => RefreshSuccessions(successor.Clan));
    }

    private void CompleteGameOver(Player player, Hero hero, PlayerSuccessionData state)
    {
        state.GameOver = true;
        if (!playerManager.IsConnected(player))
        {
            playerDeletionHandler.CompleteGameOver(player);
            sentSelections.Remove(player.HeroId);
            return;
        }

        if (sentSelections.TryGetValue(player.HeroId, out var sent) && sent is NetworkClientGameOver) return;
        if (!playerManager.TryGetPeer(player.ControllerId, out var peer)) return;
        bool clanSurvives = hero.Clan != null && (hero.Clan.Heroes.Any(member => member.IsAlive && playerManager.Contains(member)) ||
            hero.Clan.GetHeirApparents().Count > 0);
        var message = new NetworkClientGameOver(player.HeroId, state.AppointedLeaderId, clanSurvives);
        sentSelections[player.HeroId] = message;
        sendCoalescer?.Flush(network);
        network.Send(peer, message);
    }

    private void Handle_NetworkClientSelectHeir(MessagePayload<NetworkClientSelectHeir> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            var version = ++presentationVersion;
            cutscenesHandler.EnqueueDeathPresentation(() =>
            {
                if (version != presentationVersion || Hero.MainHero?.IsDead != true) return;
                CloseSelection();
                IsAppointingClanLeader = data.AppointClanLeader;

                if (data.WaitingForHeirSelection)
                {
                    showingWaitInquiry = true;
                    InformationManager.ShowInquiry(new InquiryData(
                        GameTexts.FindText("str_coop_succession_wait_title").ToString(),
                        GameTexts.FindText("str_coop_clan_experimental_warning") + "\n\n" +
                        GameTexts.FindText("str_coop_succession_wait_description"), true, false,
                        GameTexts.FindText("str_coop_succession_disconnect").ToString(), string.Empty,
                        () => messageBroker.Publish(this, new PlayerDisconnectRequested()), null));
                    return;
                }

                var heirApparents = new Dictionary<Hero, int>();
                foreach (var pair in data.HeirIdApparents)
                    if (objectManager.TryGetObjectWithLogging<Hero>(pair.Key, out var heir)) heirApparents[heir] = pair.Value;
                if (heirApparents.Count == 0) return;

                if (PlayerEncounter.Current != null && (PlayerEncounter.Battle == null || !PlayerEncounter.Battle.IsFinalized))
                    PlayerEncounter.Finish(true);
                CampaignEventDispatcher.Instance.OnHeirSelectionRequested(heirApparents);
                if (data.SelectionRejected)
                    InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("str_coop_succession_choice_unavailable").ToString()));
            });
        });
    }

    public void EndSelection()
    {
        presentationVersion++;
        CloseSelection();
    }

    private void CloseSelection()
    {
        MapScreen.Instance?.OnHeirSelectionOver(null);
        if (showingWaitInquiry) InformationManager.HideInquiry();
        showingWaitInquiry = false;
    }

    private void Handle_HeirSelectionOver(MessagePayload<HeirSelectionOver> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.OriginalHero, out var originalHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.SelectedHeir, out var selectedHeirId)) return;

        network.SendAll(new NetworkHeirSelectionOver(originalHeroId, selectedHeirId, IsAppointingClanLeader));
    }

    private void Handle_NetworkHeirSelectionOver(MessagePayload<NetworkHeirSelectionOver> obj)
    {
        if (obj.Who is not NetPeer peer) return;

        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.TryGetPlayer(peer, out var player) || player.HeroId != data.OriginalHeroId)
            {
                Logger.Warning($"Ignoring heir selection for hero {data.OriginalHeroId} from peer {peer.Id} because that peer no longer controls the hero");
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OriginalHeroId, out var originalHero)) return;
            if (!originalHero.IsDead ||
                originalHero.DeathMark == KillCharacterAction.KillCharacterActionDetail.None ||
                originalHero.Clan == null) return;

            var candidates = GetCandidates(originalHero, out var appoint, out var waiting);
            if (waiting || heirSelectionCampaignBehaviorInterface.GetSuccession(originalHero)?.GameOver == true ||
                data.AppointClanLeader != appoint ||
                !objectManager.TryGetObject<Hero>(data.SelectedHeirId, out var selectedHeir) || !candidates.ContainsKey(selectedHeir))
            {
                RefreshSuccession(player, originalHero, selectionRejected: true);
                return;
            }

            if (appoint)
            {
                AppointClanLeader(player, originalHero, selectedHeir);
                return;
            }
            // Death clears PartyBelongedTo, but the player's registered party is retained for succession.
            objectManager.TryGetObject(player.MobilePartyId, out MobileParty originalParty);
            applyHeirSelectionActionInterface.ApplyByDeath(originalHero, selectedHeir, originalParty);
            GameThread.EnqueueSafe(() => RefreshSuccessions(selectedHeir.Clan));
        });
    }

    private void Handle_ChangePlayerCharacterAfterHeirSelection(MessagePayload<ChangePlayerCharacterAfterHeirSelection> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.OriginalHero, out var originalHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Heir, out var heirId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Heir.Clan, out var clanId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Heir.CharacterObject, out var characterObjectId)) return;
        var registeredPlayer = playerManager.Players.FirstOrDefault(player => player.HeroId == originalHeroId);
        if (registeredPlayer == null)
        {
            Logger.Error($"Failed to get player for hero {originalHeroId} during heir selection");
            return;
        }

        objectManager.TryGetObject(registeredPlayer.MobilePartyId, out MobileParty originalParty);

        var replacementPlayerData = new Player(
            registeredPlayer.ControllerId,
            heirId,
            registeredPlayer.MobilePartyId,
            clanId,
            characterObjectId,
            registeredPlayer.OriginalClanId ?? registeredPlayer.ClanId);

        if (!playerPartyRestorer.TryRestore(replacementPlayerData, out var replacementPlayer))
        {
            Logger.Error($"Could not prepare heir {heirId} as the new player for controller {registeredPlayer.ControllerId}");
            return;
        }

        if (!playerManager.ReplacePlayer(registeredPlayer, replacementPlayer))
        {
            Logger.Error($"Could not replace player registration for controller {registeredPlayer.ControllerId} after heir selection");
            return;
        }

        // Migrate CoopSession data before changed player action calls PlayerHeroChanged
        coopSessionMigrator.MigratePlayerData(data.OriginalHero, data.Heir);
        sentSelections.Remove(originalHeroId);

        heirSelectionCampaignBehaviorInterface.OnBeforePlayerCharacterChanged(data.OriginalHero, originalParty);

        Logger.Information($"Transferred controller {registeredPlayer.ControllerId} from hero {originalHeroId} to heir {heirId}");

        messageBroker.Publish(this, new PlayerHeirSelectionCompleted(data.Heir));
        sendCoalescer?.Flush(network);
        network.SendAll(new NetworkChangePlayerCharacterAfterHeirSelection(replacementPlayer, originalHeroId));

        // Only disband/destroy party if the selected heir isn't in the same party as the dead/retired player
        if (originalParty != null && replacementPlayer.MobilePartyId != registeredPlayer.MobilePartyId)
        {
            if (originalParty.IsActive)
            {
                DisbandPartyAction.StartDisband(originalParty);
            }
            else
            {
                DestroyPartyAction.Apply(null, originalParty);
            }
        }
    }

    private void Handle_NetworkChangePlayerCharacterAfterHeirSelection(MessagePayload<NetworkChangePlayerCharacterAfterHeirSelection> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            var replacementPlayer = data.Player;
            if (replacementPlayer == null) return;

            if (!objectManager.TryGetObjectWithLogging<Hero>(replacementPlayer.HeroId, out var heir)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(replacementPlayer.MobilePartyId, out var heirParty)) return;

            if (!playerManager.TryGetPlayer(replacementPlayer.ControllerId, out var registeredPlayer))
            {
                Logger.Error($"Could not find player registration for controller {replacementPlayer.ControllerId} after heir selection");
                return;
            }

            if (registeredPlayer.HeroId == replacementPlayer.HeroId) return;
            if (registeredPlayer.HeroId != data.OriginalHeroId)
            {
                Logger.Warning($"Ignoring heir registration for {replacementPlayer.ControllerId} because it expected hero {data.OriginalHeroId} but found {registeredPlayer.HeroId}");
                return;
            }

            if (!playerManager.ReplacePlayer(registeredPlayer, replacementPlayer))
            {
                Logger.Error("Could not replace player registration for controller {ControllerId} after heir selection", replacementPlayer.ControllerId);
                return;
            }

            if (!heir.IsControlledByThisInstance()) return;

            var isPrisoner = heir.PartyBelongedToAsPrisoner != null;
            if (isPrisoner)
            {
                using (new AllowedThread())
                {
                    heir.PartyBelongedTo = heirParty;
                }
            }

            try
            {
                ChangePlayerCharacterAction.Apply(heir);
            }
            finally
            {
                if (isPrisoner)
                {
                    using (new AllowedThread())
                    {
                        heir.PartyBelongedTo = null;
                    }
                }
            }
        });
    }

    private void Handle_PlayerCharacterChangedAfterHeirSelection(MessagePayload<PlayerCharacterChangedAfterHeirSelection> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.OldPlayer, out var oldPlayerId)) return;
        if (!objectManager.TryGetIdWithLogging(data.NewPlayer, out var newPlayerId)) return;
        if (!objectManager.TryGetIdWithLogging(data.NewMainParty, out var newMainPartyId)) return;

        network.SendAll(new NetworkPlayerCharacterChangedAfterHeirSelection(oldPlayerId, newPlayerId, newMainPartyId, data.IsMainPartyChanged));
    }

    private void Handle_NetworkPlayerCharacterChangedAfterHeirSelection(MessagePayload<NetworkPlayerCharacterChangedAfterHeirSelection> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OldPlayerId, out var oldPlayerHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.NewPlayerId, out var newPlayerHero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.NewMainPartyId, out var newMainParty)) return;

            heirSelectionCampaignBehaviorInterface.OnPlayerCharacterChanged(oldPlayerHero, newPlayerHero, newMainParty, data.IsMainPartyChanged);
        });
    }
}
