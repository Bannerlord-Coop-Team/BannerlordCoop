#if DEBUG
using Common.Commands;
using GameInterface.Services.Armies.Patches;
using HarmonyLib;
using Newtonsoft.Json;
using SandBox.ViewModelCollection;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;

namespace GameInterface.Services.Armies.Commands;

public sealed class CaptainTooltipDebugCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.battle";
    public string Name => "captain_tooltip";
    public string Description => "Reads deployment captains and opens their production tooltip without input.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "observe, show or hide."),
        new ExpectedArgs("character_id", "Exact character id from observe; required for show and hide.", false)
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args.Count < 1 || args.Count > 2 ||
            (args[0] != "observe" && args[0] != "show" && args[0] != "hide") ||
            (args[0] != "observe" && args.Count != 2))
            return new CoopCommandResult(false, "Expected observe, or show/hide with a character id.", "command_failed");

        var view = Campaign.Current == null ? null : Mission.Current?.GetMissionBehavior<MissionGauntletOrderOfBattleUIHandler>();
        var data = view?._dataSource;
        if (ModInformation.IsServer || Campaign.Current == null || Mission.Current == null || Mission.Current.IsDeploymentFinished ||
            view?._isActive != true || !(data is SPOrderOfBattleVM))
            return new CoopCommandResult(false, "An active client campaign deployment view is required.", "command_failed");

        var items = data._allHeroes.Where(item => item.IsShown && item.Agent?.IsActive() == true &&
            item.Agent.Character is CharacterObject character && character.HeroObject != null).ToArray();
        var selected = args.Count == 2
            ? items.SingleOrDefault(item => item.Agent.Character.StringId == args[1]) : null;
        if (args.Count == 2 && selected == null)
            return new CoopCommandResult(false, "The named captain is not in the visible deployment roster.", "command_failed");

        if (args[0] == "show") selected.Tooltip.ExecuteBeginHint();
        if (args[0] == "hide") selected.Tooltip.ExecuteEndHint();
        var target = AccessTools.Method(typeof(SPOrderOfBattleVM), nameof(SPOrderOfBattleVM.GetAgentTooltip));
        var patch = AccessTools.Method(typeof(CaptainTooltipPatches), nameof(CaptainTooltipPatches.Transpiler));
        var patchCount = Harmony.GetPatchInfo(target)?.Transpilers.Count(entry => entry.PatchMethod == patch) ?? 0;
        var troop = Mission.Current.Agents.FirstOrDefault(agent => agent.IsActive() &&
            agent.Character is CharacterObject character && !character.IsHero);
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            action = args[0], selectedCharacterId = selected?.Agent.Character.StringId, patchCount,
            ordinaryTroopFound = troop != null, ordinaryTroopCharacterId = troop?.Character.StringId,
            ordinaryTroopHasHero = troop != null && CaptainTooltipPatches.GetCaptainHero(troop) != null,
            captains = items.Select(item =>
            {
                var character = (CharacterObject)item.Agent.Character;
                var hero = character.HeroObject;
                return new
                {
                    name = character.Name.ToString(), characterId = character.StringId, heroId = hero.StringId,
                    linkedHero = ReferenceEquals(hero, CaptainTooltipPatches.GetCaptainHero(item.Agent)),
                    item.IsLeadingAFormation, item.IsAssignedToAFormation,
                    skills = Skills.All.Select(skill => new
                    {
                        id = skill.StringId, name = skill.Name.ToString(),
                        characterValue = character.GetSkillValue(skill), heroValue = hero.GetSkillValue(skill)
                    }).ToArray(),
                    perks = PerkObject.All.Where(hero.GetPerkValue).Select(perk => new
                    {
                        id = perk.StringId, name = perk.Name.ToString(),
                        primaryRole = perk.PrimaryRole.ToString(), secondaryRole = perk.SecondaryRole.ToString()
                    }).ToArray(),
                    rows = item.GetCaptainTooltip().Select(row => new
                    {
                        row.DefinitionLabel, row.ValueLabel, row.OnlyShowWhenExtended,
                        row.OnlyShowWhenNotExtended, row.PropertyModifier
                    }).ToArray()
                };
            }).ToArray()
        }));
    }
}
#endif
