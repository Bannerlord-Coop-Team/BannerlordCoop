#if DEBUG
using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Commands;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.Villages.Commands;
using Serilog;
using System.Collections.Generic;

namespace GameInterface.Services.MapEvents.Handlers;

internal sealed class BattleLiveTestActionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleLiveTestActionHandler>();

    private readonly IMessageBroker messageBroker;

    public BattleLiveTestActionHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<NetworkBattleLiveTestAction>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleLiveTestAction>(Handle);
    }

    private void Handle(MessagePayload<NetworkBattleLiveTestAction> payload)
    {
        if (ModInformation.IsServer)
            return;

        var action = payload.What.Action;
        GameThread.RunSafe(() =>
        {
            string result;
            switch (action)
            {
                case BattleLiveTestAction.StartAttackMission:
                    result = MapEventDebugCommands.StartAttackMission(new List<string>());
                    break;
                case BattleLiveTestAction.FinishDeployment:
                    result = BattleTeamKillCommands.FinishDeployment(new List<string>());
                    break;
                case BattleLiveTestAction.WoundPlayer:
                    result = KillPlayerAgentCommands.KillPlayerAgent(new List<string>());
                    break;
                case BattleLiveTestAction.KillEnemyTeam:
                    result = BattleTeamKillCommands.KillEnemyTeam(new List<string>());
                    break;
                case BattleLiveTestAction.LeaveBattle:
                    result = BattleTeamKillCommands.LeaveBattle(new List<string>());
                    break;
                case BattleLiveTestAction.FinishEncounter:
                    result = MapEventDebugCommands.FinishCurrentEncounter(new List<string>());
                    break;
                default:
                    Logger.Error("[LiveTest] Unknown battle live-test action {Action}", action);
                    return;
            }

            Logger.Information("[LiveTest] Applied battle action {Action}: {Result}", action, result);
        }, context: nameof(BattleLiveTestActionHandler));
    }
}
#endif
