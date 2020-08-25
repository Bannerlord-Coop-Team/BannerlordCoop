using System;
using System.Collections.Generic;
using System.Linq;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem.Load;
using System.Reflection;
using System.Threading.Tasks;
using SandBox.View.Map;

namespace Coop.Mod.Managers
{
    public class ClientManager : CampaignGameManager
    {
        readonly string m_PartyName;
        public ClientManager(LoadResult saveGameData, string partyName) : base(saveGameData) { m_PartyName = partyName; }

        public delegate void OnOnLoadFinishedEventHandler(object source, EventArgs e);
        public static event OnOnLoadFinishedEventHandler OnLoadFinishedEvent;
        public override void OnLoadFinished()
        {
            base.OnLoadFinished();
            IEnumerable<MobileParty> parties = MobileParty.All.Where((party) => party.Name.ToString().Equals(m_PartyName));
            OnLoadFinishedEvent?.Invoke(this, EventArgs.Empty);
            
            //parties.Single().SetAsMainParty();
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
