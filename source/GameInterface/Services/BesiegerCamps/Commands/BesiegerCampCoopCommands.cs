using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.BesiegerCamps.Commands;

public interface ISetBesiegerCampNumberOfTroopsKilledOnSideCoopCommand : ICoopCommand
{
}

public sealed class SetBesiegerCampNumberOfTroopsKilledOnSideCoopCommand : LegacyCoopCommand, ISetBesiegerCampNumberOfTroopsKilledOnSideCoopCommand
{
    public SetBesiegerCampNumberOfTroopsKilledOnSideCoopCommand()
        : base(
            "coop.debug.besiegercamp",
            "set_number_of_troops_killed_on_side",
            "Sets number of troops killed on side for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("besiegerCampId", "The besieger camp id."),
                new ExpectedArgs("value", "The value."),
            },
            BesiegerCampDebugCommand.SetBesiegerCampNumberOfTroopsKilledOnSide)
    {
    }
}

public interface ISetBesiegerCampPreparationsProgressCoopCommand : ICoopCommand
{
}

public sealed class SetBesiegerCampPreparationsProgressCoopCommand : LegacyCoopCommand, ISetBesiegerCampPreparationsProgressCoopCommand
{
    public SetBesiegerCampPreparationsProgressCoopCommand()
        : base(
            "coop.debug.besiegercamp",
            "set_progress",
            "Sets progress for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("besiegerCampId", "The besieger camp id."),
                new ExpectedArgs("progress", "The progress."),
            },
            BesiegerCampDebugCommand.SetBesiegerCampPreparationsProgress)
    {
    }
}

public interface ISetBesiegerCampSiegeStrategyCoopCommand : ICoopCommand
{
}

public sealed class SetBesiegerCampSiegeStrategyCoopCommand : LegacyCoopCommand, ISetBesiegerCampSiegeStrategyCoopCommand
{
    public SetBesiegerCampSiegeStrategyCoopCommand()
        : base(
            "coop.debug.besiegercamp",
            "set_siege_strategy",
            "Sets siege strategy for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("besiegerCampId", "The besieger camp id."),
                new ExpectedArgs("strategyId", "The strategy id."),
            },
            BesiegerCampDebugCommand.SetBesiegerCampSiegeStrategy)
    {
    }
}

public interface ISetBesiegerCampLeaderPartyCoopCommand : ICoopCommand
{
}

public sealed class SetBesiegerCampLeaderPartyCoopCommand : LegacyCoopCommand, ISetBesiegerCampLeaderPartyCoopCommand
{
    public SetBesiegerCampLeaderPartyCoopCommand()
        : base(
            "coop.debug.besiegercamp",
            "set_leader_party",
            "Sets leader party for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("besiegerCampId", "The besieger camp id."),
                new ExpectedArgs("partyId", "The party id."),
            },
            BesiegerCampDebugCommand.SetBesiegerCampLeaderParty)
    {
    }
}

public interface IAddPartyToBesiegerCampCoopCommand : ICoopCommand
{
}

public sealed class AddPartyToBesiegerCampCoopCommand : LegacyCoopCommand, IAddPartyToBesiegerCampCoopCommand
{
    public AddPartyToBesiegerCampCoopCommand()
        : base(
            "coop.debug.besiegercamp",
            "add_besieger_party",
            "Adds besieger party for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("besiegerCampId", "The besieger camp id."),
                new ExpectedArgs("partyId", "The party id."),
            },
            BesiegerCampDebugCommand.AddPartyToBesiegerCamp)
    {
    }
}

public interface IRemovePartyFromBesiegerCampCoopCommand : ICoopCommand
{
}

public sealed class RemovePartyFromBesiegerCampCoopCommand : LegacyCoopCommand, IRemovePartyFromBesiegerCampCoopCommand
{
    public RemovePartyFromBesiegerCampCoopCommand()
        : base(
            "coop.debug.besiegercamp",
            "remove_party",
            "Removes party for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("besiegerCampId", "The besieger camp id."),
                new ExpectedArgs("partyId", "The party id."),
            },
            BesiegerCampDebugCommand.RemovePartyFromBesiegerCamp)
    {
    }
}
