using Common.Logging;
using Common.Messaging;
using Coop.Naval.Notifications.Messages;
using GameInterface.CoopSessionData;
using GameInterface.Services;
using GameInterface.Services.NavalDLC;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;

namespace Coop.Naval.CoopSession.Interfaces;

public interface ISessionNavalPlayerDataInterface : IGameAbstraction
{
    void UnlockFigurehead(Hero playerHero, Figurehead figurehead);
    List<Figurehead> GetUnlockedFigureheads(Hero playerHero);
    List<Figurehead> GetUnlockedFigureheads(NavalPlayerData navalPlayerData, string playerHeroId);
    CampaignTime GetLastFigureheadLootTime(string playerHeroId);
    void SetLastFigureheadLootTime(string playerHeroId, CampaignTime lootTime);
    void AddPlayerKeys(string playerHeroId);
}

public class SessionNavalPlayerDataInterface : ISessionNavalPlayerDataInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<SessionNavalPlayerDataInterface>();

    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;

    private NavalPlayerData NavalPlayerData => coopSessionProvider.CoopSession.NavalPlayerData;

    public SessionNavalPlayerDataInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager,
        IMessageBroker messageBroker)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
    }

    public void UnlockFigurehead(Hero playerHero, Figurehead figurehead)
    {
        if (!objectManager.TryGetIdWithLogging(playerHero, out var playerHeroId)) return;

        if (!TryRecordFigureheadUnlock(playerHeroId, figurehead.StringId, CampaignTime.Now._numTicks)) return;

        messageBroker.Publish(this, new NotifyFigureheadUnlocked(playerHero, figurehead));
    }

    private bool TryRecordFigureheadUnlock(string playerHeroId, string figureheadId, long lootTimeTicks)
    {
        AddPlayerKeys(playerHeroId);

        var unlockedFigureheads = NavalPlayerData.PlayerUnlockedFigureHeads[playerHeroId];
        if (unlockedFigureheads.Contains(figureheadId)) return false;

        unlockedFigureheads.Add(figureheadId);
        SetLastFigureheadLootTime(playerHeroId, new CampaignTime(lootTimeTicks));

        return true;
    }

    public List<Figurehead> GetUnlockedFigureheads(Hero playerHero)
    {
        if (!objectManager.TryGetIdWithLogging(playerHero, out var playerHeroId)) return new List<Figurehead>();

        return GetUnlockedFigureheads(NavalPlayerData, playerHeroId);
    }

    // Takes the data to read because a client only has the copy it was sent, not the session's
    public List<Figurehead> GetUnlockedFigureheads(NavalPlayerData navalPlayerData, string playerHeroId)
    {
        var unlockedFigureheads = new List<Figurehead>();

        // Null and key check for players without existing unlocked figureheads
        if (navalPlayerData?.PlayerUnlockedFigureHeads?.TryGetValue(playerHeroId, out var figureheadIds) != true) return unlockedFigureheads;

        foreach (var figureheadId in figureheadIds)
        {
            if (objectManager.TryGetObjectWithLogging<Figurehead>(figureheadId, out var figurehead)) unlockedFigureheads.Add(figurehead);
        }

        return unlockedFigureheads;
    }

    public CampaignTime GetLastFigureheadLootTime(string playerHeroId)
    {
        // Use vanilla's default if no data exists
        if (NavalPlayerData?.PlayerLastFigureheadLootTimes?.TryGetValue(playerHeroId, out var lootTimeTicks) != true) return CampaignTime.Zero;

        return new CampaignTime(lootTimeTicks);
    }

    public void SetLastFigureheadLootTime(string playerHeroId, CampaignTime lootTime)
    {
        AddPlayerKeys(playerHeroId);

        NavalPlayerData.PlayerLastFigureheadLootTimes[playerHeroId] = lootTime._numTicks;
    }

    public void AddPlayerKeys(string playerHeroId)
    {
        if (NavalPlayerData == null)
        {
            Logger.Error("NavalPlayerData was null");
            return;
        }

        if (!NavalPlayerData.PlayerUnlockedFigureHeads.ContainsKey(playerHeroId))
        {
            NavalPlayerData.PlayerUnlockedFigureHeads[playerHeroId] = new();
        }
        if (!NavalPlayerData.PlayerLastFigureheadLootTimes.ContainsKey(playerHeroId))
        {
            NavalPlayerData.PlayerLastFigureheadLootTimes[playerHeroId] = CampaignTime.Zero._numTicks;
        }
    }
}
