using Common.Logging;
using Serilog;

namespace GameInterface.Services.MobileParties.Interfaces;

/// <summary>
/// Abstracts interacting with the MobileParty class in game
/// </summary>
public interface IMobilePartyInterface : IGameAbstraction
{
}

internal class MobilePartyInterface : IMobilePartyInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyInterface>();
}
