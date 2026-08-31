using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Villages.Commands;

public interface IViewInteractedVillagersCommandCoopCommand : ICoopCommand
{
}

public sealed class ViewInteractedVillagersCommandCoopCommand : LegacyCoopCommand, IViewInteractedVillagersCommandCoopCommand
{
    public ViewInteractedVillagersCommandCoopCommand()
        : base(
            "coop.debug.villagers",
            "view_interacted_villagers",
            "Shows interacted villagers for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            VillagerPartiesCommands.ViewInteractedVillagersCommand)
    {
    }
}

public interface IViewLootedVillagersCoopCommand : ICoopCommand
{
}

public sealed class ViewLootedVillagersCoopCommand : LegacyCoopCommand, IViewLootedVillagersCoopCommand
{
    public ViewLootedVillagersCoopCommand()
        : base(
            "coop.debug.villagers",
            "view_looted_villagers",
            "Shows looted villagers for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            VillagerPartiesCommands.ViewLootedVillagers)
    {
    }
}
