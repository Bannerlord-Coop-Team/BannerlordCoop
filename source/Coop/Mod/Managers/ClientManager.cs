using System;
using System.Collections.Generic;
using System.Linq;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem.Load;
using System.Reflection;
using TaleWorlds.CampaignSystem.Actions;

namespace Coop.Mod.Managers
{
    public class ClientManager : CampaignGameManager
    {
        readonly string m_PartyName;
        Hero clientPlayer;
        public ClientManager(LoadResult saveGameData, string partyName) : base(saveGameData) { m_PartyName = partyName; }

        public delegate void OnOnLoadFinishedEventHandler(object source, EventArgs e);
        public static event OnOnLoadFinishedEventHandler OnLoadFinishedEvent;
        public override void OnLoadFinished()
        {
            base.OnLoadFinished();
            foreach(MobileParty party in MobileParty.All)
            {
                if (party.StringId == "player_party1")
                {
                    string name = party.Name.ToString();
                    if (name == m_PartyName)
                    {
                        clientPlayer = party.LeaderHero;
                        ChangePlayerCharacterAction.Apply(clientPlayer);
                        Settlement settlement = Settlement.Find("tutorial_training_field");
                        Campaign.Current.HandleSettlementEncounter(MobileParty.MainParty, settlement);
                        PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(LocationComplex.Current.GetLocationWithId("training_field"), null, null, null);
                        clientPlayer.PartyBelongedTo.Party.MemberRoster.OnHeroHealthStatusChanged(clientPlayer);
                    }
                }
            }
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
