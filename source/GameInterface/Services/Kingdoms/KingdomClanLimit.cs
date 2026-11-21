using GameInterface.Configuration;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Kingdoms;

// Public because the public VassalAndMercenaryOfferCampaignBehaviorInterface takes it in its constructor.
public interface IKingdomClanLimit : IGameAbstraction
{
    // Whether maxClansPerKingdom refuses the clan joining the kingdom. The clan is null for a new clan made for it, the kingdom null when there is none.
    bool RefusesJoin(Clan clan, Kingdom kingdom, bool asMercenary, ModOptions options);
}

/// <summary>
/// The host's maxClansPerKingdom and maxClansPerKingdomCountsMercenaries rule. The server asks it before a clan joins a kingdom.
/// </summary>
public class KingdomClanLimit : IKingdomClanLimit
{
    // The ruling clan counts like any other.
    public bool RefusesJoin(Clan clan, Kingdom kingdom, bool asMercenary, ModOptions options)
    {
        // A ruler without a kingdom makes an independent clan, which nothing limits.
        if (kingdom == null) return false;
        if (options.MaxClansPerKingdom <= 0) return false;
        if (!Counts(asMercenary, options)) return false;

        // A clan already counted in the kingdom is never refused, but a mercenary becoming a vassal there starts to count when mercenaries don't.
        bool alreadyCounted = clan?.Kingdom == kingdom && Counts(clan.IsUnderMercenaryService, options);
        if (alreadyCounted) return false;

        return kingdom.Clans.Count(member => Counts(member.IsUnderMercenaryService, options)) >= options.MaxClansPerKingdom;
    }

    /// <summary>The refusal every player sees. A null kingdom, unknown or the other player's, reads as "That kingdom".</summary>
    public static TextObject GetFullText(Kingdom kingdom, ModOptions options)
    {
        var text = new TextObject("{=coop_kingdom_full}{KINGDOM} already has the most clans this server allows ({MAX}).");
        text.SetTextVariable("KINGDOM", kingdom?.Name ?? new TextObject("{=coop_kingdom_full_other}That kingdom"));
        text.SetTextVariable("MAX", options.MaxClansPerKingdom);
        return text;
    }

    private static bool Counts(bool isMercenary, ModOptions options)
        => options.MaxClansPerKingdomCountsMercenaries || !isMercenary;
}
