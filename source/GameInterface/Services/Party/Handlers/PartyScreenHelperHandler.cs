using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.TroopRosters.Interfaces;
using Helpers;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Party.Handlers;

internal class PartyScreenHelperHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<PartyScreenHelperHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;

    public PartyScreenHelperHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITroopRosterInterface troopRosterInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;

        messageBroker.Subscribe<NewClanPartyScreenClosed>(Handle_NewClanPartyScreenClosed);
        messageBroker.Subscribe<CreateClanPartyAfterScreenClose>(Handle_CreateClanPartyAfterScreenClose);
        messageBroker.Subscribe<GarrisonDonated>(Handle_GarrisonDonated);
        messageBroker.Subscribe<DonateToGarrison>(Handle_DonateToGarrison);
        messageBroker.Subscribe<PrisonersDonated>(Handle_PrisonersDonated);
        messageBroker.Subscribe<DonatePrisoners>(Handle_DonatePrisoners);
        messageBroker.Subscribe<GarrisonManaged>(Handle_GarrisonManaged);
        messageBroker.Subscribe<DoManageGarrison>(Handle_DoManageGarrison);
        messageBroker.Subscribe<PrisonersReleasedAndTaken>(Handle_PrisonersReleasedAndTaken);
        messageBroker.Subscribe<ReleaseAndTakePrisoners>(Handle_ReleaseAndTakePrisoners);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NewClanPartyScreenClosed>(Handle_NewClanPartyScreenClosed);
        messageBroker.Unsubscribe<CreateClanPartyAfterScreenClose>(Handle_CreateClanPartyAfterScreenClose);
        messageBroker.Unsubscribe<GarrisonDonated>(Handle_GarrisonDonated);
        messageBroker.Unsubscribe<DonateToGarrison>(Handle_DonateToGarrison);
        messageBroker.Unsubscribe<PrisonersDonated>(Handle_PrisonersDonated);
        messageBroker.Unsubscribe<DonatePrisoners>(Handle_DonatePrisoners);
        messageBroker.Unsubscribe<GarrisonManaged>(Handle_GarrisonManaged);
        messageBroker.Unsubscribe<DoManageGarrison>(Handle_DoManageGarrison);
        messageBroker.Unsubscribe<PrisonersReleasedAndTaken>(Handle_PrisonersReleasedAndTaken);
        messageBroker.Unsubscribe<ReleaseAndTakePrisoners>(Handle_ReleaseAndTakePrisoners);
    }

    private void Handle_NewClanPartyScreenClosed(MessagePayload<NewClanPartyScreenClosed> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.NewLeaderHero, out var newLeaderHeroId)) return;

        var leftMemberRosterData = troopRosterInterface.PackTroopRosterData(obj.What.LeftMemberRoster);
        var leftPrisonRosterData = troopRosterInterface.PackTroopRosterData(obj.What.LeftPrisonRoster);

        var message = new CreateClanPartyAfterScreenClose(
            mainHeroId,
            newLeaderHeroId,
            leftMemberRosterData,
            leftPrisonRosterData
        );
        network.SendAll(message);
    }

    private void Handle_CreateClanPartyAfterScreenClose(MessagePayload<CreateClanPartyAfterScreenClose> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.NewLeaderHeroId, out var newLeaderHero)) return;

        int partyGoldLowerThreshold = Campaign.Current.Models.ClanFinanceModel.PartyGoldLowerThreshold;
        if (newLeaderHero.Gold < partyGoldLowerThreshold)
        {
            GiveGoldAction.ApplyBetweenCharacters(mainHero, newLeaderHero, partyGoldLowerThreshold - newLeaderHero.Gold, false);
        }
        MobileParty mobileParty = MobilePartyHelper.CreateNewClanMobileParty(newLeaderHero, newLeaderHero.Clan);
        foreach (var troopRosterElement in troopRosterInterface.UnpackTroopRosterData(obj.What.LeftMemberRosterData))
        {
            if (troopRosterElement.Character != newLeaderHero.CharacterObject)
            {
                mobileParty.MemberRoster.Add(troopRosterElement);
                //rightOwnerParty.MemberRoster.AddToCounts(troopRosterElement.Character, -troopRosterElement.Number, false, -troopRosterElement.WoundedNumber, -troopRosterElement.Xp, true, -1);
            }
        }
        foreach (var troopRosterElement in troopRosterInterface.UnpackTroopRosterData(obj.What.LeftPrisonRosterData))
        {
            mobileParty.PrisonRoster.Add(troopRosterElement);
            //rightOwnerParty.PrisonRoster.AddToCounts(troopRosterElement2.Character, -troopRosterElement2.Number, false, -troopRosterElement2.WoundedNumber, -troopRosterElement2.Xp, true, -1);
        }
    }

    private void Handle_GarrisonDonated(MessagePayload<GarrisonDonated> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.CurrentSettlement, out var currentSettlementId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.LeftMemberRoster, out var leftMemberRosterId)) return;

        var message = new DonateToGarrison(currentSettlementId, leftMemberRosterId);
        network.SendAll(message);
    }

    private void Handle_DonateToGarrison(MessagePayload<DonateToGarrison> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.CurrentSettlementId, out var currentSettlement)) return;
        if (!objectManager.TryGetObjectWithLogging<TroopRoster>(obj.What.LeftMemberRosterId, out var leftMemberRoster)) return;

        MobileParty garrisonParty = currentSettlement.Town.GarrisonParty;
        if (garrisonParty == null)
        {
            currentSettlement.AddGarrisonParty();
            garrisonParty = currentSettlement.Town.GarrisonParty;
        }
        for (int i = 0; i < leftMemberRoster.Count; i++)
        {
            TroopRosterElement elementCopyAtIndex = leftMemberRoster.GetElementCopyAtIndex(i);
            garrisonParty.AddElementToMemberRoster(elementCopyAtIndex.Character, elementCopyAtIndex.Number, false);
            if (elementCopyAtIndex.Character.IsHero)
            {
                EnterSettlementAction.ApplyForCharacterOnly(elementCopyAtIndex.Character.HeroObject, currentSettlement);
            }
        }
    }

    private void Handle_PrisonersDonated(MessagePayload<PrisonersDonated> obj)
    {
        FlattenedTroop[] rightSidePrisonerRoster = FlattenedTroopSerializer.Serialize(obj.What.RightSidePrisonerRoster, objectManager);
        if (!objectManager.TryGetIdWithLogging(obj.What.CurrentSettlement, out var currentSettlementId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.RightParty, out var rightPartyId)) return;

        var message = new DonatePrisoners(rightSidePrisonerRoster, currentSettlementId, rightPartyId);
        network.SendAll(message);
    }

    private void Handle_DonatePrisoners(MessagePayload<DonatePrisoners> obj)
    {
        FlattenedTroopRoster rightSidePrisonerRoster = FlattenedTroopSerializer.Deserialize(obj.What.RightSidePrisonerRoster, objectManager);
        if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.CurrentSettlementId, out var currentSettlement)) return;
        if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.RightPartyId, out var rightParty)) return;

        foreach (CharacterObject characterObject in rightSidePrisonerRoster.Troops)
        {
            if (characterObject.IsHero)
            {
                EnterSettlementAction.ApplyForPrisoner(characterObject.HeroObject, currentSettlement);
            }
        }
        CampaignEventDispatcher.Instance.OnPrisonerDonatedToSettlement(rightParty.MobileParty, rightSidePrisonerRoster, currentSettlement);
    }

    private void Handle_GarrisonManaged(MessagePayload<GarrisonManaged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.CurrentSettlement, out var currentSettlementId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.LeftMemberRoster, out var leftMemberRosterId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.LeftPrisonerRoster, out var leftPrisonerRosterId)) return;

        var message = new DoManageGarrison(currentSettlementId, leftMemberRosterId, leftPrisonerRosterId);
        network.SendAll(message);
    }

    private void Handle_DoManageGarrison(MessagePayload<DoManageGarrison> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.CurrentSettlementId, out var currentSettlement)) return;
        if (!objectManager.TryGetObjectWithLogging<TroopRoster>(obj.What.LeftMemberRosterId, out var leftMemberRoster)) return;
        if (!objectManager.TryGetObjectWithLogging<TroopRoster>(obj.What.LeftPrisonerRosterId, out var leftPrisonerRoster)) return;

        for (int i = 0; i < leftMemberRoster.Count; i++)
        {
            TroopRosterElement elementCopyAtIndex = leftMemberRoster.GetElementCopyAtIndex(i);
            if (elementCopyAtIndex.Character.IsHero)
            {
                EnterSettlementAction.ApplyForCharacterOnly(elementCopyAtIndex.Character.HeroObject, currentSettlement);
            }
        }
        for (int j = 0; j < leftPrisonerRoster.Count; j++)
        {
            TroopRosterElement elementCopyAtIndex2 = leftPrisonerRoster.GetElementCopyAtIndex(j);
            if (elementCopyAtIndex2.Character.IsHero)
            {
                EnterSettlementAction.ApplyForPrisoner(elementCopyAtIndex2.Character.HeroObject, currentSettlement);
            }
        }
    }

    private void Handle_PrisonersReleasedAndTaken(MessagePayload<PrisonersReleasedAndTaken> obj)
    {
        FlattenedTroop[] takenPrisonerRoster = FlattenedTroopSerializer.Serialize(obj.What.TakenPrisonerRoster, objectManager);
        FlattenedTroop[] releasedPrisonerRoster = FlattenedTroopSerializer.Serialize(obj.What.ReleasedPrisonerRoster, objectManager);

        var message = new ReleaseAndTakePrisoners(takenPrisonerRoster, releasedPrisonerRoster);
        network.SendAll(message);
    }

    private void Handle_ReleaseAndTakePrisoners(MessagePayload<ReleaseAndTakePrisoners> obj)
    {
        FlattenedTroopRoster takenPrisonerRoster = FlattenedTroopSerializer.Deserialize(obj.What.TakenPrisonerRoster, objectManager);
        FlattenedTroopRoster releasedPrisonerRoster = FlattenedTroopSerializer.Deserialize(obj.What.ReleasedPrisonerRoster, objectManager);

        if (!releasedPrisonerRoster.IsEmpty<FlattenedTroopRosterElement>())
        {
            EndCaptivityAction.ApplyByReleasedByChoice(releasedPrisonerRoster);
        }
        if (!takenPrisonerRoster.IsEmpty<FlattenedTroopRosterElement>())
        {
            TakePrisonerAction.ApplyByTakenFromPartyScreen(takenPrisonerRoster);
        }
    }
}