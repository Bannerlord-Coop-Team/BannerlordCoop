using Common.Messages;
using GameInterface.Data;
using System;

namespace GameInterface.Messages.Commands
{
    /// <summary>
    /// Goes to the main menu from any game state.
    /// </summary>
    public readonly struct EnterMainMenuCommand
    {
        public EnterMainMenuCommand(Guid id, TimeSpan timeOut)
        {
            Id = id;
            TimeOut = timeOut;
        }

        public Guid Id { get; }
        public TimeSpan TimeOut { get; }
    }

    /// <summary>
    /// Reply to <seealso cref="EnterMainMenuCommand"/>.
    /// </summary>
    public readonly struct EnteredMainMenuResponse : IResponse
    {
        public bool Success { get; }
        public EnteredMainMenuResponse(bool success)
        {
            Success = success;
        }
    }

    /// <summary>
    /// Loads a give save from any game state
    /// </summary>
    public readonly struct LoadSaveCommand
    {
        public IGameSaveData SaveData { get; }
    }

    /// <summary>
    /// Reply to <seealso cref="LoadSaveCommand"/>.
    /// </summary>
    public readonly struct LoadSaveReply : IResponse
    {
        public bool Success { get; }

        public LoadSaveReply(bool success)
        {
            Success = success;
        }
    }

    /// <summary>
    /// Goes to character creation from any game state
    /// </summary>
    public readonly struct StartCreateCharacterCommand
    {
    }

    /// <summary>
    /// Reply to <seealso cref="StartCreateCharacterCommand"/>
    /// </summary>
    public readonly struct StartCreateCharacterReply : IResponse
    {
        public bool Success { get; }

        public StartCreateCharacterReply(bool success)
        {
            Success = success;
        }
    }
}
