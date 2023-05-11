using Common.Messaging;
using System;

namespace GameInterface.Services.GameState.Messages;

/// <summary>
/// Goes to the mission state from any game state.
/// </summary>
public readonly struct EnterMissionState : ICommand
{
    public Guid TransactionID { get; }

    public EnterMissionState(Guid transactionID)
    {
        TransactionID = transactionID;
    }
}

/// <summary>
/// Reply to <seealso cref="EnterMainMenu"/>.
/// </summary>
public readonly struct MissionStateEntered : IResponse
{
    public Guid TransactionID { get; }

    public MissionStateEntered(Guid transactionID)
    {
        TransactionID = transactionID;
    }
}
