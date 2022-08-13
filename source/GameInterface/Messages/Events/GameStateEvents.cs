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

    public readonly struct MainMenuEvent { }

    public readonly struct CharacterCreationFinishedEvent
    {
        public ICharacterCreationData CharacterData { get; }

        public CharacterCreationFinishedEvent(ICharacterCreationData characterData)
        {
            CharacterData = characterData;
        }
    }
}
