#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Utils.Commands;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Commands;

public sealed class ArmyRemovalFixtureSetupCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;
    private readonly ITimeControlInterface timeControl;
    private readonly ICommandObjectResolver resolver;

    public ArmyRemovalFixtureSetupCommand(IObjectManager objectManager, ITimeControlInterface timeControl, ICommandObjectResolver resolver)
    {
        this.objectManager = objectManager;
        this.timeControl = timeControl;
        this.resolver = resolver;
    }

    public string Prefix => "coop.debug.army";
    public string Name => "removal_fixture_setup";
    public string Description => "Stages two clean allied lord parties for army removal in a disposable paused campaign.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("settlementId", "Settlement name or id, such as town_ES1 or Danustica; quote names with spaces."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsClient || args.Count != 1 || timeControl.GetTimeControl() != TimeControlEnum.Pause)
            return new CoopCommandResult(false, "Run on the paused server with one settlement name or id.", "command_failed");
        if (!resolver.TryResolveSettlement(args[0], out var settlement, out var error))
            return new CoopCommandResult(false, error, "command_failed");
        if (settlement.MapFaction is not Kingdom kingdom)
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
    private readonly ICommandObjectResolver resolver;

    public ArmyRemovalFixtureStateCommand(IObjectManager objectManager, ICommandObjectResolver resolver)
    {
        this.objectManager = objectManager;
        this.resolver = resolver;
    }

    public string Prefix => "coop.debug.army";
    public string Name => "removal_fixture_state";
    public string Description => "Inspects exact army membership and attachment identities without changing state.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("armyId", "The original registered army id, also after destruction."),
        new ExpectedArgs("leaderId", "Leader party name or id; quote names with spaces."),
        new ExpectedArgs("memberId", "Member party name or id; quote names with spaces."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args.Count != 3)
            return new CoopCommandResult(false, "Expected army, leader and member ids.", "command_failed");
        objectManager.TryGetObject<Army>(args[0], out var army);
        var ids = new[] { args[1], args[2] };
        var resolvedParties = new List<MobileParty>();
        foreach (var id in ids)
        {
            var resolved = resolver.TryResolveMobileParty(id, out var party, out var resolveError);
            if (!resolved && resolver.IsAmbiguous(resolveError))
                return new CoopCommandResult(false, resolveError, "command_failed");
            resolvedParties.Add(party);
        }
        var parties = ids.Select((id, index) =>
        {
            var party = resolvedParties[index];
            // Echo the canonical registry id when the argument was a name, the raw argument when nothing resolved.
            if (party == null || !objectManager.TryGetId(party, out var canonicalId))
                canonicalId = id;
            return new
            {
                partyId = canonicalId, exists = party != null, partyStringId = party?.StringId,
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
