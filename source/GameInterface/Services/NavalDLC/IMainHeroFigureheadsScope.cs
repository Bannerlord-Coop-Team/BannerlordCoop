using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.NavalDLC;

/// <summary>
/// Gives the main hero a player's unlocked figureheads. Implemented in Coop.Naval, which is only registered while
/// NavalDLC co-op is on.
/// </summary>
public interface IMainHeroFigureheadsScope
{
    /// <summary>[Server, game thread] Sets Campaign.UnlockedFigureheadsByMainHero to the player's until the returned scope is disposed.</summary>
    IDisposable Enter(Hero playerHero);
}
