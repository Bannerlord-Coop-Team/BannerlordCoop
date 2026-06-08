using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using Helpers;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Localization;

namespace GameInterface.Services.PlayerCaptivityService.Handlers;

internal class PlayerCaptivityHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerCaptivityHandler>();
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerRegistry playerRegistry;

    public PlayerCaptivityHandler(
        IObjectManager objectManager,
        INetwork network,
        IMessageBroker messageBroker,
        IPlayerRegistry playerRegistry)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.messageBroker = messageBroker;
        this.playerRegistry = playerRegistry;

        messageBroker.Subscribe<PrisonerTaken>(Handle_PrisonerTaken);
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);

        messageBroker.Subscribe<PlayerSurrendered>(Handle_PlayerSurrendered);
        messageBroker.Subscribe<NetworkPlayerSurrendered>(Handle_NetworkPlayerSurrendered);

        messageBroker.Subscribe<PlayerCaptivityChanged>(Handle_PlayerCaptivityChanged);

        messageBroker.Subscribe<EndPlayerCaptivityAttempted>(Handle_PlayerCaptivityEnded);
        messageBroker.Subscribe<NetworkEndPlayerCaptivityAttempted>(Handle_NetworkEndPlayerCaptivityAttempted);
        messageBroker.Subscribe<NetworkPlayerCaptivityEnded>(Handle_NetworkPlayerCaptivityEnded);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PrisonerTaken>(Handle_PrisonerTaken);
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);

        messageBroker.Unsubscribe<PlayerSurrendered>(Handle_PlayerSurrendered);
        messageBroker.Unsubscribe<NetworkPlayerSurrendered>(Handle_NetworkPlayerSurrendered);

        messageBroker.Unsubscribe<PlayerCaptivityChanged>(Handle_PlayerCaptivityChanged);

        messageBroker.Unsubscribe<EndPlayerCaptivityAttempted>(Handle_PlayerCaptivityEnded);
        messageBroker.Unsubscribe<NetworkEndPlayerCaptivityAttempted>(Handle_NetworkEndPlayerCaptivityAttempted);
        messageBroker.Unsubscribe<NetworkPlayerCaptivityEnded>(Handle_NetworkPlayerCaptivityEnded);
    }

    private void Handle_PrisonerTaken(MessagePayload<PrisonerTaken> payload)
    {
        var obj = payload.What;

        var hero = obj.PrisonerHero;
        var mobileParty = hero.PartyBelongedTo;

        if (mobileParty?.IsPlayer() == false)
            return;

        if (hero.PartyBelongedToAsPrisoner != null)
            return;

        hero.PartyBelongedToAsPrisoner = payload.What.CapturerParty;
        hero.PartyBelongedTo = null;

        mobileParty.MemberRoster.RemoveTroop(hero.CharacterObject);
        mobileParty.MemberRoster.Clear();
        mobileParty.PrisonRoster.Clear();
        
        payload.What.CapturerParty.PrisonRoster.AddToCounts(hero.CharacterObject, 1);

        mobileParty.IsActive = false;
        mobileParty.ChangePartyLeader(null);
    }

    private void Handle_PlayerSurrendered(MessagePayload<PlayerSurrendered> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out string mapEventId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.PlayerParty, out string playerParty)) return;

        var message = new NetworkPlayerSurrendered(playerParty, mapEventId);

        network.SendAll(message);
    }

    private void Handle_NetworkPlayerSurrendered(MessagePayload<NetworkPlayerSurrendered> payload)
    {
        if (!objectManager.TryGetObjectWithLogging(payload.What.MapEventId, out MapEvent mapEvent)) return;
        if (!objectManager.TryGetObjectWithLogging(payload.What.PlayerParty, out MobileParty playerParty)) return;

        try
        {
            mapEvent.DoSurrender(playerParty.Party.Side);
            mapEvent.FinalizeEvent();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to surrender");
        }
    }

    /// <summary>
    /// This will run every time player captivity changes
    /// </summary>
    /// <param name="payload"></param>
    private void Handle_PlayerCaptivityChanged(MessagePayload<PlayerCaptivityChanged> payload)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            if (Campaign.Current is null) return;

            using (new AllowedThread())
            {
                if (PlayerEncounter.Current != null)
                {
                    PlayerEncounter.LeaveEncounter = true;
                }
            }

            var playerCaptivity = Campaign.Current.PlayerCaptivity;
            var captorParty = payload.What.CaptorParty;
            if (captorParty == null)
            {
                PartyBase.MainParty.UpdateVisibilityAndInspected(MobileParty.MainParty.Position, 0f);
                PartyBase.MainParty.SetAsCameraFollowParty();

                playerCaptivity._captorParty = null;
                return;
            }

            playerCaptivity._captivityStartTime = CampaignTime.Now;
            playerCaptivity._lastCheckTime = CampaignTime.Now;
            PlayerCaptivity.RandomNumber = MBRandom.RandomFloat;

            PartyBase.MainParty.UpdateVisibilityAndInspected(MobileParty.MainParty.Position, 0f);
            playerCaptivity._captorParty = payload.What.CaptorParty;
            playerCaptivity._captorParty.SetAsCameraFollowParty();
            playerCaptivity._captorParty.UpdateVisibilityAndInspected(MobileParty.MainParty.Position, 0f);
        });
    }

    // Client
    private void Handle_PlayerCaptivityEnded(MessagePayload<EndPlayerCaptivityAttempted> payload)
    {
        var data = payload.What;

        if (!objectManager.TryGetIdWithLogging(data.PlayerHero, out string heroId)) return;

        var playerParty = MobileParty.MainParty;
        if (!objectManager.TryGetIdWithLogging(playerParty, out string partyId)) return;

        string facilitatorId = null;
        if (data.Facilitator != null && !objectManager.TryGetIdWithLogging(data.Facilitator, out facilitatorId)) return;

        var message = new NetworkEndPlayerCaptivityAttempted(heroId, partyId, playerParty.Position, data.Detail, facilitatorId);
        network.SendAll(message);


        var playerCaptivity = Campaign.Current.PlayerCaptivity;

        playerCaptivity._captorParty = null;
        playerCaptivity.CountOfOffers = 0;
        playerCaptivity.CurrentRansomAmount = 0;
    }

    // Server
    private void Handle_NetworkEndPlayerCaptivityAttempted(MessagePayload<NetworkEndPlayerCaptivityAttempted> payload)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(payload.What.PlayerHeroId, out var playerHero))
            return;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(payload.What.PlayerPartyId, out var playerParty))
            return;

        Hero facilitator = null;
        if (payload.What.FacilitatorId != null && !objectManager.TryGetObjectWithLogging(payload.What.FacilitatorId, out facilitator))
            return;

        // PlayerCaptivity.EndCaptivityInternal
        EndPlayerCaptivityInternal(playerHero, playerParty, payload.What.Detail, facilitator);

        PartyBase partyBelongedToAsPrisoner = playerHero.PartyBelongedToAsPrisoner;
        IFaction capturerFaction = (partyBelongedToAsPrisoner != null) ? partyBelongedToAsPrisoner.MapFaction : null;
        if (partyBelongedToAsPrisoner != null && partyBelongedToAsPrisoner.IsSettlement)
        {
            playerParty.DisembarkToPosition(partyBelongedToAsPrisoner.Settlement.GatePosition);
        }
        else if (partyBelongedToAsPrisoner != null && partyBelongedToAsPrisoner.IsMobile)
        {
            playerParty.IsCurrentlyAtSea = partyBelongedToAsPrisoner.MobileParty.IsCurrentlyAtSea;
        }
        if (facilitator != null && payload.What.Detail != EndCaptivityDetail.Death)
        {
            StringHelpers.SetCharacterProperties("FACILITATOR", facilitator.CharacterObject, null, false);
            StringHelpers.SetCharacterProperties("PRISONER", playerHero.CharacterObject, null, false);
            MBInformationManager.AddQuickInformation(new TextObject("{=xPuSASof}{FACILITATOR.NAME} paid a ransom and freed {PRISONER.NAME} from captivity."));
        }
        CampaignEventDispatcher.Instance.OnHeroPrisonerReleased(playerHero, partyBelongedToAsPrisoner, capturerFaction, payload.What.Detail, true);

        playerParty.IsActive = true;
        playerParty.Position = payload.What.PlayerPartyPosition;
        playerParty.IgnoreForHours(4);

        var message = new NetworkPlayerCaptivityEnded();
        network.Send(payload.Who as NetPeer, message);
    }

    private void Handle_NetworkPlayerCaptivityEnded(MessagePayload<NetworkPlayerCaptivityEnded> payload)
    {
        
        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.LeaveSettlement();
        }

        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.Finish(true);
        }
        else if (Campaign.Current.CurrentMenuContext != null)
        {
            GameMenu.ExitToLast();
        }
    }

    private void EndPlayerCaptivityInternal(Hero playerHero, MobileParty playerParty, EndCaptivityDetail detail, Hero facilitator)
    {
        var partyBelongedToAsPrisoner = playerHero.PartyBelongedToAsPrisoner;

        if (playerHero.IsAlive)
        {
            playerHero.ChangeState(Hero.CharacterStates.Active);
            playerParty.AddElementToMemberRoster(playerHero.CharacterObject, 1, true);
            playerParty.ChangePartyLeader(playerHero);
        }
        if (playerHero.CurrentSettlement != null)
        {
            if (playerHero.IsAlive)
            {
                LeaveSettlementAction.ApplyForParty(playerParty);
            }
            else
            {
                LeaveSettlementAction.ApplyForCharacterOnly(playerHero);
            }
        }
        if (partyBelongedToAsPrisoner?.IsActive == true)
        {
            partyBelongedToAsPrisoner.PrisonRoster.RemoveTroop(playerHero.CharacterObject);
            if (partyBelongedToAsPrisoner.IsMobile && !partyBelongedToAsPrisoner.MobileParty.IsCurrentlyAtSea)
            {
                playerParty.TeleportPartyToOutSideOfEncounterRadius();
            }
        }
        if (playerHero.IsAlive)
        {
            playerParty.IsActive = true;
            playerParty.Party.SetAsCameraFollowParty();
            playerParty.SetMoveModeHold();

            // TODO
            SkillLevelingManager.OnMainHeroReleasedFromCaptivity(PlayerCaptivity.CaptivityStartTime.ElapsedHoursUntilNow);
            if (!playerParty.IsCurrentlyAtSea)
            {
                playerParty.Party.UpdateVisibilityAndInspected(playerParty.Position);
            }
        }
    }

    // Keep player party by captor
    // Re-implements <see cref="PlayerCaptivity.Update"> for multiplayer
    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        foreach (var (hero, mobileParty) in PlayerHeros())
        {
            if (!hero.IsPrisoner) continue;

            var captorParty = hero.PartyBelongedToAsPrisoner;

            if (captorParty == null) continue;

            mobileParty.Position = hero.PartyBelongedToAsPrisoner.Position;
        }
    }

    private IEnumerable<(Hero, MobileParty)> PlayerHeros()
    {
        foreach (var player in playerRegistry)
        {
            if (objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) &&
                objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var mobileParty))
            {
                yield return (hero, mobileParty);
            }
        }
    }
}
