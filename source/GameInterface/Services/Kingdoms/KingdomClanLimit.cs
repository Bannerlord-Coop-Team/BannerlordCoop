using GameInterface.Configuration;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Kingdoms;

// Public because the public VassalAndMercenaryOfferCampaignBehaviorInterface takes it in its constructor.
public interface IKingdomClanLimit : IGameAbstraction
{
    // Whether maxClansPerKingdom refuses the clan joining the kingdom. The clan is null for a new clan made for it.
    bool IsFull(Clan clan, Kingdom kingdom, bool asMercenary, ModOptions options);
}

/// <summary>
/// The host's maxClansPerKingdom and countMercenaries rule. The server asks it before a clan joins a kingdom.
/// </summary>
public class KingdomClanLimit : IKingdomClanLimit
{
    // The ruling clan counts like any other. A clan already counted in the kingdom is never refused, but a
    // mercenary becoming a vassal there starts to count when mercenaries don't.
    public bool IsFull(Clan clan, Kingdom kingdom, bool asMercenary, ModOptions options)
    {
        if (options.MaxClansPerKingdom <= 0) return false;
        if (!Counts(asMercenary, options)) return false;
        if (clan?.Kingdom == kingdom && Counts(clan.IsUnderMercenaryService, options)) return false;

        return kingdom.Clans.Count(member => Counts(member.IsUnderMercenaryService, options)) >= options.MaxClansPerKingdom;
    }

    /// <summary>The refusal every player sees. A null kingdom reads as the other player's kingdom.</summary>
    public static TextObject GetFullText(Kingdom kingdom, ModOptions options)
    {
        var text = new TextObject("{=coop_kingdom_full}{KINGDOM} already has the most clans this server allows ({MAX}).");
        text.SetTextVariable("KINGDOM", kingdom?.Name ?? new TextObject("{=coop_kingdom_full_other}The other player's kingdom"));
        text.SetTextVariable("MAX", options.MaxClansPerKingdom);
        return text;
    }

    private static bool Counts(bool isMercenary, ModOptions options)
        => options.CountMercenaries || !isMercenary;
}
