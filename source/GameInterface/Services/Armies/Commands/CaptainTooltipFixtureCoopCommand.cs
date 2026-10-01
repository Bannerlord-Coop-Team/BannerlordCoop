#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using Newtonsoft.Json;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Armies.Commands;

public sealed class CaptainTooltipFixtureCoopCommand : ICoopCommand
{
    private static Hero captain;
    private static MobileParty[] parties;
    private static string[] originalRosters;
    public string Prefix => "coop.debug.battle";
    public string Name => "captain_tooltip_fixture";
    public string Description => "Creates, transfers or removes the owned captain tooltip test hero.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "create, move or cleanup."),
        new ExpectedArgs("controller_id", "Target connected player for create or move.", false),
        new ExpectedArgs("second_controller_id", "Second connected player for create.", false)
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsClient || Campaign.Current == null)
            return Failed("An authoritative campaign is required.");
        if (args.Count == 1 && args[0] == "cleanup") return Cleanup();
        if (!((args.Count == 3 && args[0] == "create") || (args.Count == 2 && args[0] == "move")))
            return Failed("Expected create with two controllers, move with a controller, or cleanup.");
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var players) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objects) ||
            !players.TryGetPlayer(args[1], out var player) || !players.TryGetPeer(args[1], out _) ||
            !objects.TryGetObject<MobileParty>(player.MobilePartyId, out var target) ||
            target.MapEvent != null || target.CurrentSettlement != null || target.LeaderHero == null)
            return Failed("A connected player party outside battle and settlements is required.");
        if (args[0] == "create")
        {
            if (captain != null || parties != null) return Failed("The captain fixture is already active.");
            if (!players.TryGetPlayer(args[2], out var second) || !players.TryGetPeer(args[2], out _) ||
                !objects.TryGetObject<MobileParty>(second.MobilePartyId, out var other) || other == target ||
                other.MapEvent != null || other.CurrentSettlement != null)
                return Failed("Two different connected campaign parties are required.");
            var template = Hero.AllAliveHeroes.FirstOrDefault(hero => hero.IsWanderer && hero.CompanionOf == null);
            if (template == null) return Failed("A living wanderer template is required.");
            parties = new[] { target, other };
            originalRosters = parties.Select(Roster).ToArray();
            try
            {
                captain = HeroCreator.CreateSpecialHero(template.CharacterObject, target.LeaderHero.HomeSettlement, age: 30);
                var name = new TextObject("Issue 3286 Captain");
                captain.SetName(name, name);
                captain.SetNewOccupation(Occupation.Wanderer);
                captain.ChangeState(Hero.CharacterStates.Active);
                captain.HitPoints = 100;
                foreach (var skill in Skills.All) captain.SetSkillValue(skill, 150);
                foreach (var perk in PerkObject.All.Where(perk =>
                    perk.PrimaryRole == PartyRole.Captain || perk.SecondaryRole == PartyRole.Captain).Take(4))
                    captain.SetPerkValueInternal(perk, true);
                if (!objects.TryGetId(captain, out _) || !objects.TryGetId(captain.CharacterObject, out _) ||
                    captain.StringId == captain.CharacterObject.StringId)
                    throw new InvalidOperationException("The created captain must have registered, mismatched hero and character identities.");
                AddHeroToPartyAction.Apply(captain, target, false);
            }
            catch (Exception error)
            {
                var cleanup = Cleanup();
                return Failed(error.Message + " Cleanup: " + cleanup.Output);
            }
        }
        else
        {
            if (captain == null || !parties.Contains(target) || parties.Any(party => party.MapEvent != null))
                return Failed("Restore both battles before moving the owned captain.");
            if (captain.PartyBelongedTo != target) AddHeroToPartyAction.Apply(captain, target, false);
        }
        if (captain.PartyBelongedTo != target || target.MemberRoster.GetTroopCount(captain.CharacterObject) != 1)
            return Failed("The captain did not join the selected party.");
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            characterId = captain.CharacterObject.StringId, heroId = captain.StringId,
            name = captain.Name.ToString(), controllerId = args[1], partyId = target.StringId,
            perkCount = PerkObject.All.Count(captain.GetPerkValue)
        }));
    }

    private static CoopCommandResult Cleanup()
    {
        if (parties?.Any(party => party.MapEvent != null) == true)
            return Failed("Restore the battle before removing the captain.");
        if (captain != null)
        {
            var party = captain.PartyBelongedTo;
            if (party != null) party.MemberRoster.AddToCounts(captain.CharacterObject, -1);
            if (!captain.IsDead) KillCharacterAction.ApplyByRemove(captain, false, true);
        }
        bool restored = parties == null || parties.Select(Roster).SequenceEqual(originalRosters);
        if (!restored) return Failed("The original player rosters did not restore.");
        captain = null;
        parties = null;
        originalRosters = null;
        return new CoopCommandResult(true, "LIVE_TEST_JSON={\"fixtureActive\":false,\"restored\":true}");
    }

    private static string Roster(MobileParty party) => JsonConvert.SerializeObject(
        party.MemberRoster.GetTroopRoster().OrderBy(element => element.Character.StringId).Select(element => new
        { id = element.Character.StringId, element.Number, element.WoundedNumber, element.Xp }));

    private static CoopCommandResult Failed(string reason) => new CoopCommandResult(false, reason, "command_failed");
}
#endif
