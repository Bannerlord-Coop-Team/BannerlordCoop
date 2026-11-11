#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.ObjectManager;
using NavalDLC.CharacterDevelopment;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.Commands;

/// <summary>Stages real Boatswain damage reduction and reads the campaign XP and ship HP used by M6 validation.</summary>
public sealed class NavalBoatswainCoopCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;

    /// <summary>Uses the campaign registry to address a player's party on either machine.</summary>
    public NavalBoatswainCoopCommand(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public string Prefix => "coop.debug.naval";
    public string Name => "boatswain";
    public string Description => "Reads a party's effective First Mate XP and ship HP; prepare sets Boatswain to 100 and raises focus to 5 on the server before battle.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("partyId", "Registered party id from coop.debug.mobileparty.list.", true),
        new ExpectedArgs("prepare", "Optional: stage skill-based hull damage reduction before battle, server only.", false),
    };

    /// <summary>Prepares only on the campaign authority; reads never mutate XP or campaign ships.</summary>
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args == null || args.Count < 1 || args.Count > 2 || string.IsNullOrWhiteSpace(args[0]) ||
            (args.Count == 2 && args[1] != "prepare"))
            return Failed("Usage: coop.debug.naval.boatswain <partyId> [prepare]");

        bool prepare = args.Count == 2;
        if (prepare && ModInformation.IsClient) return Failed("Run prepare on the server before battle.");
        if (Campaign.Current == null) return Failed("No active campaign.");
        if (!objectManager.TryGetObject<MobileParty>(args[0], out var party)) return Failed("Party not found: " + args[0]);
        if (prepare && party.MapEvent != null) return Failed("Prepare before the party enters a battle.");

        var hero = party.GetEffectiveRoleHolder(PartyRole.FirstMate);
        if (hero == null) return Failed("The party has no effective First Mate.");

        var skill = NavalSkills.Boatswain;
        var developer = hero.HeroDeveloper;
        if (prepare)
        {
            // SkillHelper uses the effective First Mate's Boatswain skill, no ship upgrade is required.
            if (developer.GetFocus(skill) < 5) developer.AddFocus(skill, 5 - developer.GetFocus(skill), false);
            developer.SetInitialSkillLevel(skill, 100);
        }

        objectManager.TryGetId(hero, out var heroId);
        var output = new StringBuilder();
        output.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "NAVAL_BOATSWAIN party={0} hero={1} level={2} xp={3:R} learningRate={4:R} prepared={5}",
            args[0], heroId, hero.GetSkillValue(skill), developer.GetSkillXp(skill), developer.GetFocusFactor(skill), prepare));
        foreach (var ship in party.Ships)
        {
            objectManager.TryGetId(ship, out var shipId);
            float finalDamage = Campaign.Current.Models.CampaignShipDamageModel.GetShipDamage(ship, null, 100f);
            output.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "ship={0} hp={1:R} maxHp={2:R} sailHp={3:R} rawProbe=100 finalProbe={4:R} mitigatedProbe={5:R}",
                shipId, ship.HitPoints, ship.MaxHitPoints, ship.SailHitPoints, finalDamage, System.Math.Max(100f - finalDamage, 0f)));
        }
        return new CoopCommandResult(true, output.ToString());
    }

    /// <summary>Returns an actionable command failure without changing campaign state.</summary>
    private CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif