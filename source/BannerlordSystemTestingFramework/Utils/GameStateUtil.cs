using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BannerlordSystemTestingLibrary.Utils
{

    #region StateMachine
    using StateConfiguration = StateMachine<EGameState, EGameTrigger>.StateConfiguration;
    public enum EGameState
    {
        Starting,
        Initial,
        BannerEditor,
        Barber,
        CharacterDeveloper,
        Clan,
        Crafting,
        Editor,
        GameLoading,
        Kingdom,
        LobbyGame,
        Lobby,
        Map,
        Mission,
        PlayerGame,
        Quests,
        Tutorial,
        UnspecifiedDedicatedServer,
        VideoPlayback,
    }
    enum EGameTrigger
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

    /// <summary>
    /// Defines state machine used by CoopClient
    /// </summary>
    //internal class CoopClientSM : CoopStateMachine<ECoopClientState, ECoopClientTrigger>
    //{
    //    public readonly StateConfiguration MainMenuState;
    //    public readonly StateConfiguration CharacterCreationState;
    //    public readonly StateConfiguration ReceivingWorldDataState;
    //    public readonly StateConfiguration LoadingState;
    //    public readonly StateConfiguration PlayingState;
    //    public CoopClientSM() : base(EGameState.Starting)
    //    {
    //        // Client at Main Menu
    //        MainMenuState = StateMachine.Configure(ECoopClientState.MainManu);
    //        MainMenuState.Permit(ECoopClientTrigger.RequiresCharacterCreation, ECoopClientState.CharacterCreation);
    //        MainMenuState.Permit(ECoopClientTrigger.CharacterExists, ECoopClientState.ReceivingWorldData);

    //        // Client creating character
    //        CharacterCreationState = StateMachine.Configure(ECoopClientState.CharacterCreation);
    //        CharacterCreationState.Permit(ECoopClientTrigger.CharacterCreated, ECoopClientState.ReceivingWorldData);

    //        // Client receiving world data
    //        ReceivingWorldDataState = StateMachine.Configure(ECoopClientState.ReceivingWorldData);
    //        ReceivingWorldDataState.Permit(ECoopClientTrigger.WorldDataReceived, ECoopClientState.Loading);

    //        // Client loading
    //        LoadingState = StateMachine.Configure(ECoopClientState.Loading);
    //        LoadingState.Permit(ECoopClientTrigger.GameLoaded, ECoopClientState.Playing);

    //        // Client playing
    //        PlayingState = StateMachine.Configure(ECoopClientState.Playing);
    //    }
    //}
    #endregion

    /// <summary>
    /// Utils used while in specific game states
    /// </summary>
    public static class GameStateUtil
    {
        /// <summary>
        /// Initial state utils
        /// </summary>
        /// <remarks>
        /// The initial state consists of the initial loading and main menu.
        /// </remarks>
        public static class InitialState
        {
            public static TimeSpan StartupTimeout = TimeSpan.FromSeconds(120);
            public static TimeSpan StartupPollRate = TimeSpan.FromMilliseconds(500);
            public static void WaitForMenuReady()
            {
                // Get event through websocket
            }

            /// <summary>
            /// Starts a completely new game
            /// </summary>
            public static void StartNewGame()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Continues last saved game
            /// </summary>
            public static void ContinueGame()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Loads existing game
            /// </summary>
            /// <param name="saveName">Name of existing save</param>
            public static void LoadGame(string saveName)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Exits the game from main menu
            /// </summary>
            public static void QuitGame()
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Character creation state utils
        /// </summary>
        public static class CharacterCreationState
        {
            /// <summary>
            /// Creates a random character
            /// </summary>
            public static void GenerateRandomCharacter()
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Tutorial state utils
        /// </summary>
        /// <remarks>
        /// The tutorial state state consists of the training field and first mission.
        /// </remarks>
        public static class TutorialState
        {
            /// <summary>
            /// Skips the tutorial
            /// </summary>
            public static void ExitTutorial()
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Map state utils
        /// </summary>
        /// <remarks>
        /// The map state consists of the overworld map.
        /// </remarks>
        public static class MapState
        {
            /// <summary>
            /// 
            /// </summary>
            public static void SaveGame()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// 
            /// </summary>
            public static void ExitGame()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// 
            /// </summary>
            public static void SaveAndExitGame()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// 
            /// </summary>
            public static void GoToNearestTown()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// 
            /// </summary>
            public static void GoToNearestParty()
            {
                throw new NotImplementedException();
            }
        }

    }
}
