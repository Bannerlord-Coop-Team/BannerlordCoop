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
        ReceivingWorldData,

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
        CharacterExists,

        /// <summary>
        /// A new character has been created.
        /// </summary>
        CharacterCreated,

        /// <summary>
        /// World data has been received.
        /// </summary>
        WorldDataReceived,

        /// <summary>
        /// The game has been loaded for the client.
        /// </summary>
        GameLoaded,
    }
    internal class CoopClientSM : CoopStateMachine<ECoopClientState, ECoopClientTrigger>
    {
        public readonly StateConfiguration MainMenuState;
        public readonly StateConfiguration CharacterCreationState;
        public readonly StateConfiguration ReceivingWorldDataState;
        public readonly StateConfiguration LoadingState;
        public readonly StateConfiguration PlayingState;
        public CoopClientSM() : base(ECoopClientState.MainManu)
        {
            MainMenuState = StateMachine.Configure(ECoopClientState.MainManu);
            MainMenuState.Permit(ECoopClientTrigger.RequiresCharacterCreation, ECoopClientState.CharacterCreation);
            MainMenuState.Permit(ECoopClientTrigger.CharacterExists, ECoopClientState.ReceivingWorldData);

            CharacterCreationState = StateMachine.Configure(ECoopClientState.CharacterCreation);
            CharacterCreationState.Permit(ECoopClientTrigger.CharacterCreated, ECoopClientState.ReceivingWorldData);

            ReceivingWorldDataState = StateMachine.Configure(ECoopClientState.ReceivingWorldData);
            ReceivingWorldDataState.Permit(ECoopClientTrigger.WorldDataReceived, ECoopClientState.Loading);

            LoadingState = StateMachine.Configure(ECoopClientState.Loading);
            LoadingState.Permit(ECoopClientTrigger.GameLoaded, ECoopClientState.Playing);

            PlayingState = StateMachine.Configure(ECoopClientState.Playing);
        }
    }
}
