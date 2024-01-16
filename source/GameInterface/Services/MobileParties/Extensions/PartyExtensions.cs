using Common.Logging;
using GameInterface.Services.Entity;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Extensions;

internal static class PartyExtensions
{
    private static ILogger Logger = LogManager.GetLogger<MobileParty>();

    public static bool IsPartyControlled(this MobileParty party)
    {
        // Allow method if container or registry cannot be resolved
        if (ContainerProvider.TryResolve<IControlledEntityRegistry>(out var entityRegistry) == false)
        {
            Logger.Error("Unable to resolve {name}", nameof(IControlledEntityRegistry));
            return false;

        }
        if (ContainerProvider.TryResolve<IControllerIdProvider>(out var idProvider) == false)
        {
            Logger.Error("Unable to resolve {name}", nameof(IControllerIdProvider));
            return false;
        };

        return entityRegistry.IsControlledBy(idProvider.ControllerId, party.StringId);
    }
}
