using GameInterface.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Messages.Events
{
    public enum EGameState
    {
        MainMenu,
        CharacterCreation,
        LoadingMainMenu,
        LoadingSave,
        Map,
        Mission,
    }

    public readonly struct GameStateChangedEvent
    {
        public EGameState PreviousState { get; }
        public EGameState NewState { get; }

        public GameStateChangedEvent(EGameState previousState, EGameState newState)
        {
            PreviousState = previousState;
            NewState = newState;
        }
    }

    public readonly struct CharacterCreationFinishedEvent
    {
        public ICharacterCreationData CharacterData { get; }

        public CharacterCreationFinishedEvent(ICharacterCreationData characterData)
        {
            CharacterData = characterData;
        }
    }
}
