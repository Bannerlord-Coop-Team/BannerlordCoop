using System;
using System.Collections.Generic;
using System.Linq;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem.Load;
using System.Reflection;
using TaleWorlds.CampaignSystem.Actions;
using System.Threading.Tasks;

namespace Coop.Mod.Managers
{
    public class ClientManager : CampaignGameManager
    {
        readonly string m_PartyName;
        Hero clientPlayer;
        public ClientManager(LoadResult saveGameData, string partyName) : base(saveGameData) { m_PartyName = partyName; }

        public delegate void OnOnLoadFinishedEventHandler(object source, EventArgs e);
        public static event OnOnLoadFinishedEventHandler OnPreLoadFinishedEvent;
        public static event OnOnLoadFinishedEventHandler OnPostLoadFinishedEvent;
        public override void OnLoadFinished()
        {
            OnPreLoadFinishedEvent?.Invoke(this, EventArgs.Empty);
            base.OnLoadFinished();
            OnPostLoadFinishedEvent?.Invoke(this, EventArgs.Empty);

            // Find actual unique way to differentiate parties
            MobileParty playerParty = MobileParty.All.AsParallel().SingleOrDefault(party => party.Name.ToString() == m_PartyName);

            if(playerParty != null)
            {
                clientPlayer = playerParty.LeaderHero;

                // Switch current player party from host to client party
                ChangePlayerCharacterAction.Apply(clientPlayer);

                // Start player at training field
                Settlement settlement = Settlement.Find("tutorial_training_field");
                Campaign.Current.HandleSettlementEncounter(MobileParty.MainParty, settlement);
                PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(LocationComplex.Current.GetLocationWithId("training_field"), null, null, null);
            }
            else
            {
                throw new Exception("Transferred player party could not be found");
            }
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
