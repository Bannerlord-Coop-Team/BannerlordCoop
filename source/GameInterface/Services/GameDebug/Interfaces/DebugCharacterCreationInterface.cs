using Common;
using SandBox;
using SandBox.GauntletUI.CharacterCreation;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation;
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
            gameManager.LaunchSandboxCharacterCreation();

            CharacterCreationState characterCreationState = GameStateManager.Current.ActiveState as CharacterCreationState;
            CharacterCreationStageBase currentStage = characterCreationState._characterCreationManager.CurrentStage;

            if (currentStage is CharacterCreationCultureStage)
            {
                var cultures = characterCreationState._characterCreationManager.CharacterCreationContent.GetCultures();
                CultureObject culture = cultures.First(c => c.Name.ToString() == "Empire");
                characterCreationState._characterCreationManager.CharacterCreationContent.SetSelectedCulture(culture, characterCreationState._characterCreationManager);
                characterCreationState._characterCreationManager.NextStage();
            }

            if (currentStage is CharacterCreationFaceGeneratorStage)
            {
                ICharacterCreationStageListener listener = characterCreationState._characterCreationManager.CurrentStage.Listener;
                BodyGeneratorView bgv = (listener as CharacterCreationFaceGeneratorView)._faceGeneratorView;

                FaceGenVM facegen = bgv.DataSource;

                facegen.FaceProperties.Randomize();
                characterCreationState._characterCreationManager.NextStage();
            }

            if (currentStage is CharacterCreationNarrativeStage)
            {
                for (int i = 0; i < characterCreationState._characterCreationManager.CharacterCreationMenuCount; i++)
                {
                    NarrativeMenuOption characterCreationOption = characterCreationState._characterCreationManager.GetCurrentMenuOptions(i).FirstOrDefault((NarrativeMenuOption o) => o.OnCondition == null || o.OnCondition(characterCreationState._characterCreationManager));
                    bool flag4 = characterCreationOption != null;
                    if (flag4)
                    {
                        //characterCreationState.CharacterCreation.RunConsequence(characterCreationOption, i, false);
                    }
                }
                characterCreationState._characterCreationManager.NextStage();
            }

            if (currentStage is CharacterCreationBannerEditorStage)
            {
                characterCreationState._characterCreationManager.NextStage();
            }

            if (currentStage is CharacterCreationClanNamingStage)
            {

                characterCreationState._characterCreationManager.CharacterCreationContent.MainCharacterName = "RandomPlayer";
                characterCreationState._characterCreationManager.NextStage();
            }

            if (currentStage is CharacterCreationReviewStage)
            {
                characterCreationState._characterCreationManager.NextStage();
            }

            if (currentStage is CharacterCreationOptionsStage)
            {
                characterCreationState._characterCreationManager.NextStage();
            }
        }
    }
}