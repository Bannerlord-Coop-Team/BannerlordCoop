using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Bandits.Messages;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Bandits.Handlers;

internal class BanditInteractionsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BanditInteractionsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface;

    public BanditInteractionsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionInteractionsPlayerDataInterface = sessionInteractionsPlayerDataInterface;

        messageBroker.Subscribe<SetPlayerBanditInteraction>(Handle_SetPlayerBanditInteraction);
        messageBroker.Subscribe<NetworkSetPlayerBanditInteraction>(Handle_NetworkSetPlayerBanditInteraction);

        messageBroker.Subscribe<MobilePartyDestroyed>(Handle_MobilePartyDestroyed);
        messageBroker.Subscribe<NetworkBanditPartyDestroyed>(Handle_NetworkBanditPartyDestroyed);

        messageBroker.Subscribe<BanditPartyScreenDoneCondition>(Handle_BanditPartyScreenDoneCondition);
        messageBroker.Subscribe<NetworkBanditPartyScreenDoneCondition>(Handle_NetworkBanditPartyScreenDoneCondition);

        messageBroker.Subscribe<GetBanditMemberAndPrisonerRosters>(Handle_GetBanditMemberAndPrisonerRosters);
        messageBroker.Subscribe<NetworkGetBanditMemberAndPrisonerRosters>(Handle_NetworkGetBanditMemberAndPrisonerRosters);

        messageBroker.Subscribe<RosterScreenAfterBanditEncounter>(Handle_RosterScreenAfterBanditEncounter);
        messageBroker.Subscribe<NetworkRosterScreenAfterBanditEncounter>(Handle_NetworkRosterScreenAfterBanditEncounter);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SetPlayerBanditInteraction>(Handle_SetPlayerBanditInteraction);
        messageBroker.Unsubscribe<NetworkSetPlayerBanditInteraction>(Handle_NetworkSetPlayerBanditInteraction);

        messageBroker.Unsubscribe<MobilePartyDestroyed>(Handle_MobilePartyDestroyed);
        messageBroker.Unsubscribe<NetworkBanditPartyDestroyed>(Handle_NetworkBanditPartyDestroyed);

        messageBroker.Unsubscribe<BanditPartyScreenDoneCondition>(Handle_BanditPartyScreenDoneCondition);
        messageBroker.Unsubscribe<NetworkBanditPartyScreenDoneCondition>(Handle_NetworkBanditPartyScreenDoneCondition);

        messageBroker.Unsubscribe<GetBanditMemberAndPrisonerRosters>(Handle_GetBanditMemberAndPrisonerRosters);
        messageBroker.Unsubscribe<NetworkGetBanditMemberAndPrisonerRosters>(Handle_NetworkGetBanditMemberAndPrisonerRosters);

        messageBroker.Unsubscribe<RosterScreenAfterBanditEncounter>(Handle_RosterScreenAfterBanditEncounter);
        messageBroker.Unsubscribe<NetworkRosterScreenAfterBanditEncounter>(Handle_NetworkRosterScreenAfterBanditEncounter);
    }

    private void Handle_SetPlayerBanditInteraction(MessagePayload<SetPlayerBanditInteraction> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(data.ConversationParty, out var conversationPartyId)) return;

            var message = new NetworkSetPlayerBanditInteraction(mainHeroId, conversationPartyId, data.Interaction);
            network.SendAll(message);
        });  
    }

    private void Handle_NetworkSetPlayerBanditInteraction(MessagePayload<NetworkSetPlayerBanditInteraction> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            // Guard against saving ids that can't be resolved on the server
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var _)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.ConversationPartyId, out var _)) return;

            sessionInteractionsPlayerDataInterface.SetPlayerBanditsInteraction(data.MainHeroId, data.ConversationPartyId, data.Interaction);
        });
    }

    private void Handle_MobilePartyDestroyed(MessagePayload<MobilePartyDestroyed> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            // Don't process anything for destroyed mobile parties that aren't bandits
            if (!data.MobileParty.IsBandit) return;

            if (!objectManager.TryGetIdWithLogging(data.MobileParty, out var mobilePartyId)) return;

            // Update CoopSession data on server
            sessionInteractionsPlayerDataInterface.RemoveInteractedBanditsForAllPlayers(mobilePartyId);

            var message = new NetworkBanditPartyDestroyed(mobilePartyId);
            network.SendAll(message);
        }); 
    }

    private void Handle_NetworkBanditPartyDestroyed(MessagePayload<NetworkBanditPartyDestroyed> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetBanditInteractionsBehavior(out var banditInteractionsBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MobilePartyId, out var mobileParty)) return;

            if (banditInteractionsBehavior._interactedBandits.ContainsKey(mobileParty))
            {
                banditInteractionsBehavior._interactedBandits.Remove(mobileParty);
            }
        });
    }

    private void Handle_BanditPartyScreenDoneCondition(MessagePayload<BanditPartyScreenDoneCondition> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            var charactersIds = new List<string>();
            foreach (var troopRosterElement in data.RightMemberRoster.data)
            {
                if (!objectManager.TryGetIdWithLogging(troopRosterElement.Character, out var characterId)) continue;

                charactersIds.Add(characterId);
            }

            var message = new NetworkBanditPartyScreenDoneCondition(charactersIds);
            network.SendAll(message);
        });  
    }

    private void Handle_NetworkBanditPartyScreenDoneCondition(MessagePayload<NetworkBanditPartyScreenDoneCondition> obj)
    {
        GameThread.RunSafe(() =>
        {
            foreach (var characterId in obj.What.CharactersIds)
            {
                if (!objectManager.TryGetObjectWithLogging<CharacterObject>(characterId, out var character)) continue;

                if (character.IsHero && character.HeroObject.HeroState == Hero.CharacterStates.Fugitive)
                {
                    character.HeroObject.ChangeState(Hero.CharacterStates.Active);
                }
            }
        });
    }

    private void Handle_GetBanditMemberAndPrisonerRosters(MessagePayload<GetBanditMemberAndPrisonerRosters> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.PlayerClan, out var playerClanId)) return;
            if (!objectManager.TryGetIdWithLogging(data.MainParty, out var mainPartyId)) return;

            var partiesIds = new List<string>();
            foreach (var party in data.Parties)
            {
                if (!objectManager.TryGetIdWithLogging(party, out var partyId)) continue;

                partiesIds.Add(partyId);
            }

            var message = new NetworkGetBanditMemberAndPrisonerRosters(playerClanId, mainPartyId, partiesIds, data.DoBanditsJoinPlayerSide);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkGetBanditMemberAndPrisonerRosters(MessagePayload<NetworkGetBanditMemberAndPrisonerRosters> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.PlayerClanId, out var playerClan)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MainPartyId, out var mainParty)) return;

            foreach (var partyId in data.PartiesIds)
            {
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(partyId, out var mobileParty)) return;

                for (int j = mobileParty.PrisonRoster.Count - 1; j > -1; j--)
                {
                    CharacterObject characterAtIndex = mobileParty.PrisonRoster.GetCharacterAtIndex(j);
                    if (characterAtIndex.HeroObject.Clan == playerClan)
                    {
                        if (data.DoBanditsJoinPlayerSide)
                        {
                            EndCaptivityAction.ApplyByPeace(characterAtIndex.HeroObject, null);
                        }
                        else
                        {
                            EndCaptivityAction.ApplyByReleasedAfterBattle(characterAtIndex.HeroObject);
                        }
                        characterAtIndex.HeroObject.ChangeState(Hero.CharacterStates.Active);
                        AddHeroToPartyAction.Apply(characterAtIndex.HeroObject, mainParty, true);
                    }
                    else if (playerClan.IsAtWarWith(characterAtIndex.HeroObject.Clan))
                    {
                        TransferPrisonerAction.Apply(characterAtIndex, mobileParty.Party, mainParty.Party);
                    }
                }
            }
        });
    }

    private void Handle_RosterScreenAfterBanditEncounter(MessagePayload<RosterScreenAfterBanditEncounter> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.MainParty, out var mainPartyId)) return;

            var partiesIds = new List<string>();
            foreach (var party in data.Parties)
            {
                if (!objectManager.TryGetIdWithLogging(party, out var partyId)) continue;

                partiesIds.Add(partyId);
            }

            var message = new NetworkRosterScreenAfterBanditEncounter(partiesIds, mainPartyId);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkRosterScreenAfterBanditEncounter(MessagePayload<NetworkRosterScreenAfterBanditEncounter> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MainPartyId, out var mainParty)) return;

            foreach (var partyId in data.PartiesIds)
            {
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(partyId, out var mobileParty)) return;

                CampaignEventDispatcher.Instance.OnBanditPartyRecruited(mobileParty);
                DestroyPartyAction.Apply(mainParty.Party, mobileParty);
            }
        });
    }

    private bool TryGetBanditInteractionsBehavior(out BanditInteractionsCampaignBehavior banditInteractionsBehavior)
    {
        banditInteractionsBehavior = Campaign.Current?.GetCampaignBehavior<BanditInteractionsCampaignBehavior>();
        if (banditInteractionsBehavior != null) return true;

        Logger.Debug("Skipping bandit interactions update because the campaign behavior is unavailable");
        return false;
    }
}
