using Common;
using SandBox;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;
using TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator;

namespace GameInterface.Services.GameDebug.Interfaces
{
    internal interface IDebugCharacterCreationInterface : IGameAbstraction
    {
        void SkipCharacterCreation();
    }

    internal class DebugCharacterCreationInterface : IDebugCharacterCreationInterface
    {
        /// <summary>
        /// Name of into video from <see cref="SandboxGameManager.OnLoadFinished"/>
        /// </summary>
        private static readonly string VideoPathName = "campaign_intro";

        /// <summary>
        /// Determines if game is currently running the character creation intro.
        /// The character creation into is the first state of character creation.
        /// </summary>
        /// <returns>True if game is in character creation intro state, false otherwise</returns>
        public static bool InCharacterCreationIntro()
        {
            return GameStateManager.Current?.ActiveState is VideoPlaybackState videoState &&
                   videoState.VideoPath.Contains(VideoPathName);
        }

        private readonly MethodInfo LaunchSandboxCharacterCreation = typeof(SandBoxGameManager).GetMethod("LaunchSandboxCharacterCreation", BindingFlags.NonPublic | BindingFlags.Instance);
        public void SkipCharacterCreation()
        {
            // Validation
            if (InCharacterCreationIntro() == false) return;

            // Logic
            GameLoopRunner.RunOnMainThread(SkipCharacterCreationInternal);
        }

        public void SkipCharacterCreationInternal()
        {
            // Skip intro video
            SandBoxGameManager gameManager = (SandBoxGameManager)Game.Current.GameManager;
            LaunchSandboxCharacterCreation.Invoke(gameManager, Array.Empty<object>());

            CharacterCreationState characterCreationState = GameStateManager.Current.ActiveState as CharacterCreationState;
            if (characterCreationState.CurrentStage is CharacterCreationCultureStage)
            {
                var cultures = CharacterCreationContentBase.Instance.GetCultures();
                CultureObject culture = cultures.First(c => c.Name.ToString() == "Empire");
                CharacterCreationContentBase.Instance.SetSelectedCulture(culture, characterCreationState.CharacterCreation);
                characterCreationState.NextStage();
            }

            if (characterCreationState.CurrentStage is CharacterCreationFaceGeneratorStage)
            {
                ICharacterCreationStageListener listener = characterCreationState.CurrentStage.Listener;
                BodyGeneratorView bgv = (BodyGeneratorView)listener.GetType().GetField("_faceGeneratorView", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(listener);

                FaceGenVM facegen = bgv.DataSource;

                facegen.FaceProperties.Randomize();
                characterCreationState.NextStage();
            }

            if (characterCreationState.CurrentStage is CharacterCreationGenericStage)
            {
                for (int i = 0; i < characterCreationState.CharacterCreation.CharacterCreationMenuCount; i++)
                {
                    CharacterCreationOption characterCreationOption = characterCreationState.CharacterCreation.GetCurrentMenuOptions(i).FirstOrDefault((CharacterCreationOption o) => o.OnCondition == null || o.OnCondition());
                    bool flag4 = characterCreationOption != null;
                    if (flag4)
                    {
                        characterCreationState.CharacterCreation.RunConsequence(characterCreationOption, i, false);
                    }
                }
                characterCreationState.NextStage();
            }

            if (characterCreationState.CurrentStage is CharacterCreationBannerEditorStage)
            {
                characterCreationState.NextStage();
            }

            if (characterCreationState.CurrentStage is CharacterCreationClanNamingStage)
            {
                characterCreationState.CharacterCreation.Name = "RandomPlayer";
                characterCreationState.NextStage();
            }

            if (characterCreationState.CurrentStage is CharacterCreationReviewStage)
            {
                characterCreationState.NextStage();
            }

            if (characterCreationState.CurrentStage is CharacterCreationOptionsStage)
            {
                characterCreationState.NextStage();
            }
        }
    }
}