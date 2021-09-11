using System;
using System.Linq;
using StoryMode;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem.Load;
using System.Reflection;
using TaleWorlds.Library;
using Sync.Store;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.MountAndBlade.GauntletUI;
using TaleWorlds.MountAndBlade.ViewModelCollection;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Engine;
using TaleWorlds.Localization;

namespace Coop.Mod.Managers
{
    public class HeroEventArgs : EventArgs
    {

        public ObjectId HeroId { get; private set; }
        public string PartyName { get; private set; }
        public HeroEventArgs(string PartyName, ObjectId HeroId)
        {
            this.PartyName = PartyName;
            this.HeroId = HeroId;
        }
    }
    public class ClientCharacterCreatorManager : StoryModeGameManager
    {
        public ClientCharacterCreatorManager()
        {
        }

        public delegate void OnLoadFinishedEventHandler(object source, EventArgs e);
        public static event OnLoadFinishedEventHandler OnCharacterCreationLoadFinishedEvent;
        public static event OnLoadFinishedEventHandler OnGameLoadFinishedEvent;

        public MobileParty ClientParty { get; private set; }
        public Hero ClientHero { get; private set; }
        public CharacterObject ClientCharacterObject { get; private set; }
        

        public override void OnLoadFinished()
        {
            base.OnLoadFinished();

            OnCharacterCreationLoadFinishedEvent?.Invoke(this, EventArgs.Empty);

#if DEBUG
            SkipCharacterCreation();
#endif
            Settlement settlement = Settlement.Find("tutorial_training_field");
            MobileParty.MainParty.Position2D = settlement.Position2D;

            OnGameLoadFinishedEvent?.Invoke(this, new HeroEventArgs(
                MobileParty.MainParty.Name.ToString(),
                CoopClient.Instance.SyncedObjectStore.Insert(Hero.MainHero)
            ));
            EndGame();
        }

        private void SkipCharacterCreation()
        {
            if (GameStateManager.Current.ActiveState is VideoPlaybackState videoPlaybackState)
            {
                if (ScreenManager.TopScreen is VideoPlaybackGauntletScreen)
                {
                    VideoPlaybackGauntletScreen videoPlaybackScreen = ScreenManager.TopScreen as VideoPlaybackGauntletScreen;
                    FieldInfo fieldInfo = typeof(VideoPlaybackGauntletScreen).GetField("_videoPlayerView", BindingFlags.NonPublic | BindingFlags.Instance);
                    VideoPlayerView videoPlayerView = (VideoPlayerView)fieldInfo.GetValue(videoPlaybackScreen);
                    videoPlayerView.StopVideo();
                    videoPlaybackState.OnVideoFinished();
                    videoPlayerView.SetEnable(false);
                    fieldInfo.SetValue(videoPlaybackScreen, null);
                }
            }


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

            if (characterCreationState.CurrentStage is CharacterCreationReviewStage)
            {
                characterCreationState.NextStage();
            }

            characterCreationState = (GameStateManager.Current.ActiveState as CharacterCreationState);
        }
    }
}
