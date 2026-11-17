using Common.Logging;
using Common.Network;
using GameInterface.Configuration;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms;

public interface IKingdomClanLimit : IGameAbstraction
{
    // Whether the host's maxClansPerKingdom refuses this clan joining the kingdom, as a mercenary or a vassal.
    bool IsFull(Clan clan, Kingdom kingdom, bool asMercenary, ModOptions options);
    // IsFull with the current options. When it refuses, the player who asked gets NetworkKingdomFull.
    bool RefuseIfFull(NetPeer peer, Clan clan, Kingdom kingdom, bool asMercenary);
}

/// <summary>
/// The host's maxClansPerKingdom and countMercenaries rule. The server asks it before a clan joins a kingdom.
/// </summary>
public class KingdomClanLimit : IKingdomClanLimit
{
    private static readonly ILogger Logger = LogManager.GetLogger<KingdomClanLimit>();

    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public KingdomClanLimit(INetwork network, IObjectManager objectManager)
    {
        this.network = network;
        this.objectManager = objectManager;
    }

    // The ruling clan counts like any other. A clan already counted in the kingdom is never refused, but a
    // mercenary becoming a vassal there starts to count when mercenaries don't.
    public bool IsFull(Clan clan, Kingdom kingdom, bool asMercenary, ModOptions options)
    {
        if (options.MaxClansPerKingdom <= 0) return false;
        if (!Counts(asMercenary, options)) return false;
        if (clan.Kingdom == kingdom && Counts(clan.IsUnderMercenaryService, options)) return false;

        return kingdom.Clans.Count(member => !member.IsEliminated && Counts(member.IsUnderMercenaryService, options)) >= options.MaxClansPerKingdom;
    }

    public bool RefuseIfFull(NetPeer peer, Clan clan, Kingdom kingdom, bool asMercenary)
    {
        if (!IsFull(clan, kingdom, asMercenary, ModConfigProvider.ModOptions)) return false;

        Logger.Information("Refused clan {ClanId} joining kingdom {KingdomId} because it has maxClansPerKingdom clans", clan.StringId, kingdom.StringId);
        if (objectManager.TryGetIdWithLogging(kingdom, out var kingdomId))
            network.Send(peer, new NetworkKingdomFull(kingdomId));
        return true;
    }

    private static bool Counts(bool isMercenary, ModOptions options)
        => options.CountMercenaries || !isMercenary;
}
