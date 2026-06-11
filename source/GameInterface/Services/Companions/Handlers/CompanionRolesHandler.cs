using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Companions.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Companions.Handlers;

internal class CompanionRolesHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<CompanionRolesHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public CompanionRolesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<ClanNameSelectionDone>(Handle_ClanNameSelectionDone);
        messageBroker.Subscribe<DoClanNameSelection>(Handle_DoClanNameSelection);
        messageBroker.Subscribe<CompanionFired>(Handle_CompanionFired);
        messageBroker.Subscribe<FireCompanion>(Handle_FireCompanion);
        messageBroker.Subscribe<CompanionRejoinAfterEmprisonment>(Handle_CompanionRejoinAfterEmprisonment);
        messageBroker.Subscribe<DoCompanionRejoinAfterEmprisonment>(Handle_DoCompanionRejoinAfterEmprisonment);
        messageBroker.Subscribe<CompanionJoinedPartyByRescue>(Handle_CompanionJoinedPartyByRescue);
        messageBroker.Subscribe<DoCompanionJoinedPartyByRescue>(Handle_DoCompanionJoinedPartyByRescue);
        messageBroker.Subscribe<PartyScreenClosedFromRescuing>(Handle_PartyScreenClosedFromRescuing);
        messageBroker.Subscribe<DoPartyScreenClosedFromRescuing>(Handle_DoPartyScreenClosedFromRescuing);
        messageBroker.Subscribe<CompanionRescued>(Handle_CompanionRescued);
        messageBroker.Subscribe<RescueCompanion>(Handle_RescueCompanion);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClanNameSelectionDone>(Handle_ClanNameSelectionDone);
        messageBroker.Unsubscribe<DoClanNameSelection>(Handle_DoClanNameSelection);
        messageBroker.Unsubscribe<CompanionFired>(Handle_CompanionFired);
        messageBroker.Unsubscribe<FireCompanion>(Handle_FireCompanion);
        messageBroker.Unsubscribe<CompanionRejoinAfterEmprisonment>(Handle_CompanionRejoinAfterEmprisonment);
        messageBroker.Unsubscribe<DoCompanionRejoinAfterEmprisonment>(Handle_DoCompanionRejoinAfterEmprisonment);
        messageBroker.Unsubscribe<CompanionJoinedPartyByRescue>(Handle_CompanionJoinedPartyByRescue);
        messageBroker.Unsubscribe<DoCompanionJoinedPartyByRescue>(Handle_DoCompanionJoinedPartyByRescue);
        messageBroker.Unsubscribe<PartyScreenClosedFromRescuing>(Handle_PartyScreenClosedFromRescuing);
        messageBroker.Unsubscribe<DoPartyScreenClosedFromRescuing>(Handle_DoPartyScreenClosedFromRescuing);
        messageBroker.Unsubscribe<CompanionRescued>(Handle_CompanionRescued);
        messageBroker.Unsubscribe<RescueCompanion>(Handle_RescueCompanion);
    }

    private void Handle_ClanNameSelectionDone(MessagePayload<ClanNameSelectionDone> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.OneToOneConversationHero, out var oneToOneConversationHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.SelectedFief, out var selectedFiefId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;

        var message = new DoClanNameSelection(
            mainHeroId,
            oneToOneConversationHeroId,
            selectedFiefId,
            mainPartyId,
            obj.What.ClanName
        );

        network.SendAll(message);
    }

    private void Handle_DoClanNameSelection(MessagePayload<DoClanNameSelection> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.OneToOneConversationHeroId, out var oneToOneConversationHero)) return;
        if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.SelectedFiefId, out var selectedFief)) return;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;

        var companionRolesCampaignBehavior = Campaign.Current.GetCampaignBehavior<CompanionRolesCampaignBehavior>();

        RemoveCompanionAction.ApplyByByTurningToLord(mainHero.Clan, oneToOneConversationHero);
        oneToOneConversationHero.SetNewOccupation(Occupation.Lord);
        TextObject textObject = GameTexts.FindText("str_generic_clan_name", null);
        textObject.SetTextVariable("CLAN_NAME", new TextObject(obj.What.ClanName, null));
        int randomBannerIdForNewClan = companionRolesCampaignBehavior.GetRandomBannerIdForNewClan();
        Clan clan = Clan.CreateCompanionToLordClan(oneToOneConversationHero, selectedFief, textObject, randomBannerIdForNewClan);
        if (oneToOneConversationHero.PartyBelongedTo == mainParty)
        {
            mainParty.MemberRoster.AddToCounts(oneToOneConversationHero.CharacterObject, -1, false, 0, 0, true, -1);
        }
        MobileParty partyBelongedTo = oneToOneConversationHero.PartyBelongedTo;
        if (partyBelongedTo == null)
        {
            MobileParty mobileParty = LordPartyComponent.CreateLordParty(oneToOneConversationHero.CharacterObject.StringId, oneToOneConversationHero, mainParty.Position, 3f, selectedFief, oneToOneConversationHero);
            mobileParty.MemberRoster.AddToCounts(clan.Culture.BasicTroop, MBRandom.RandomInt(12, 15), false, 0, 0, true, -1);
            mobileParty.MemberRoster.AddToCounts(clan.Culture.EliteBasicTroop, MBRandom.RandomInt(10, 15), false, 0, 0, true, -1);
        }
        else
        {
            partyBelongedTo.ActualClan = clan;
            partyBelongedTo.Party.SetVisualAsDirty();
        }
        companionRolesCampaignBehavior.AdjustCompanionsEquipment(oneToOneConversationHero);
        companionRolesCampaignBehavior.SpawnNewHeroesForNewCompanionClan(oneToOneConversationHero, clan, selectedFief); // Gives relation with Hero.MainHero, need to expand this to work for mainHero
        GiveGoldAction.ApplyBetweenCharacters(mainHero, oneToOneConversationHero, 20000, false);
        GainKingdomInfluenceAction.ApplyForDefault(mainHero, -500f);
        ChangeRelationAction.ApplyPlayerRelation(oneToOneConversationHero, 50, true, true);
    }

    private void Handle_CompanionFired(MessagePayload<CompanionFired> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.OneToOneConversationHero, out var oneToOneConversationHeroId)) return;

        var message = new FireCompanion(oneToOneConversationHeroId);

        network.SendAll(message);
    }

    private void Handle_FireCompanion(MessagePayload<FireCompanion> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.OneToOneConversationHeroId, out var oneToOneConversationHero)) return;

        RemoveCompanionAction.ApplyByFire(oneToOneConversationHero.CompanionOf, oneToOneConversationHero);
        KillCharacterAction.ApplyByRemove(oneToOneConversationHero, false, true);
    }

    private void Handle_CompanionRejoinAfterEmprisonment(MessagePayload<CompanionRejoinAfterEmprisonment> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.OneToOneConversationHero, out var oneToOneConversationHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;

        var message = new DoCompanionRejoinAfterEmprisonment(
            oneToOneConversationHeroId,
            mainPartyId
        );

        network.SendAll(message);
    }

    private void Handle_DoCompanionRejoinAfterEmprisonment(MessagePayload<DoCompanionRejoinAfterEmprisonment> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.OneToOneConversationHeroId, out var oneToOneConversationHero)) return;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;

        AddHeroToPartyAction.Apply(oneToOneConversationHero, mainParty, true);
    }

    private void Handle_CompanionJoinedPartyByRescue(MessagePayload<CompanionJoinedPartyByRescue> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.OneToOneConversationHero, out var oneToOneConversationHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;

        var message = new DoCompanionJoinedPartyByRescue(
            oneToOneConversationHeroId,
            mainPartyId
        );

        network.SendAll(message);
    }

    private void Handle_DoCompanionJoinedPartyByRescue(MessagePayload<DoCompanionJoinedPartyByRescue> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.OneToOneConversationHeroId, out var oneToOneConversationHero)) return;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;

        EndCaptivityAction.ApplyByReleasedAfterBattle(oneToOneConversationHero);
        oneToOneConversationHero.ChangeState(Hero.CharacterStates.Active);
        mainParty.AddElementToMemberRoster(oneToOneConversationHero.CharacterObject, 1, false);
    }

    private void Handle_PartyScreenClosedFromRescuing(MessagePayload<PartyScreenClosedFromRescuing> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.LeftOwnerParty, out var leftOwnerPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.LeftMemberRoster, out var leftMemberRosterId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.LeftPrisonRoster, out var leftPrisonRosterId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.RightOwnerParty, out var rightOwnerPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.RightMemberRoster, out var rightMemberRosterId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.RightPrisonRoster, out var rightPrisonRosterId)) return;

        var message = new DoPartyScreenClosedFromRescuing(
            leftOwnerPartyId,
            leftMemberRosterId,
            leftPrisonRosterId,
            rightOwnerPartyId,
            rightMemberRosterId,
            rightPrisonRosterId
        );

        network.SendAll(message);
    }

    private void Handle_DoPartyScreenClosedFromRescuing(MessagePayload<DoPartyScreenClosedFromRescuing> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.LeftOwnerPartyId, out var leftOwnerParty)) return;
        if (!objectManager.TryGetObjectWithLogging<TroopRoster>(obj.What.LeftMemberRosterId, out var leftMemberRoster)) return;
        if (!objectManager.TryGetObjectWithLogging<TroopRoster>(obj.What.LeftPrisonRosterId, out var leftPrisonRoster)) return;
        if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.RightOwnerPartyId, out var rightOwnerParty)) return;
        if (!objectManager.TryGetObjectWithLogging<TroopRoster>(obj.What.RightMemberRosterId, out var rightMemberRoster)) return;
        if (!objectManager.TryGetObjectWithLogging<TroopRoster>(obj.What.RightPrisonRosterId, out var rightPrisonRoster)) return;

        var companionRolesCampaignBehavior = Campaign.Current.GetCampaignBehavior<CompanionRolesCampaignBehavior>();
        companionRolesCampaignBehavior.PartyScreenClosed(
            leftOwnerParty,
            leftMemberRoster,
            leftPrisonRoster,
            rightOwnerParty,
            rightMemberRoster,
            rightPrisonRoster,
            false
        );
    }

    private void Handle_CompanionRescued(MessagePayload<CompanionRescued> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.OneToOneConversationHero, out var oneToOneConversationHeroId)) return;

        var message = new RescueCompanion(oneToOneConversationHeroId);

        network.SendAll(message);
    }

    private void Handle_RescueCompanion(MessagePayload<RescueCompanion> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.OneToOneConversationHeroId, out var oneToOneConversationHero)) return;

        EndCaptivityAction.ApplyByReleasedAfterBattle(oneToOneConversationHero);
    }
}
