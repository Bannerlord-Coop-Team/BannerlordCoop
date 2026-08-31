using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Settlements.Commands;

public interface IEnterRandomCastleCoopCommand : ICoopCommand
{
}

public sealed class EnterRandomCastleCoopCommand : LegacyCoopCommand, IEnterRandomCastleCoopCommand
{
    public EnterRandomCastleCoopCommand()
        : base(
            "coop.debug.settlements",
            "enter_random_castle",
            "Enters random castle for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("castleId", "The castle id.", isRequired: false),
            },
            SettlementCommands.EnterRandomCastle)
    {
    }
}

#if DEBUG
public interface ITeleportMainPartyToCastleCoopCommand : ICoopCommand
{
}

public sealed class TeleportMainPartyToCastleCoopCommand : LegacyCoopCommand, ITeleportMainPartyToCastleCoopCommand
{
    public TeleportMainPartyToCastleCoopCommand()
        : base(
            "coop.debug.settlements",
            "teleport_main_party_to_castle",
            "Runs main party to castle for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("castleId", "The castle id."),
            },
            SettlementCommands.TeleportMainPartyToCastle)
    {
    }
}

public interface IRestoreMainPartyCastleTeleportCoopCommand : ICoopCommand
{
}

public sealed class RestoreMainPartyCastleTeleportCoopCommand : LegacyCoopCommand, IRestoreMainPartyCastleTeleportCoopCommand
{
    public RestoreMainPartyCastleTeleportCoopCommand()
        : base(
            "coop.debug.settlements",
            "restore_main_party_castle_teleport",
            "Restores main party castle teleport for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            SettlementCommands.RestoreMainPartyCastleTeleport)
    {
    }
}
#endif

public interface IGetTownNameCoopCommand : ICoopCommand
{
}

public sealed class GetTownNameCoopCommand : LegacyCoopCommand, IGetTownNameCoopCommand
{
    public GetTownNameCoopCommand()
        : base(
            "coop.debug.settlements",
            "get_town_name",
            "Gets town name for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SettlementCommands.GetTownName)
    {
    }
}

public interface ISetEnemiesSpottedCoopCommand : ICoopCommand
{
}

public sealed class SetEnemiesSpottedCoopCommand : LegacyCoopCommand, ISetEnemiesSpottedCoopCommand
{
    public SetEnemiesSpottedCoopCommand()
        : base(
            "coop.debug.settlements",
            "set_enemies_spotted",
            "Sets enemies spotted for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
                new ExpectedArgs("value", "The value."),
            },
            SettlementCommands.SetEnemiesSpotted)
    {
    }
}

public interface ISetAlliesSpottedCoopCommand : ICoopCommand
{
}

public sealed class SetAlliesSpottedCoopCommand : LegacyCoopCommand, ISetAlliesSpottedCoopCommand
{
    public SetAlliesSpottedCoopCommand()
        : base(
            "coop.debug.settlements",
            "set_allies_spotted",
            "Sets allies spotted for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
                new ExpectedArgs("value", "The value."),
            },
            SettlementCommands.SetAlliesSpotted)
    {
    }
}

public interface ISetBribePaidCoopCommand : ICoopCommand
{
}

public sealed class SetBribePaidCoopCommand : LegacyCoopCommand, ISetBribePaidCoopCommand
{
    public SetBribePaidCoopCommand()
        : base(
            "coop.debug.settlements",
            "set_bribe_paid",
            "Sets bribe paid for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
                new ExpectedArgs("value", "The value."),
            },
            SettlementCommands.SetBribePaid)
    {
    }
}

public interface ISetHitPointsCoopCommand : ICoopCommand
{
}

public sealed class SetHitPointsCoopCommand : LegacyCoopCommand, ISetHitPointsCoopCommand
{
    public SetHitPointsCoopCommand()
        : base(
            "coop.debug.settlements",
            "set_hit_points",
            "Sets hit points for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
                new ExpectedArgs("value", "The value."),
            },
            SettlementCommands.SetHitPoints)
    {
    }
}

public interface ISetLastAttackerPartyCoopCommand : ICoopCommand
{
}

public sealed class SetLastAttackerPartyCoopCommand : LegacyCoopCommand, ISetLastAttackerPartyCoopCommand
{
    public SetLastAttackerPartyCoopCommand()
        : base(
            "coop.debug.settlements",
            "last_attacker",
            "Runs attacker for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
                new ExpectedArgs("lastAttackerPartyId", "The last attacker party id."),
            },
            SettlementCommands.SetLastAttackerParty)
    {
    }
}

public interface IListSiegeStatesCoopCommand : ICoopCommand
{
}

public sealed class ListSiegeStatesCoopCommand : LegacyCoopCommand, IListSiegeStatesCoopCommand
{
    public ListSiegeStatesCoopCommand()
        : base(
            "coop.debug.settlements",
            "list_siege_state",
            "Lists siege state for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            SettlementCommands.ListSiegeStates)
    {
    }
}

public interface ISetSiegeStateCoopCommand : ICoopCommand
{
}

public sealed class SetSiegeStateCoopCommand : LegacyCoopCommand, ISetSiegeStateCoopCommand
{
    public SetSiegeStateCoopCommand()
        : base(
            "coop.debug.settlements",
            "set_siege_state",
            "Sets siege state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
                new ExpectedArgs("siegeState", "The siege state."),
            },
            SettlementCommands.SetSiegeState)
    {
    }
}

