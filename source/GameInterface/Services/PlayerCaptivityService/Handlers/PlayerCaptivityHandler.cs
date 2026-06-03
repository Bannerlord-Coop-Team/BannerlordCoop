using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
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

    public PlayerCaptivityHandler(IObjectManager objectManager, INetwork network, IMessageBroker messageBroker)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<PlayerSurrendered>(Handle_PlayerSurrendered);
        messageBroker.Subscribe<NetworkPlayerSurrendered>(Handle_NetworkPlayerSurrendered);


        messageBroker.Subscribe<EndPlayerCaptivityAttempted>(Handle_PlayerCaptivityEnded);
        messageBroker.Subscribe<NetworkEndPlayerCaptivityAttempted>(Handle_NetworkEndPlayerCaptivityAttempted);
        messageBroker.Subscribe<NetworkPlayerCaptivityEnded>(Handle_NetworkPlayerCaptivityEnded);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerSurrendered>(Handle_PlayerSurrendered);
        messageBroker.Unsubscribe<NetworkPlayerSurrendered>(Handle_NetworkPlayerSurrendered);
    }

    private void Handle_PlayerSurrendered(MessagePayload<PlayerSurrendered> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out string mapEventId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.MobileParty, out string mobilePartyId)) return;

        var message = new NetworkPlayerSurrendered(mobilePartyId, mapEventId);

        network.SendAll(message);

        using (new AllowedThread())
        {
            var playerCaptivity = Campaign.Current.PlayerCaptivity;
            playerCaptivity._captivityStartTime = CampaignTime.Now;
            playerCaptivity._lastCheckTime = CampaignTime.Now;

            PlayerCaptivity.RandomNumber = MBRandom.RandomFloat;
            if (PlayerEncounter.Current != null)
            {
                PlayerEncounter.LeaveEncounter = true;
            }
            MobileParty.MainParty.IsActive = false;
            PartyBase.MainParty.UpdateVisibilityAndInspected(MobileParty.MainParty.Position, 0f);
            playerCaptivity._captorParty = payload.What.MobileParty.Party;
            playerCaptivity._captorParty.SetAsCameraFollowParty();
            playerCaptivity._captorParty.UpdateVisibilityAndInspected(MobileParty.MainParty.Position, 0f);
            Hero.MainHero.PartyBelongedToAsPrisoner = payload.What.MobileParty.Party;
        }
    }

    private void Handle_NetworkPlayerSurrendered(MessagePayload<NetworkPlayerSurrendered> payload)
    {
        if (!objectManager.TryGetObjectWithLogging(payload.What.MapEventId, out MapEvent mapEvent)) return;
        if (!objectManager.TryGetObjectWithLogging(payload.What.MobilePartyId, out MobileParty mobileParty)) return;

        mapEvent.DoSurrender(mobileParty.Party.Side);
        mapEvent.FinalizeEvent();
    }

    // Client
    private void Handle_PlayerCaptivityEnded(MessagePayload<EndPlayerCaptivityAttempted> payload)
    {
        var data = payload.What;

        if (!objectManager.TryGetIdWithLogging(data.PlayerHero, out string heroId)) return;
        if (!objectManager.TryGetIdWithLogging(MobileParty.MainParty, out string partyId)) return;

        string facilitatorId = null;
        if (data.Facilitator != null && !objectManager.TryGetIdWithLogging(data.Facilitator, out facilitatorId)) return;

        var message = new NetworkEndPlayerCaptivityAttempted(heroId, partyId, data.Detail, facilitatorId);
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
        if (!objectManager.TryGetObjectWithLogging<Hero>(payload.What.PlayerPartyId, out var playerParty))
            return;

        Hero facilitator = null;
        if (payload.What.FacilitatorId != null && !objectManager.TryGetObjectWithLogging(payload.What.FacilitatorId, out facilitator))
            return;

        // PlayerCaptivity.EndCaptivityInternal
        EndPlayerCaptivityInternal(playerHero, payload.What.Detail, facilitator);

        PartyBase partyBelongedToAsPrisoner = playerHero.PartyBelongedToAsPrisoner;
        IFaction capturerFaction = (partyBelongedToAsPrisoner != null) ? partyBelongedToAsPrisoner.MapFaction : null;
        if (partyBelongedToAsPrisoner != null && partyBelongedToAsPrisoner.IsSettlement)
        {
            playerHero.PartyBelongedTo.DisembarkToPosition(partyBelongedToAsPrisoner.Settlement.GatePosition);
        }
        else if (partyBelongedToAsPrisoner != null && partyBelongedToAsPrisoner.IsMobile)
        {
            playerHero.PartyBelongedTo.IsCurrentlyAtSea = partyBelongedToAsPrisoner.MobileParty.IsCurrentlyAtSea;
        }
        if (facilitator != null && payload.What.Detail != EndCaptivityDetail.Death)
        {
            StringHelpers.SetCharacterProperties("FACILITATOR", facilitator.CharacterObject, null, false);
            StringHelpers.SetCharacterProperties("PRISONER", playerHero.CharacterObject, null, false);
            MBInformationManager.AddQuickInformation(new TextObject("{=xPuSASof}{FACILITATOR.NAME} paid a ransom and freed {PRISONER.NAME} from captivity."));
        }
        CampaignEventDispatcher.Instance.OnHeroPrisonerReleased(playerHero, partyBelongedToAsPrisoner, capturerFaction, payload.What.Detail, true);


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

    private void EndPlayerCaptivityInternal(Hero playerHero, EndCaptivityDetail detail, Hero facilitator)
    {
        var partyBelongedToAsPrisoner = playerHero.PartyBelongedToAsPrisoner;
        var playerParty = playerHero.PartyBelongedTo;

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
        if (partyBelongedToAsPrisoner.IsActive == true)
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
}
