using GameInterface.Configuration;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.AdvancedStart;

/// <summary>
/// An on/off vanilla starting option, such as War Sails' PersonalShip, that a joining player can tick on top of
/// whichever start they pick. The window shows the vanilla option as vanilla built it. Register an implementation to offer it.
/// </summary>
public interface IAdvancedStartModifier
{
    /// <summary>The vanilla option's id.</summary>
    string OptionId { get; }

    /// <summary>Whether the host's config offers it.</summary>
    bool IsOffered(AdvancedStartConfigData config);

    /// <summary>Server, game thread: applies it to <paramref name="hero"/> after their start, with the hero standing
    /// in as the main hero and patches live.</summary>
    void Run(Hero hero);
}
