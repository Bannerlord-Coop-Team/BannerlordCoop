using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using MBMultiplayerCampaign.Serializers;
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

namespace Coop.Mod.Managers
{
    class ClientGameManager : StoryModeGameManager
    {
        public ClientGameManager() : base() 
        {
            // CharacterCreationState.FinalizeCharacterCreation()
            StoryModeEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, GetCreatedCharacter);

            if(Main.DEBUG)
            {
                Game.GameStateManager.CleanAndPushState(Game.Current.GameStateManager.CreateState<CharacterCreationState>());
                SkipCharacterCreation();
                // TODO use HeroCreator.CreateNewHero
                // TODO Populate using CharacterCreator
                // TODO Override 
                ChangePlayerCharacterAction.Apply(ClientHero);
            }
        }
        public ClientGameManager(LoadResult saveGameData) : base(saveGameData) { }

        public delegate void OnOnLoadFinishedEventHandler(object source, EventArgs e);
        public event OnOnLoadFinishedEventHandler OnLoadFinishedEvent;

        public Hero ClientHero { get; private set; }

        public override void OnLoadFinished()
        {
            base.OnLoadFinished();
            

            OnLoadFinishedEvent?.Invoke(this, EventArgs.Empty);
            
            
        }

        public void GetCreatedCharacter()
        {
            // CharacterCreationContent.ApplySkillAndAttributeEffects()
            ClientHero = Hero.MainHero;
        }

        

        public new void OnTick(float dt)
        {
            FieldInfo entityFieldInfo = typeof(GameManagerBase).GetField("_entitySystem", BindingFlags.Instance | BindingFlags.NonPublic);
            if(entityFieldInfo.GetValue(this) == null)
            {
                entityFieldInfo.SetValue(this, new EntitySystem<GameManagerComponent>());
            }
            base.OnTick(dt);
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
