using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Data.IFactionDatas;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms.KingdomDecision
{
    /// <summary>
    /// Class for testing the serialization of DeclareWarDecisionData.
    /// </summary>
    public class DeclareWarDecisionSerializationTest
    {
        DeclareWarDecisionData DeclareWarDecisionClan { get; }
        DeclareWarDecisionData DeclareWarDecisionKingdom { get; }
        public DeclareWarDecisionSerializationTest() 
        {
            DeclareWarDecisionClan = new DeclareWarDecisionData("ProposerClan", "Kingdom", 10, true, true, true, new ClanFactionData("ClanFaction"));
            DeclareWarDecisionKingdom = new DeclareWarDecisionData("ProposerClan", "Kingdom", 10, true, true, true, new KingdomFactionData("KingdomFaction"));
        }

        [Fact]
        public void SerializeDeclareWarDecisionDataWithClanFaction()
        {
            MemoryStream memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream,DeclareWarDecisionClan);
            DeclareWarDecisionData deserializedObj = Serializer.Deserialize<DeclareWarDecisionData>(memoryStream);
            Assert.Equal(DeclareWarDecisionClan.ProposerClanId, deserializedObj.ProposerClanId);
            Assert.Equal(DeclareWarDecisionClan.KingdomId, deserializedObj.KingdomId);
            Assert.Equal(DeclareWarDecisionClan.PlayerExamined, deserializedObj.PlayerExamined);
            Assert.Equal(DeclareWarDecisionClan.TriggerTime, deserializedObj.TriggerTime);
            Assert.Equal(DeclareWarDecisionClan.NotifyPlayer, deserializedObj.NotifyPlayer);
            Assert.Equal(DeclareWarDecisionClan.IsEnforced, deserializedObj.IsEnforced);
            Assert.IsType(DeclareWarDecisionClan.FactionToDeclareWarOn.GetType() , deserializedObj.FactionToDeclareWarOn);
            Assert.Equal(((ClanFactionData)DeclareWarDecisionClan.FactionToDeclareWarOn).ClanId, ((ClanFactionData)deserializedObj.FactionToDeclareWarOn).ClanId);

        }
    }
}
