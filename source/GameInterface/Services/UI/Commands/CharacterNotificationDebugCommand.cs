#if DEBUG
using Common;
using Common.Commands;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Helpers;

namespace GameInterface.Services.UI.Commands;

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
}
#endif
