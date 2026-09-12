using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Kingdoms;

/// <summary>
/// Server-authoritative kingdom creation. Shared by the governor "create kingdom" dialog request and by
/// the coop.debug.kingdom.create console command so both paths register the new kingdom with the coop
/// object manager, publish the same membership collection changes, and broadcast the same
/// <see cref="PlayerKingdomCreated"/> notification to clients.
/// </summary>
public interface IKingdomCreator
{
    /// <summary>
    /// Creates a kingdom ruled by <paramref name="rulingClan"/>, registers it with the coop object
    /// manager, and publishes <see cref="PlayerKingdomCreated"/> so clients relink the ruling clan.
    /// </summary>
    /// <param name="rulingClan">Clan that will rule the new kingdom</param>
    /// <param name="kingdomName">Display name of the new kingdom</param>
    /// <param name="culture">Culture the kingdom inherits, also the source of its default policies</param>
    /// <param name="controllerId">Owning co-op player, or empty when no player owns the clan (the
    /// notification's settlement restore is a no-op for unknown controllers)</param>
    /// <param name="kingdomId">Coop object manager id of the created kingdom, null on failure</param>
    /// <param name="error">Failure reason, null on success</param>
    /// <returns>True when the kingdom exists and has a coop id, otherwise False</returns>
    bool TryCreateKingdom(
        Clan rulingClan,
        string kingdomName,
        CultureObject culture,
        string controllerId,
        out string kingdomId,
        out string error);
}

internal class KingdomCreator : IKingdomCreator
{
    private static readonly ILogger Logger = LogManager.GetLogger<KingdomCreator>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IKingdomMembershipState kingdomMembershipState;

    public KingdomCreator(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IKingdomMembershipState kingdomMembershipState)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.kingdomMembershipState = kingdomMembershipState;
    }

    public bool TryCreateKingdom(
        Clan rulingClan,
        string kingdomName,
        CultureObject culture,
        string controllerId,
        out string kingdomId,
        out string error)
    {
        kingdomId = null;

        if (rulingClan == null)
        {
            error = "ruling clan was null";
            return false;
        }

        if (string.IsNullOrWhiteSpace(kingdomName))
        {
            error = "kingdom name was empty";
            return false;
        }

        if (culture == null)
        {
            error = "culture was null";
            return false;
        }

        var campaign = Campaign.Current;
        if (campaign == null)
        {
            error = "no campaign is loaded";
            return false;
        }

        if (!objectManager.TryGetIdWithLogging(rulingClan, out string clanId))
        {
            error = "ruling clan is not registered with the coop object manager";
            return false;
        }

        var campaignObjectManager = campaign.CampaignObjectManager;
        TextObject name = new TextObject(kingdomName);

        try
        {
            campaign.KingdomManager?.CreateKingdom(
                name,
                name,
                culture,
                rulingClan,
                culture.DefaultPolicyList,
                TextObject.GetEmpty(),
                name,
                TextObject.GetEmpty());
        }
        catch (Exception e)
        {
            Logger.Warning(
                e,
                "Native kingdom creation failed for {KingdomName}; falling back to coop kingdom state creation.",
                kingdomName);
        }

        Kingdom kingdom = rulingClan.Kingdom ?? campaignObjectManager.Kingdoms
            .FirstOrDefault(existing => existing?.RulingClan == rulingClan && existing.Name?.ToString() == kingdomName)
            ?? CreateCoopKingdom(name, culture, rulingClan);

        if (kingdom == null)
        {
            error = "native creation completed but no kingdom was assigned to the clan";
            return false;
        }

        EnsureKingdomRegisteredInCampaign(kingdom, campaignObjectManager);

        if (!objectManager.TryGetId(kingdom, out kingdomId))
        {
            messageBroker.Publish(this, new InstanceCreated<Kingdom>(kingdom));
        }

        SyncCreatedKingdomProperties(kingdom, name, culture);

        if (!objectManager.TryGetId(kingdom, out kingdomId))
        {
            error = "created kingdom could not be registered with the coop object manager";
            return false;
        }

        kingdomMembershipState.EnsureClanInKingdom(kingdom, rulingClan, publishCollectionChanges: true);

        objectManager.TryGetId(culture, out string cultureId);
        messageBroker.Publish(
            this,
            new PlayerKingdomCreated(controllerId ?? string.Empty, kingdomId, kingdomName, clanId, cultureId));

        error = null;
        return true;
    }

    private static Kingdom CreateCoopKingdom(TextObject kingdomName, CultureObject culture, Clan clan)
    {
        var kingdom = new Kingdom();

        kingdom._rulingClan = clan;
        SyncCreatedKingdomProperties(kingdom, kingdomName, culture);
        return kingdom;
    }

    internal static void SyncCreatedKingdomProperties(Kingdom kingdom, TextObject kingdomName, CultureObject culture)
    {
        KingdomRegistry.EnsureRuntimeCollections(kingdom);

        kingdom.Name = kingdomName;
        kingdom.InformalName = kingdomName;
        kingdom.Culture = culture;
        kingdom.EncyclopediaText = TextObject.GetEmpty();
        kingdom.EncyclopediaTitle = kingdomName;
        kingdom.EncyclopediaRulerTitle = TextObject.GetEmpty();
        kingdom._isEliminated = false;
    }

    internal static void EnsureKingdomRegisteredInCampaign(Kingdom kingdom, CampaignObjectManager campaignObjectManager)
    {
        if (campaignObjectManager == null || campaignObjectManager.Kingdoms.Contains(kingdom)) return;

        using (new AllowedThread())
        {
            kingdom._isEliminated = false;
        }

        campaignObjectManager.AddKingdom(kingdom);
        if (!campaignObjectManager.Kingdoms.Contains(kingdom)
            && campaignObjectManager._kingdoms != null
            && !campaignObjectManager._kingdoms.Contains(kingdom))
        {
            campaignObjectManager._kingdoms.Add(kingdom);
        }
    }
}
