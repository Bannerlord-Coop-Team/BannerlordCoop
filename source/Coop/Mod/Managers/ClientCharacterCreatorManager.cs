using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using Coop.Mod.Serializers;
using SandBox;
using StoryMode;
using StoryMode.CharacterCreationSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.Screens;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem.Load;
using System.Reflection;
using NetworkMessages.FromClient;
using Module = TaleWorlds.MountAndBlade.Module;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Helpers;
using Sync.Store;

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
        public ClientCharacterCreatorManager(LoadResult saveGameData) : base(saveGameData) { }

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

            if (Main.DEBUG)
            {
                SkipCharacterCreation();
            }

            OnGameLoadFinishedEvent?.Invoke(this, new HeroEventArgs(
                MobileParty.MainParty.Name.ToString(),
                CoopClient.Instance.SyncedObjectStore.Insert(Hero.MainHero)
            ));
            EndGame();
        }

        private void SkipCharacterCreation()
        {
            CharacterCreationState characterCreationState = GameStateManager.Current.ActiveState as CharacterCreationState;
            bool flag = CharacterCreationContent.Instance.Culture == null;
            if (flag)
            {
                CultureObject culture = CharacterCreationContent.Instance.GetCultures().FirstOrDefault<CultureObject>();
                CharacterCreationContent.Instance.Culture = culture;
                CharacterCreationContent.CultureOnCondition(characterCreationState.CharacterCreation);
                characterCreationState.NextStage();
            }
            bool flag2 = characterCreationState.CurrentStage is CharacterCreationFaceGeneratorStage;
            if (flag2)
            {
                characterCreationState.NextStage();
            }
            bool flag3 = characterCreationState.CurrentStage is CharacterCreationGenericStage;
            if (flag3)
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
            bool flag5 = characterCreationState.CurrentStage is CharacterCreationReviewStage;
            if (flag5)
            {
                characterCreationState.NextStage();
            }
            bool flag6 = characterCreationState.CurrentStage is CharacterCreationOptionsStage;
            if (flag6)
            {
                (Game.Current.GameStateManager.ActiveState as CharacterCreationState).CharacterCreation.Name = "Jeff";
                characterCreationState.NextStage();
            }
            characterCreationState = (GameStateManager.Current.ActiveState as CharacterCreationState);
        }
    }
}
