using System;
using System.Linq;
using SandBox;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ServerHeadless
{
    /// <summary>
    /// Drives a NEW campaign's startup headless. A fresh sandbox campaign normally plays an intro
    /// video and then walks the player through the interactive character-creation stages before
    /// the world finishes generating; with no UI those states would sit idle forever. Each
    /// load-loop tick <see cref="AdvanceSetupStep"/> advances whatever setup state is active —
    /// the video is skipped and every character-creation stage is completed with defaults — until
    /// the campaign reaches the map. (GameInterface has a debug skip for the same stages, but it
    /// drives the Gauntlet views, which do not exist headless; this works on the
    /// CharacterCreationManager directly.)
    /// </summary>
    internal static class HeadlessNewGame
    {
        /// <summary>Name for the host's hero; any culture works, empire spawns centrally.</summary>
        private const string HostHeroName = "Host";
        private const string DefaultCultureId = "empire";

        /// <summary>True once the new campaign has reached the map — setup is finished.</summary>
        public static bool IsOnMap => GameStateManager.Current?.ActiveState is MapState;

        /// <summary>Advances the active new-game setup state by one step. Game-thread only.</summary>
        public static void AdvanceSetupStep()
        {
            switch (GameStateManager.Current?.ActiveState)
            {
                case VideoPlaybackState _:
                    // The campaign intro video; headless there is no player to watch it.
                    Console.WriteLine("[ServerHeadless] New game: skipping intro video...");
                    ((SandBoxGameManager)Game.Current.GameManager).LaunchSandboxCharacterCreation();
                    break;

                case CharacterCreationState characterCreation:
                    AdvanceCharacterCreation(characterCreation._characterCreationManager);
                    break;
            }
        }

        /// <summary>
        /// Completes the current character-creation stage with defaults, one stage per call.
        /// Only the culture and clan-naming stages need data; every other stage (face generator,
        /// narrative choices, banner, review, options) accepts its defaults, and NextStage on the
        /// final stage finalizes the character and pushes MapState.
        /// </summary>
        private static void AdvanceCharacterCreation(CharacterCreationManager manager)
        {
            switch (manager.CurrentStage)
            {
                case CharacterCreationCultureStage _:
                    var cultures = manager.CharacterCreationContent.GetCultures();
                    var culture = cultures.FirstOrDefault(c => c.StringId == DefaultCultureId) ?? cultures.First();
                    manager.CharacterCreationContent.SetSelectedCulture(culture, manager);
                    Console.WriteLine($"[ServerHeadless] New game: host culture '{culture.Name}'.");
                    break;

                case CharacterCreationClanNamingStage _:
                    manager.CharacterCreationContent.MainCharacterName = HostHeroName;
                    break;
            }

            manager.NextStage();
        }
    }
}
