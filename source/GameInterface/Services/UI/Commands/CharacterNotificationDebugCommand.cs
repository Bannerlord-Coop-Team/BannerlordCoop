#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.Clans;
using GameInterface.Services.Clans.Data;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Helpers;

namespace GameInterface.Services.UI.Commands;

/// <summary>DEBUG-only character-alert setup and observations for disposable live-test campaigns.</summary>
internal class CharacterNotificationDebugCommand
{
    public sealed class CharacterNotificationInspectCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.character_notification";
        public string Name => "inspect";
        public string Description => "Reads the local hero and character-tab notification.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer || Campaign.Current == null)
                return new CoopCommandResult(false, "Requires a client campaign.", "command_failed");
            var tracker = Campaign.Current.GetCampaignBehavior<ViewDataTrackerCampaignBehavior>();
            var hero = Hero.MainHero;
            if (tracker == null || hero == null)
                return new CoopCommandResult(false, "Character notification state unavailable.", "command_failed");
            return new CoopCommandResult(true,
                $"hero={hero.StringId} name={hero.Name} clan={hero.Clan.StringId} level={hero.Level} " +
                $"focus={hero.HeroDeveloper.UnspentFocusPoints} attributes={hero.HeroDeveloper.UnspentAttributePoints} " +
                $"perks={PerkHelper.AvailablePerkCountOfHero(hero)} alert={tracker.IsCharacterNotificationActive}");
        }
    }
    /// <summary>Uses production clan joining and development actions to prepare two real players.</summary>
    public sealed class CharacterNotificationPrepareCoopCommand : ICoopCommand
    {
        private readonly IObjectManager objects;
        private readonly IPlayerManager players;
        private readonly IClanJoinRules clanJoin;

        public CharacterNotificationPrepareCoopCommand(IObjectManager objects, IPlayerManager players, IClanJoinRules clanJoin)
        {
            this.objects = objects;
            this.players = players;
            this.clanJoin = clanJoin;
        }

        public string Prefix => "coop.debug.character_notification";
        public string Name => "prepare";
        public string Description => "Prepares registered actor and observer in a disposable campaign.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("actor_hero_id", "Actor registry id."),
            new ExpectedArgs("observer_hero_id", "Observer registry id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient || Campaign.Current == null)
                return Failed("Requires a server campaign.");
            if (!objects.TryGetObjectWithLogging<Hero>(args[0], out var actor) ||
                !objects.TryGetObjectWithLogging<Hero>(args[1], out var observer))
                return Failed("Registered heroes unavailable.");
            if (actor == observer || !players.Contains(actor) || !players.Contains(observer) ||
                actor.Clan == null || observer.Clan == null)
                return Failed("Requires two distinct registered players with clans.");
            if (actor.Clan != observer.Clan)
            {
                var reason = clanJoin.GetUnavailableReason(observer, actor);
                if (reason != ClanJoinUnavailableReason.None) return Failed($"Clan join unavailable: {reason}");
                clanJoin.Apply(observer, actor.Clan);
            }
            foreach (var hero in new[] { actor, observer })
            {
                var developer = hero.HeroDeveloper;
                if (developer.UnspentFocusPoints > 1000 || developer.UnspentAttributePoints > 1000)
                    return Failed("Unexpected pending point count.");
                while (developer.UnspentFocusPoints > 0)
                {
                    var skill = Skills.All.FirstOrDefault(developer.CanAddFocusToSkill);
                    if (skill == null) return Failed("No skill can accept pending focus points.");
                    int before = developer.UnspentFocusPoints;
                    developer.AddFocus(skill, 1);
                    if (developer.UnspentFocusPoints >= before) return Failed("Focus spending made no progress.");
                }
                while (developer.UnspentAttributePoints > 0)
                {
                    var attribute = Attributes.All.FirstOrDefault(value =>
                        hero.GetAttributeValue(value) < Campaign.Current.Models.CharacterDevelopmentModel.MaxAttribute);
                    if (attribute == null) return Failed("No attribute can accept pending points.");
                    int before = developer.UnspentAttributePoints;
                    developer.AddAttribute(attribute, 1);
                    if (developer.UnspentAttributePoints >= before) return Failed("Attribute spending made no progress.");
                }
                foreach (var perk in PerkObject.All)
                {
                    if (hero.GetSkillValue(perk.Skill) >= perk.RequiredSkillValue && !hero.GetPerkValue(perk) &&
                        (perk.AlternativePerk == null || !hero.GetPerkValue(perk.AlternativePerk)))
                        developer.AddPerk(perk);
                }
                if (PerkHelper.AvailablePerkCountOfHero(hero) != 0)
                    return Failed("Pending perks remain after preparation.");
            }
            return new CoopCommandResult(true, $"Prepared actor={actor.StringId} observer={observer.StringId} clan={actor.Clan.StringId}; pending upgrades spent.");
        }
    }

    /// <summary>Drives real skill experience and notification events on the authoritative server.</summary>
    public sealed class CharacterNotificationAdvanceCoopCommand : ICoopCommand
    {
        private readonly IObjectManager objects;
        private readonly IPlayerManager players;

        public CharacterNotificationAdvanceCoopCommand(IObjectManager objects, IPlayerManager players)
        {
            this.objects = objects;
            this.players = players;
        }

        public string Prefix => "coop.debug.character_notification";
        public string Name => "advance";
        public string Description => "Advances a registered player's skill through production XP processing.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[] { new ExpectedArgs("actor_hero_id", "Actor registry id.") };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient || Campaign.Current == null)
                return Failed("Requires a server campaign.");
            if (!objects.TryGetObjectWithLogging<Hero>(args[0], out var hero) || !players.Contains(hero))
                return Failed("Requires a registered player hero.");
            var perk = PerkObject.All.Where(value => value.RequiredSkillValue > hero.GetSkillValue(value.Skill) &&
                !hero.GetPerkValue(value) && (value.AlternativePerk == null || !hero.GetPerkValue(value.AlternativePerk)))
                .OrderBy(value => value.RequiredSkillValue - hero.GetSkillValue(value.Skill)).FirstOrDefault();
            if (perk == null) return Failed("No unearned perk threshold available.");
            int previousLevel = hero.Level;
            int previousSkill = hero.GetSkillValue(perk.Skill);
            int target = Math.Min(400, Math.Max((int)Math.Ceiling(perk.RequiredSkillValue), previousSkill + 100));
            hero.HeroDeveloper.ChangeSkillLevel(perk.Skill, target - previousSkill, true);
            return new CoopCommandResult(true, $"Advanced hero={hero.StringId} skill={perk.Skill.StringId} before={previousSkill} after={hero.GetSkillValue(perk.Skill)} levelBefore={previousLevel} levelAfter={hero.Level} perks={PerkHelper.AvailablePerkCountOfHero(hero)}");
        }
    }

    /// <summary>Dismisses the local badge using the same tracker action as the character view.</summary>
    public sealed class CharacterNotificationDismissCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.character_notification";
        public string Name => "dismiss";
        public string Description => "Dismisses the local character badge for baseline preparation.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer || Campaign.Current == null)
                return Failed("Requires a client campaign.");
            var tracker = Campaign.Current.GetCampaignBehavior<ViewDataTrackerCampaignBehavior>();
            if (tracker == null) return Failed("Character notification state unavailable.");
            tracker.ClearCharacterNotification();
            return new CoopCommandResult(true, "Dismissed local character notification through the production tracker action.");
        }
    }

    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");

}
#endif
