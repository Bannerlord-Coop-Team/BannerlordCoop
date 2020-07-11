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

namespace Coop.Mod.Managers
{
    public class ClientManager : CampaignGameManager
    {
        public ClientManager(LoadResult saveGameData) : base(saveGameData) { }

        public delegate void OnOnLoadFinishedEventHandler(object source, EventArgs e);
        public static event OnOnLoadFinishedEventHandler OnLoadFinishedEvent;

        public override void OnLoadFinished()
        {
            base.OnLoadFinished();

            OnLoadFinishedEvent?.Invoke(this, EventArgs.Empty);

            // TODO recieve host party and instantiate it            
            //ClientParty = new MobileParty();
            //TextObject name = MobilePartyHelper.GeneratePartyName(player);
            //ClientParty.InitializeMobileParty(name, Game.Current.ObjectManager.GetObject<PartyTemplateObject>("main_hero_party_template"), new Vec2(685.3f, 410.9f), 0f, 0f, MobileParty.PartyTypeEnum.Default, -1);
            //ClientParty.ItemRoster.AddToCounts(DefaultItems.Grain, 1, true);
            //ClientParty.Party.Owner = clientHero;
            //ClientParty.SetAsMainParty();
            //Campaign.Current.CameraFollowParty = ClientParty.Party;
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
    }
}
