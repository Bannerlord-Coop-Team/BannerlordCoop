using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Messages;
using Serilog;
using System;
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

    public PartyScreenHelperHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

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
        messageBroker.Unsubscribe<GarrisonDonated>(Handle_GarrisonDonated);
        messageBroker.Unsubscribe<DonateToGarrison>(Handle_DonateToGarrison);
        messageBroker.Unsubscribe<PrisonersDonated>(Handle_PrisonersDonated);
        messageBroker.Unsubscribe<DonatePrisoners>(Handle_DonatePrisoners);
        messageBroker.Unsubscribe<GarrisonManaged>(Handle_GarrisonManaged);
        messageBroker.Unsubscribe<DoManageGarrison>(Handle_DoManageGarrison);
        messageBroker.Unsubscribe<PrisonersReleasedAndTaken>(Handle_PrisonersReleasedAndTaken);
        messageBroker.Unsubscribe<ReleaseAndTakePrisoners>(Handle_ReleaseAndTakePrisoners);
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
        var data = obj.What;

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Settlement>(data.CurrentSettlementId, out var currentSettlement)) return;
                if (!objectManager.TryGetObjectWithLogging<TroopRoster>(data.LeftMemberRosterId, out var leftMemberRoster)) return;

                // Server-authoritative apply: run with patches live (no AllowedThread) so a
                // newly created garrison party registers and replicates to clients, and the
                // donated troops broadcast as roster deltas.
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
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply {Message}", nameof(DonateToGarrison));
            }
        });
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
        var data = obj.What;

        GameThread.Run(() =>
        {
            try
            {
                FlattenedTroopRoster rightSidePrisonerRoster = FlattenedTroopSerializer.Deserialize(data.RightSidePrisonerRoster, objectManager);
                if (!objectManager.TryGetObjectWithLogging<Settlement>(data.CurrentSettlementId, out var currentSettlement)) return;
                if (!objectManager.TryGetObjectWithLogging<PartyBase>(data.RightPartyId, out var rightParty)) return;

                using (new AllowedThread())
                {
                    foreach (CharacterObject characterObject in rightSidePrisonerRoster.Troops)
                    {
                        if (characterObject.IsHero)
                        {
                            EnterSettlementAction.ApplyForPrisoner(characterObject.HeroObject, currentSettlement);
                        }
                    }
                    CampaignEventDispatcher.Instance.OnPrisonerDonatedToSettlement(rightParty.MobileParty, rightSidePrisonerRoster, currentSettlement);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply {Message}", nameof(DonatePrisoners));
            }
        });
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
        var data = obj.What;

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Settlement>(data.CurrentSettlementId, out var currentSettlement)) return;
                if (!objectManager.TryGetObjectWithLogging<TroopRoster>(data.LeftMemberRosterId, out var leftMemberRoster)) return;
                if (!objectManager.TryGetObjectWithLogging<TroopRoster>(data.LeftPrisonerRosterId, out var leftPrisonerRoster)) return;

                using (new AllowedThread())
                {
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
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply {Message}", nameof(DoManageGarrison));
            }
        });
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
        var data = obj.What;

        GameThread.Run(() =>
        {
            try
            {
                FlattenedTroopRoster takenPrisonerRoster = FlattenedTroopSerializer.Deserialize(data.TakenPrisonerRoster, objectManager);
                FlattenedTroopRoster releasedPrisonerRoster = FlattenedTroopSerializer.Deserialize(data.ReleasedPrisonerRoster, objectManager);

                // Server-authoritative apply: run with patches live (no AllowedThread) so each
                // capture/release side effect replicates to clients as its own message (roster
                // deltas and auto-synced hero state), and so a player hero's release routes
                // through the coop captivity path instead of the partial native one.
                if (!releasedPrisonerRoster.IsEmpty<FlattenedTroopRosterElement>())
                {
                    EndCaptivityAction.ApplyByReleasedByChoice(releasedPrisonerRoster);
                }
                if (!takenPrisonerRoster.IsEmpty<FlattenedTroopRosterElement>())
                {
                    TakePrisonerAction.ApplyByTakenFromPartyScreen(takenPrisonerRoster);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply {Message}", nameof(ReleaseAndTakePrisoners));
            }
        });
    }
}