public interface ISetMiltiiaCoopCommand : ICoopCommand
{
}

public sealed class SetMiltiiaCoopCommand : LegacyCoopCommand, ISetMiltiiaCoopCommand
{
    public SetMiltiiaCoopCommand()
        : base(
            "coop.debug.settlements",
            "set_militia",
            "Sets militia for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
                new ExpectedArgs("militia", "The militia."),
            },
            SettlementCommands.SetMiltiia)
    {
    }
}

public interface ISetGarrisonWageLimitCoopCommand : ICoopCommand
{
}

public sealed class SetGarrisonWageLimitCoopCommand : LegacyCoopCommand, ISetGarrisonWageLimitCoopCommand
{
    public SetGarrisonWageLimitCoopCommand()
        : base(
            "coop.debug.settlements",
            "set_garrison_pay_limit",
            "Sets garrison pay limit for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
                new ExpectedArgs("payLimit", "The pay limit."),
            },
            SettlementCommands.SetGarrisonWageLimit)
    {
    }
}

public interface ICollectCacheNotablesCoopCommand : ICoopCommand
{
}

public sealed class CollectCacheNotablesCoopCommand : LegacyCoopCommand, ICollectCacheNotablesCoopCommand
{
    public CollectCacheNotablesCoopCommand()
        : base(
            "coop.debug.settlements",
            "collect_cache_notables",
            "Collects cache notables for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SettlementCommands.CollectCacheNotables)
    {
    }
}

public interface IInfoCoopCommand : ICoopCommand
{
}

public sealed class InfoCoopCommand : LegacyCoopCommand, IInfoCoopCommand
{
    public InfoCoopCommand()
        : base(
            "coop.debug.settlements",
            "info",
            "Shows the relevant state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            SettlementCommands.Info)
    {
    }
}

public interface ISetOwnerCoopCommand : ICoopCommand
{
}

public sealed class SetOwnerCoopCommand : LegacyCoopCommand, ISetOwnerCoopCommand
{
    public SetOwnerCoopCommand()
        : base(
            "coop.debug.settlement_component",
            "set_owner",
            "Sets owner for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementComponentId", "The settlement component id."),
                new ExpectedArgs("mobilePartyId", "The mobile party id."),
            },
            SettlementCommands.SetOwner)
    {
    }
}

public interface ICaptureBySiegeCoopCommand : ICoopCommand
{
}

public sealed class CaptureBySiegeCoopCommand : LegacyCoopCommand, ICaptureBySiegeCoopCommand
{
    public CaptureBySiegeCoopCommand()
        : base(
            "coop.debug.settlements",
            "capture_by_siege",
            "Captures by siege for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementNameOrId", "The exact settlement name or id; quote names containing spaces."),
                new ExpectedArgs("capturerHeroId", "The capturer hero id.", isRequired: false),
            },
            SettlementCommands.CaptureBySiege)
    {
    }
}

public interface IOwnerStateCoopCommand : ICoopCommand
{
}

public sealed class OwnerStateCoopCommand : LegacyCoopCommand, IOwnerStateCoopCommand
{
    public OwnerStateCoopCommand()
        : base(
            "coop.debug.settlements",
            "owner_state",
            "Shows state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementNameOrId", "The exact settlement name or id; quote names containing spaces."),
            },
            SettlementCommands.OwnerState)
    {
    }
}

public interface ISetGoldCoopCommand : ICoopCommand
{
}

public sealed class SetGoldCoopCommand : LegacyCoopCommand, ISetGoldCoopCommand
{
    public SetGoldCoopCommand()
        : base(
            "coop.debug.settlement_component",
            "set_gold",
            "Sets gold for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementComponentId", "The settlement component id."),
                new ExpectedArgs("gold", "The gold."),
            },
            SettlementCommands.SetGold)
    {
    }
}

public interface ISetIsOwnerUnassignedCoopCommand : ICoopCommand
{
}

public sealed class SetIsOwnerUnassignedCoopCommand : LegacyCoopCommand, ISetIsOwnerUnassignedCoopCommand
{
    public SetIsOwnerUnassignedCoopCommand()
        : base(
            "coop.debug.settlement_component",
            "set_is_owner_unassigned",
            "Sets is owner unassigned for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementComponentId", "The settlement component id."),
                new ExpectedArgs("value", "The value."),
            },
            SettlementCommands.SetIsOwnerUnassigned)
    {
    }
}

public interface ISetOwnerClanCoopCommand : ICoopCommand
{
}

public sealed class SetOwnerClanCoopCommand : LegacyCoopCommand, ISetOwnerClanCoopCommand
{
    public SetOwnerClanCoopCommand()
        : base(
            "coop.debug.settlements",
            "set_owner_clan",
            "Sets owner clan for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementNameOrId", "The exact settlement name or id; quote names containing spaces."),
                new ExpectedArgs("heroId", "The hero id."),
            },
            SettlementCommands.SetOwnerClan)
    {
    }
}
