using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Buildings.Messages;
using GameInterface.Services.Clans.Extensions;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;

namespace GameInterface.Services.Buildings.Handlers;

internal class BuildingsCampaignBehaviorHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BuildingsCampaignBehaviorHandler>();

    private readonly IMessageBroker messageBroker;

    public BuildingsCampaignBehaviorHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<OnSettlementOwnerChanged>(Handle_OnSettlementOwnerChanged);
        messageBroker.Subscribe<BuildingsDailySettlementTick>(Handle_BuildingsDailySettlementTick);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<OnSettlementOwnerChanged>(Handle_OnSettlementOwnerChanged);
        messageBroker.Unsubscribe<BuildingsDailySettlementTick>(Handle_BuildingsDailySettlementTick);
    }

    private void Handle_OnSettlementOwnerChanged(MessagePayload<OnSettlementOwnerChanged> obj)
    {
        if (obj.What.Settlement.Town != null && !obj.What.NewOwner.Clan.IsPlayerClan())
        {
            obj.What.Settlement.Town.BuildingsInProgress.Clear();
        }
    }

    private void Handle_BuildingsDailySettlementTick(MessagePayload<BuildingsDailySettlementTick> obj)
    {
        if (obj.What.Settlement.IsFortification)
        {
            Town town = obj.What.Settlement.Town;
            foreach (Building building in town.Buildings)
            {
                if (town.Owner.Settlement.SiegeEvent == null)
                {
                    building.HitPointChanged(10f);
                }
            }
            if (!town.Owner.Settlement.OwnerClan.IsPlayerClan()) // Replacement for checking if not Clan.PlayerClan
            {
                if (MBRandom.RandomFloat < 0.1f)
                {
                    BuildingsCampaignBehavior.DecideBuildingQueue(town);
                }
                if (MBRandom.RandomFloat < 0.01f)
                {
                    BuildingsCampaignBehavior.DecideDailyProject(town);
                }
            }
            if (!town.CurrentBuilding.BuildingType.IsDailyProject)
            {
                obj.What.BuildingsCampaignBehavior.TickCurrentBuildingForTown(town);
                return;
            }
            if (town.Governor != null && town.Governor.GetPerkValue(DefaultPerks.Charm.Virile) && MBRandom.RandomFloat <= DefaultPerks.Charm.Virile.SecondaryBonus)
            {
                Hero randomElement = obj.What.Settlement.Notables.GetRandomElement<Hero>();
                if (randomElement != null)
                {
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(town.Governor.Clan.Leader, randomElement, 1, false);
                }
            }
        }
    }
}