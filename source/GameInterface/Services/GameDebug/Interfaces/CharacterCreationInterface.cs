using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator;
using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;
using TaleWorlds.Library;

namespace GameInterface.Services.GameDebug.Interfaces
{
    internal class CharacterCreationInterface : ICharacterCreationInterface
    {
        public void SkipCharacterCreation()
        {
            if(GameStateManager.Current?.ActiveState?.GetType() == typeof(CharacterCreationState))
            {
                SkipCharacterCreationInternal();
            }
        }

        private void SkipCharacterCreationInternal()
        {
            CharacterCreationState characterCreationState = GameStateManager.Current.ActiveState as CharacterCreationState;
            if (characterCreationState.CurrentStage is CharacterCreationCultureStage)
            {
                CultureObject culture = CharacterCreationContentBase.Instance.GetCultures().GetRandomElementInefficiently();
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

            characterCreationState = (GameStateManager.Current.ActiveState as CharacterCreationState);
        }
    }
}