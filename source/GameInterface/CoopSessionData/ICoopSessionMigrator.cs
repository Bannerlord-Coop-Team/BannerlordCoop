using Common.Logging;
using Common.Network;
using GameInterface.CoopSessionData.Messages;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.CoopSessionData;

public interface ICoopSessionMigrator : IGameAbstraction
{
    void MigratePlayerData(Hero oldPlayerHero, Hero newPlayerHero);
}

public class CoopSessionMigrator : ICoopSessionMigrator
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopSessionMigrator>();

    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    private CoopSession Session => (CoopSession)coopSessionProvider.CoopSession;

    public CoopSessionMigrator(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager,
        INetwork network)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.network = network;
    }

    public void MigratePlayerData(Hero oldPlayerHero, Hero newPlayerHero)
    {
        if (!objectManager.TryGetIdWithLogging(oldPlayerHero, out var oldPlayerHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(newPlayerHero, out var newPlayerHeroId)) return;

        if (!TryMigratePreservedFields(oldPlayerHeroId, newPlayerHeroId))
        {
            Logger.Error($"Error preserving fields during CoopSession migration from {oldPlayerHeroId} to {newPlayerHeroId}");
            return;
        }

        if (!TryMigrateClearedFields(oldPlayerHeroId))
        {
            Logger.Error($"Error clearing fields during CoopSession migration from {oldPlayerHeroId} to {newPlayerHeroId}");
            return;
        }

        // Update CoopSession on clients. When client calls PlayerHeroChanged, initialization handlers update with new data
        network.SendAll(new NetworkUpdateCoopSession(Session));
    }

    private bool TryMigratePreservedFields(string oldPlayerHeroId, string newPlayerHeroId)
    {
        foreach (var property in CoopSessionMigrationRules.PreserveWithMigration)
        {
            if (!TryGetPlayerDictionary(property, out var dictionary)) return false;

            if (!dictionary.Contains(oldPlayerHeroId)) return false;

            var playerData = dictionary[oldPlayerHeroId];

            dictionary.Remove(oldPlayerHeroId);
            dictionary[newPlayerHeroId] = playerData;
        }

        return true;
    }

    private bool TryMigrateClearedFields(string oldPlayerHeroId)
    {
        foreach (var property in CoopSessionMigrationRules.ClearWithMigration)
        {
            if (!TryGetPlayerDictionary(property, out var dictionary)) return false;

            dictionary.Remove(oldPlayerHeroId);
        }

        return true;
    }

    private bool TryGetPlayerDictionary(PropertyInfo propertyInfo, out IDictionary dictionary)
    {
        dictionary = null;

        if (propertyInfo == null) return false;

        // Find the property of the CoopSession that owns the target property
        // e.g. Result is CoopSession.CraftingPlayerData for propertyInfo CraftingPlayerData.PlayerCraftedItemsHistory
        var ownerProperty = typeof(CoopSession)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .SingleOrDefault(sessionProperty => sessionProperty.PropertyType == propertyInfo.DeclaringType);

        if (ownerProperty == null) return false;

        // Get reference to actual property instance/object from CoopSession
        // e.g. CraftingPlayerData
        var owner = ownerProperty.GetValue(Session);
        if (owner == null) return false;

        // Get reference to actual dictionary from found CoopSession property
        dictionary = propertyInfo.GetValue(owner) as IDictionary;
        return dictionary != null;
    }
}