using Common;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MapEvents.Commands;

/// <summary>Debug-only commands for driving and inspecting AI party conversations during live tests.</summary>
internal class ConversationDebugCommands
{
    [CommandLineArgumentFunction("start_nearest_ai", "coop.debug.conversation")]
    public static string StartNearestAi(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run coop.debug.conversation.start_nearest_ai on a client only";
        if (args.Count != 0)
            return "Usage: coop.debug.conversation.start_nearest_ai";

        var playerParty = MobileParty.MainParty;
        if (playerParty == null)
            return "The local player has no party";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return $"Unable to get {nameof(IObjectManager)}";

        var playerPosition = playerParty.Position.ToVec2();
        var target = MobileParty.All
            .Where(party => party.IsActive &&
                !party.IsPlayerParty() &&
                party.LeaderHero != null &&
                party.MapEvent == null &&
                party.CurrentSettlement == null &&
                party.BesiegerCamp == null &&
                party.Ai?.IsDisabled == false &&
                party.MemberRoster.TotalManCount > 0)
            .OrderBy(party => party.Position.ToVec2().DistanceSquared(playerPosition))
            .FirstOrDefault();
        if (target == null)
            return "No active AI lord party is available";
        if (!objectManager.TryGetId(target.Party, out var partyId))
            return "Unable to get the AI party id";

        EncounterManager.StartPartyEncounter(playerParty.Party, target.Party);
        return $"Started AI conversation with {target.StringId}; PartyId={partyId}";
    }

    [CommandLineArgumentFunction("state", "coop.debug.conversation")]
    public static string State(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run coop.debug.conversation.state on the server only";
        if (args.Count != 1)
            return "Usage: coop.debug.conversation.state <partyId>";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return $"Unable to get {nameof(IObjectManager)}";
        if (!objectManager.TryGetObjectWithLogging<PartyBase>(args[0], out var partyBase) ||
            partyBase.MobileParty == null)
            return $"Unable to resolve mobile party '{args[0]}'";

        var party = partyBase.MobileParty;
        return
            $"PartyId={args[0]}\n" +
            $"StringId={party.StringId}\n" +
            $"PositionX={party.Position.X.ToString("R", CultureInfo.InvariantCulture)}\n" +
            $"PositionY={party.Position.Y.ToString("R", CultureInfo.InvariantCulture)}\n" +
            $"MoveMode={party.PartyMoveMode}\n" +
            $"AiDisabled={party.Ai?.IsDisabled == true}\n" +
            $"DoNotMakeNewDecisions={party.Ai?.DoNotMakeNewDecisions == true}\n" +
            $"InConversation={ConversationPartyHold.IsInPlayerConversation(party)}";
    }

}
