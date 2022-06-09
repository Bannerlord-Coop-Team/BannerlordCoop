using System;
using System.Linq;
using StoryMode;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.ScreenSystem;
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
using System.Collections.Generic;
using Coop.Mod.Extentions;
using Coop.Mod.Serializers;
using SandBox;
using TaleWorlds.ObjectSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;
using TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator;

namespace Coop.Mod.Managers
{
    public class HeroEventArgs : EventArgs
    {
        public PlayerHeroSerializer SerializedHero { get; private set; }
    }
    public class ClientCharacterCreatorManager : SandBoxGameManager
    {
        public ClientCharacterCreatorManager()
        {
        }

        public delegate void OnLoadFinishedEventHandler(object source, EventArgs e);
        public static event Action OnCharacterCreationFinishedEvent;
        public static event OnLoadFinishedEventHandler OnGameLoadFinishedEvent;

        public MobileParty ClientParty { get; private set; }
        public Hero ClientHero { get; private set; }
        public CharacterObject ClientCharacterObject { get; private set; }
        

        public override void OnLoadFinished()
        {
            base.OnLoadFinished();

            CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, () => { OnCharacterCreationFinishedEvent?.Invoke(); });

#if DEBUG
            SkipCharacterCreation();
#endif

            OnGameLoadFinishedEvent?.Invoke(this, new HeroEventArgs());
        }

        public void RemoveAllObjects()
        {
            CampaignObjectManager campaignObjectManager = Campaign.Current.CampaignObjectManager;

            //campaignObjectManager.GetMobileParties().RemoveAll(x => true);
            //campaignObjectManager.GetDeadOrDisabledHeros().RemoveAll(x => true);
            //campaignObjectManager.GetAliveHeros().RemoveAll(x => true);
            //campaignObjectManager.GetClans().RemoveAll(x => true);
            //campaignObjectManager.GetKingdoms().RemoveAll(x => true);

            //MBObjectManager.Instance.ClearAllObjectsWithType(typeof(MobileParty));
            //MBObjectManager.Instance.ClearAllObjectsWithType(typeof(CharacterObject));
            //MBObjectManager.Instance.ClearAllObjectsWithType(typeof(Hero));

            //typeof(Campaign)
            //    .GetField("_towns", BindingFlags.Instance | BindingFlags.NonPublic)
            //    .SetValue(Campaign.Current, new List<Town>());
            //typeof(Campaign)
            //    .GetField("_castles", BindingFlags.Instance | BindingFlags.NonPublic)
            //    .SetValue(Campaign.Current, new List<Town>());
            //typeof(Campaign)
            //    .GetField("_villages", BindingFlags.Instance | BindingFlags.NonPublic)
            //    .SetValue(Campaign.Current, new List<Village>());
            //typeof(Campaign)
            //    .GetField("_hideouts", BindingFlags.Instance | BindingFlags.NonPublic)
            //    .SetValue(Campaign.Current, new List<Hideout>());


            //List<Settlement> settlements = campaignObjectManager.Settlements.ToList();
            //settlements.ForEach(x => Campaign.Current.ObjectManager.UnregisterObject(x));

            MobileParty.MainParty.RemoveParty();

            GC.Collect();
        }

        private void RemoveHero(Hero hero)
        {
            KillCharacterAction.ApplyByRemove(hero);
        }

        private void RemoveParty(MobileParty party)
        {
            FieldInfo _mobilePartyLocator = typeof(Campaign).GetField("_mobilePartyLocator", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo _customHomeSettlement = typeof(MobileParty).GetField("_customHomeSettlement", BindingFlags.NonPublic | BindingFlags.Instance);


            party.IsActive = false;
            party.IsVisible = false;
            Campaign campaign = Campaign.Current;
            IPartyVisual visuals = party.Party.Visuals;
            if (visuals != null)
            {
                visuals.OnPartyRemoved();
            }
            party.AttachedTo = null;
            party.BesiegerCamp = null;
            party.ItemRoster.Clear();
            party.MemberRoster.Reset();
            party.PrisonRoster.Reset();
            //((LocatorGrid<MobileParty>)_mobilePartyLocator.GetValue(Campaign.Current)).RemoveParty(party);
            CampaignEventDispatcher.Instance.OnPartyRemoved(party.Party);
            OnRemoveParty(party);
            //party._customHomeSettlement = null;
        }

        private void OnRemoveParty(MobileParty party)
        {
            party.Army = null;
            party.CurrentSettlement = null;
            party.AttachedTo = null;
            party.BesiegerCamp = null;
            List<Settlement> list = new List<Settlement>();
            if (party.CurrentSettlement != null)
            {
                list.Add(party.CurrentSettlement);
            }
            else if ((party.IsGarrison || party.IsMilitia || party.IsBandit || party.IsVillager) && party.HomeSettlement != null)
            {
                list.Add(party.HomeSettlement);
            }
            PartyComponent partyComponent = party.PartyComponent;
            if (partyComponent != null)
            {
                //partyComponent.Finish();
            }
            party.ActualClan = null;
            //Campaign.Current.CampaignObjectManager.RemoveMobileParty(party);
            foreach (Settlement settlement in list)
            {
                settlement.SettlementComponent.OnRelatedPartyRemoved(party);
            }
        }

        private void SkipCharacterCreation()
        {
            if (GameStateManager.Current.ActiveState is VideoPlaybackState videoPlaybackState)
            {
                if (ScreenManager.TopScreen is GauntletVideoPlaybackScreen)
                {
                    GauntletVideoPlaybackScreen videoPlaybackScreen = ScreenManager.TopScreen as GauntletVideoPlaybackScreen;
                    FieldInfo fieldInfo = typeof(GauntletVideoPlaybackScreen).GetField("_videoPlayerView", BindingFlags.NonPublic | BindingFlags.Instance);
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

            if (characterCreationState.CurrentStage is CharacterCreationBannerEditorStage)
            {
                characterCreationState.NextStage();
            }

            if (characterCreationState.CurrentStage is CharacterCreationClanNamingStage)
            {
                characterCreationState.CharacterCreation.Name = "Joke";
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
