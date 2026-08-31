using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.MobileParties.Commands;

public interface IInfoCoopCommand : ICoopCommand
{
}

public sealed class InfoCoopCommand : LegacyCoopCommand, IInfoCoopCommand
{
    public InfoCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "info",
            "Shows the relevant state for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("partyStringId", "The party string id."),
            },
            MobilePartyDebugCommand.Info)
    {
    }
}

public interface IComponentInfoCoopCommand : ICoopCommand
{
}

public sealed class ComponentInfoCoopCommand : LegacyCoopCommand, IComponentInfoCoopCommand
{
    public ComponentInfoCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "component_info",
            "Runs info for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("partyStringId", "The party string id."),
            },
            MobilePartyDebugCommand.ComponentInfo)
    {
    }
}

public interface IAttachmentIdsCoopCommand : ICoopCommand
{
}

public sealed class AttachmentIdsCoopCommand : LegacyCoopCommand, IAttachmentIdsCoopCommand
{
    public AttachmentIdsCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "attachment_ids",
            "Runs ids for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("partyStringId", "The party string id."),
            },
            MobilePartyDebugCommand.AttachmentIds)
    {
    }
}

public interface IVerifyAiAuthorityCoopCommand : ICoopCommand
{
}

public sealed class VerifyAiAuthorityCoopCommand : LegacyCoopCommand, IVerifyAiAuthorityCoopCommand
{
    public VerifyAiAuthorityCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "verify_ai_authority",
            "Verifies ai authority for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("mobilePartyId", "The mobile party id."),
            },
            MobilePartyDebugCommand.VerifyAiAuthority)
    {
    }
}

public interface ICreateNewPartyCoopCommand : ICoopCommand
{
}

public sealed class CreateNewPartyCoopCommand : LegacyCoopCommand, ICreateNewPartyCoopCommand
{
    public CreateNewPartyCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "create_party",
            "Creates party for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The hero id."),
                new ExpectedArgs("settlementId", "The settlement id."),
            },
            MobilePartyDebugCommand.CreateNewParty)
    {
    }
}

public interface ISpawnTestPartiesCoopCommand : ICoopCommand
{
}

public sealed class SpawnTestPartiesCoopCommand : LegacyCoopCommand, ISpawnTestPartiesCoopCommand
{
    public SpawnTestPartiesCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "spawn_test_parties",
            "Spawns test parties for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("count", "The count.", isRequired: false),
                new ExpectedArgs("settlementId", "The settlement id.", isRequired: false),
            },
            MobilePartyDebugCommand.SpawnTestParties)
    {
    }
}

public interface IDestroyPartyCoopCommand : ICoopCommand
{
}

public sealed class DestroyPartyCoopCommand : LegacyCoopCommand, IDestroyPartyCoopCommand
{
    public DestroyPartyCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "destroy_party",
            "Destroys party for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("mobilePartyId", "The mobile party id."),
            },
            MobilePartyDebugCommand.DestroyParty)
    {
    }
}

public interface IDestroyAllBanditPartiesCoopCommand : ICoopCommand
{
}

public sealed class DestroyAllBanditPartiesCoopCommand : LegacyCoopCommand, IDestroyAllBanditPartiesCoopCommand
{
    public DestroyAllBanditPartiesCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "destroy_all_bandit_parties",
            "Destroys all bandit parties for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            MobilePartyDebugCommand.DestroyAllBanditParties)
    {
    }
}

public interface IListMobilePartiesCoopCommand : ICoopCommand
{
}

public sealed class ListMobilePartiesCoopCommand : LegacyCoopCommand, IListMobilePartiesCoopCommand
{
    public ListMobilePartiesCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "list",
            "Lists the relevant state for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            MobilePartyDebugCommand.ListMobileParties)
    {
    }
}

public interface ISetWagePaymentLimitCoopCommand : ICoopCommand
{
}

public sealed class SetWagePaymentLimitCoopCommand : LegacyCoopCommand, ISetWagePaymentLimitCoopCommand
{
    public SetWagePaymentLimitCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "set_wage_limit_updated",
            "Sets wage limit updated for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("partyStringId", "The party string id."),
                new ExpectedArgs("value", "The value."),
            },
            MobilePartyDebugCommand.SetWagePaymentLimit)
    {
    }
}

public interface ISetUnlimitedWageToggleCoopCommand : ICoopCommand
{
}

public sealed class SetUnlimitedWageToggleCoopCommand : LegacyCoopCommand, ISetUnlimitedWageToggleCoopCommand
{
    public SetUnlimitedWageToggleCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "set_wage_unlimited",
            "Sets wage unlimited for co-op debugging.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("partyStringId", "The party string id."),
                new ExpectedArgs("value", "The value."),
            },
            MobilePartyDebugCommand.SetUnlimitedWageToggle)
    {
    }
}

public interface IAuditPartiesCoopCommand : ICoopCommand
{
}

public sealed class AuditPartiesCoopCommand : LegacyCoopCommand, IAuditPartiesCoopCommand
{
    public AuditPartiesCoopCommand()
        : base(
            "coop.debug.mobileparty",
            "audit",
            "Audits the relevant state for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            MobilePartyDebugCommand.AuditParties)
    {
    }
}
