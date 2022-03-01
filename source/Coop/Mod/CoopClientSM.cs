using Common;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod
{
    using StateConfiguration = StateMachine<ECoopClientState, ECoopClientTrigger>.StateConfiguration;
    public enum ECoopClientState
    {
        /// <summary>
        /// Client is at main menu.
        /// </summary>
        MainManu,

        /// <summary>
        /// Client is creating character.
        /// </summary>
        CharacterCreation,

        /// <summary>
        /// Client is receiving world data.
        /// </summary>
        ReceivingGameData,

        /// <summary>
        /// Client is loading.
        /// </summary>
        Loading,

        /// <summary>
        /// Client is playing
        /// </summary>
        Playing,
    }
    enum ECoopClientTrigger
    {
        /// <summary>
        /// Client is required to create character.
        /// </summary>
        RequiresCharacterCreation,

        /// <summary>
        /// Client has existing character on host.
        /// </summary>
        IsServer,

        /// <summary>
        /// Client has existing character on host.
        /// </summary>
        CharacterExists,

        /// <summary>
        /// A new character has been created.
        /// </summary>
        CharacterCreated,

        /// <summary>
        /// World data has been received.
        /// </summary>
        GameDataReceived,

        /// <summary>
        /// The game has been loaded for the client.
        /// </summary>
        GameLoaded,
    }

    /// <summary>
    /// Defines state machine used by CoopClient
    /// </summary>
    internal class CoopClientSM : CoopStateMachine<ECoopClientState, ECoopClientTrigger>
    {
        public readonly StateConfiguration MainMenuState;
        public readonly StateConfiguration CharacterCreationState;
        public readonly StateConfiguration ReceivingGameDataState;
        public readonly StateConfiguration PlayingState;
        public CoopClientSM() : base(ECoopClientState.MainManu)
        {
            // Client at Main Menu
            MainMenuState = StateMachine.Configure(ECoopClientState.MainManu);
            MainMenuState.Permit(ECoopClientTrigger.RequiresCharacterCreation, ECoopClientState.CharacterCreation);
            MainMenuState.Permit(ECoopClientTrigger.IsServer, ECoopClientState.Playing);

            // Client creating character
            CharacterCreationState = StateMachine.Configure(ECoopClientState.CharacterCreation);
            CharacterCreationState.Permit(ECoopClientTrigger.CharacterCreated, ECoopClientState.ReceivingGameData);

            // Client receiving world data
            ReceivingGameDataState = StateMachine.Configure(ECoopClientState.ReceivingGameData);
            ReceivingGameDataState.Permit(ECoopClientTrigger.GameDataReceived, ECoopClientState.Playing);

            // Client playing
            PlayingState = StateMachine.Configure(ECoopClientState.Playing);
        }
    }
}
