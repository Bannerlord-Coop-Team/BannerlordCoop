using Common.Commands;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Utils.Commands;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.MobilePartyAIs.Commands;

/// <summary>
/// Reads server party AI decisions so live tests can check the AI reactions to player parties.
/// </summary>
internal static class MobilePartyAiDebugCommands
{
    // Builds a successful command result.
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    // Builds a failed command result.
    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    /// <summary>
    /// Reports whether the server AI may pick a settlement for a military action, the check that skips a
    /// settlement whose last attacker the party is barred from attacking.
    /// </summary>
    public sealed class MilitaryTargetSuitableCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.mobile_party";

        public string Name => "military_target_suitable";

        public string Description => "Reports whether an AI party may pick a settlement for a military action.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("party_id", "AI mobile party name or id; quote names with spaces."),
            new ExpectedArgs("settlement_id", "Settlement name or id; quote names with spaces."),
            new ExpectedArgs("mission_type", "Besieger, Raider, Defender or Patrolling."),
        };

        private readonly ICommandObjectResolver resolver;

        // Takes the resolver that turns command arguments into game objects.
        public MilitaryTargetSuitableCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        // Runs the live AiMilitaryBehavior settlement check, patches included, and prints what it read.
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!resolver.TryResolveMobileParty(args[0], out var party, out var error)) return Failed(error);
            if (!resolver.TryResolveSettlement(args[1], out var settlement, out error)) return Failed(error);
            if (!Enum.TryParse(args[2], ignoreCase: true, out Army.ArmyTypes missionType)) return Failed($"Unknown mission type {args[2]}.");

            var behavior = Campaign.Current.GetCampaignBehavior<AiMilitaryBehavior>();
            if (behavior == null) return Failed("AiMilitaryBehavior is not registered on this campaign.");

            var lastAttacker = settlement.LastAttackerParty;
            bool attackPrevented = DefaultMobilePartyAIModelPatches.IsAttackPrevented(party, lastAttacker);
            bool suitable = behavior.CheckIfSettlementIsSuitableForMilitaryAction(settlement, party, missionType, isCalculatingForNewArmyCreation: false);
            return Succeeded($"MILITARY_TARGET party={resolver.DisplayId(party)} settlement={settlement.StringId} " +
                $"missionType={missionType} lastAttacker={lastAttacker?.StringId ?? "none"} " +
                $"attackPrevented={attackPrevented} suitable={suitable}");
        }
    }
}
