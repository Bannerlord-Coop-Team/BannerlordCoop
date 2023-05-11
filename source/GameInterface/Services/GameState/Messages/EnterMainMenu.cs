using Common.Messaging;
using System;

namespace GameInterface.Services.GameState.Messages;

/// <summary>
/// Goes to the main menu from any game state.
/// </summary>
public readonly struct EnterMainMenu : ICommand
{
    public Guid TransactionID { get; }

    public EnterMainMenu(Guid transactionID)
    {
        TransactionID = transactionID;
    }
}

/// <summary>
/// Reply to <seealso cref="EnterMainMenu"/>.
/// </summary>
public readonly struct MainMenuEntered : IResponse
{
    public Guid TransactionID { get; }

    public MainMenuEntered(Guid transactionID)
    {
        TransactionID = transactionID;
    }
}
