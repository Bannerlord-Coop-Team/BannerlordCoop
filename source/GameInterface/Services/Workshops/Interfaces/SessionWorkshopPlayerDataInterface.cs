using Autofac.Features.OwnedInstances;
using Common.Logging;
using GameInterface.CoopSessionData;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace GameInterface.Services.Workshops.Interfaces;

public interface ISessionWorkshopPlayerDataInterface : IGameAbstraction
{
    void AddNewWarehouseDataIfNeeded(string ownerId, string settlementId);
    void RemoveWarehouseData(string ownerId, string settlementId);
    void AddPlayerKeys(string playerHeroId);
}

public class SessionWorkshopPlayerDataInterface : ISessionWorkshopPlayerDataInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<SessionWorkshopPlayerDataInterface>();
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private WorkshopPlayerData WorkshopPlayerData => coopSessionProvider.CoopSession.WorkshopPlayerData;

    public SessionWorkshopPlayerDataInterface(ICoopSessionProvider coopSessionProvider, IPlayerManager playerManager, IObjectManager objectManager)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
    }

    public void AddNewWarehouseDataIfNeeded(string ownerId, string settlementId)
    {
        bool existingData = false;
        foreach (var settlementWarehouseRoster in WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId])
        {
            if (settlementWarehouseRoster.Key == settlementId)
            {
                existingData = true;
                break;
            }
        }
        if (!existingData)
        {
            for (int j = 0; j < WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId].Length; j++)
            {
                var settlementWarehouseRoster = WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId][j];
                if (settlementWarehouseRoster.Value == null)
                {
                    WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId][j] = new KeyValuePair<string, List<ItemRosterElement>>(settlementId, new List<ItemRosterElement>());
                    return;
                }

            }
        }
    }

    public void RemoveWarehouseData(string ownerId, string settlementId)
    {
        for (int i = 0; i < WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId].Length; i++)
        {
            if (WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId][i].Key == settlementId)
            {
                WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[ownerId][i] = new KeyValuePair<string, List<ItemRosterElement>>(null, null);
                return;
            }
        }
    }

    public void AddPlayerKeys(string playerHeroId)
    {
        if (WorkshopPlayerData == null)
        {
            Logger.Error("WorkshopPlayerData was null");
            return;
        }

        if (!WorkshopPlayerData.PlayerWarehouseRosterPerSettlement.ContainsKey(playerHeroId))
        {
            WorkshopPlayerData.PlayerWarehouseRosterPerSettlement[playerHeroId] = new KeyValuePair<string, List<ItemRosterElement>>[Campaign.Current.Models.ClanTierModel.MaxClanTier + 1];
        }
    }

    private bool IsPlayerHeroIdValid(string playerHeroId)
    {
        return objectManager.TryGetObjectWithLogging(playerHeroId, out Hero _);
    }
}
