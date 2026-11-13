using Common;
using SandBox;
using SandBox.GauntletUI.CharacterCreation;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// True while the skip runs, so the character creation state it pushes does not start a second skip.
        /// </summary>
        private static bool skipping;

        public static bool InCharacterCreation()
        {
            return GameStateManager.Current?.ActiveState is CharacterCreationState;
        }

        /// <summary>
        /// The culture the skip picks: Empire when the game has it, else the first one. A total conversion
        /// can rename or remove Empire.
        /// </summary>
        internal static int ChooseCultureIndex(IReadOnlyList<string> cultureNames)
        {
            for (int i = 0; i < cultureNames.Count; i++)
            {
                if (cultureNames[i] == "Empire") return i;
            }
            return 0;
        }

        internal enum SkipEntry { None, Inline, NextFrame }

        /// <summary>
        /// The intro path runs the skip at once, as it pushes character creation itself. A direct start is seen
        /// while the state activates, before its screen and stage views exist, so the skip waits a frame.
        /// </summary>
        internal static SkipEntry ChooseSkipEntry(bool inIntro, bool inCharacterCreation, bool skipInProgress)
        {
            if (skipInProgress) return SkipEntry.None;
            if (inIntro) return SkipEntry.Inline;
            return inCharacterCreation ? SkipEntry.NextFrame : SkipEntry.None;
        }

        public void SkipCharacterCreation()
        {
            switch (ChooseSkipEntry(InCharacterCreationIntro(), InCharacterCreation(), skipping))
            {
                case SkipEntry.Inline:
                    GameThread.Run(SkipCharacterCreationInternal);
                    break;
                case SkipEntry.NextFrame:
                    GameThread.EnqueueSafe(SkipCharacterCreationInternal, "DEBUG character creation skip");
                    break;
            }
        }

        public void SkipCharacterCreationInternal()
        {
            if (skipping) return;
            skipping = true;
            try
            {
                SkipStages();
            }
            finally
            {
                skipping = false;
            }
        }

        private static void SkipStages()
        {
            // Skip intro video, when the game plays it
            if (InCharacterCreationIntro())
            {
                SandBoxGameManager gameManager = (SandBoxGameManager)Game.Current.GameManager;
                gameManager.LaunchSandboxCharacterCreation();
            }

            CharacterCreationState characterCreationState = GameStateManager.Current.ActiveState as CharacterCreationState;
            if (characterCreationState == null) return;
            CharacterCreationStageBase currentStage = characterCreationState._characterCreationManager.CurrentStage;
            
            if (currentStage is CharacterCreationCultureStage)
            {
                var cultures = characterCreationState._characterCreationManager.CharacterCreationContent.GetCultures().ToList();
                CultureObject culture = cultures[ChooseCultureIndex(cultures.Select(c => c.Name.ToString()).ToList())];
                characterCreationState._characterCreationManager.CharacterCreationContent.SetSelectedCulture(culture, characterCreationState._characterCreationManager);
                characterCreationState._characterCreationManager.NextStage();
            }

            if (characterCreationState._characterCreationManager.CurrentStage is CharacterCreationFaceGeneratorStage)
            {
                ICharacterCreationStageListener listener = characterCreationState._characterCreationManager.CurrentStage.Listener;
                BodyGeneratorView bgv = (listener as CharacterCreationFaceGeneratorView)._faceGeneratorView;

                FaceGenVM facegen = bgv.DataSource;

                facegen.FaceProperties.Randomize();
                characterCreationState._characterCreationManager.NextStage();
            }

            if (characterCreationState._characterCreationManager.CurrentStage is CharacterCreationNarrativeStage)
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

            if (characterCreationState._characterCreationManager.CurrentStage is CharacterCreationBannerEditorStage)
            {
                characterCreationState._characterCreationManager.NextStage();
            }

            if (characterCreationState._characterCreationManager.CurrentStage is CharacterCreationClanNamingStage)
            {
                characterCreationState._characterCreationManager.CharacterCreationContent.MainCharacterName = "RandomPlayer";
                characterCreationState._characterCreationManager.NextStage();
            }

            if (characterCreationState._characterCreationManager.CurrentStage is CharacterCreationReviewStage)
            {
                characterCreationState._characterCreationManager.NextStage();
            }

            if (characterCreationState._characterCreationManager.CurrentStage is CharacterCreationOptionsStage)
            {
                characterCreationState._characterCreationManager.NextStage();
            }
        }
    }
}