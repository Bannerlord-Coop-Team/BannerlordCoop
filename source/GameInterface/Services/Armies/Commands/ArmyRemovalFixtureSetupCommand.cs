#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using Newtonsoft.Json;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Commands;

public sealed class ArmyRemovalFixtureSetupCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;
    private readonly ITimeControlInterface timeControl;

    public ArmyRemovalFixtureSetupCommand(IObjectManager objectManager, ITimeControlInterface timeControl)
    {
        this.objectManager = objectManager;
        this.timeControl = timeControl;
    }

    public string Prefix => "coop.debug.army";
    public string Name => "removal_fixture_setup";
    public string Description => "Stages two clean allied lord parties for army removal in a disposable paused campaign.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("settlementId", "A registered settlement, such as town_ES1 (Danustica)."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsClient || args.Count != 1 || timeControl.GetTimeControl() != TimeControlEnum.Pause)
            return new CoopCommandResult(false, "Run on the paused server with one settlement id.", "command_failed");
        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement) || settlement.MapFaction is not Kingdom kingdom)
            return new CoopCommandResult(false, "The settlement must resolve to a kingdom.", "command_failed");
        var parties = MobileParty.AllLordParties.Where(p => p.IsActive && p.MapFaction == kingdom
            && p.LeaderHero != null && !p.IsPlayerParty() && p.Army == null && p.AttachedTo == null
            && p.AttachedParties.Count == 0 && p.MapEvent == null && p.CurrentSettlement == null
            && p.BesiegerCamp == null && objectManager.TryGetId(p, out _))
            .OrderBy(p => p.StringId, StringComparer.Ordinal).Take(2).ToArray();
        if (parties.Length != 2)
            return new CoopCommandResult(false, "Need two clean registered allied lord parties. No state was changed.", "command_failed");

        // Setup uses vanilla with patches live so the fixture objects retain network identities.
        kingdom.CreateArmy(parties[0].LeaderHero, settlement, Army.ArmyTypes.Defender);
        var army = parties[0].Army;
        if (army == null || !objectManager.TryGetId(army, out var armyId))
            return new CoopCommandResult(false, "Fixture army was not registered; discard this test campaign.", "command_failed");
        parties[1].Army = army;
        army.AddPartyToMergedParties(parties[1]);
        objectManager.TryGetId(parties[0], out var leaderId);
        objectManager.TryGetId(parties[1], out var memberId);
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            armyId, armyName = army.Name.ToString(), leaderId, memberId, leaderName = parties[0].Name.ToString(), memberName = parties[1].Name.ToString(),
            leaderHeroName = parties[0].LeaderHero.Name.ToString(), memberHeroName = parties[1].LeaderHero.Name.ToString(),
        }));
    }
}

public sealed class ArmyRemovalFixtureStateCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;

    public ArmyRemovalFixtureStateCommand(IObjectManager objectManager) => this.objectManager = objectManager;

    public string Prefix => "coop.debug.army";
    public string Name => "removal_fixture_state";
    public string Description => "Inspects exact army membership and attachment identities without changing state.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("armyId", "The original registered army id, also after destruction."),
        new ExpectedArgs("leaderId", "The registered leader party id."),
        new ExpectedArgs("memberId", "The registered member party id."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args.Count != 3)
            return new CoopCommandResult(false, "Expected army, leader and member ids.", "command_failed");
        objectManager.TryGetObject<Army>(args[0], out var army);
        var ids = new[] { args[1], args[2] };
        var parties = ids.Select(id =>
        {
            objectManager.TryGetObject<MobileParty>(id, out var party);
            return new
            {
                partyId = id, exists = party != null, partyStringId = party?.StringId,
                armyId = GetId(party?.Army), attachedToId = GetId(party?.AttachedTo),
                attachedPartyIds = party?.AttachedParties.Select(p => GetId(p)).ToArray() ?? Array.Empty<string>(),
            };
        }).ToArray();
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            authoritative = ModInformation.IsServer, armyExists = army != null,
            leaderId = GetId(army?.LeaderParty), armyPartyIds = army?.Parties.Select(p => GetId(p)).ToArray() ?? Array.Empty<string>(),
            parties,
        }));
    }

    private string GetId(object obj) => obj == null ? null : objectManager.TryGetId(obj, out var id) ? id : "unregistered";
}
#endif
