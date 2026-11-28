using GameInterface.Configuration;
using SandBox.AdvancedStartOptions;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.AdvancedStart;

/// <summary>
/// A starting option a joining player sets on top of whichever start they pick, such as War Sails' PersonalShip
/// (on or off) or a number of siblings. Register an implementation to offer it.
/// </summary>
public interface IAdvancedStartModifier
{
    /// <summary>The option's id, which is also the vanilla option's id when the window shows vanilla's own.</summary>
    string OptionId { get; }

    /// <summary>The most a player can pick under the host's config: 1 for an on/off option, 0 when switched off.</summary>
    int GetLimit(AdvancedStartConfigData config);

    /// <summary>Client: the option the window shows under the host's <paramref name="limit"/>, or null to show
    /// vanilla's own option with this id as vanilla built it.</summary>
    AdvancedStartOption CreateOption(int limit);

    /// <summary>Server, game thread: applies the picked <paramref name="value"/>, from 1 up to the host's limit, to
    /// <paramref name="hero"/> after their start, with the hero standing in as the main hero and patches live.</summary>
    void Run(Hero hero, int value);
}
