using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Towns.Messages
{
    /// <summary>
    /// Server sends this data when a Town Changes GarrisonAutoRecruitmentIsEnabled
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkChangeTownGarrisonAutoRecruitmentIsEnabled : IEvent
    {
        [ProtoMember(1)]
        public string TownId { get; }
        [ProtoMember(2, IsRequired = true)]
        public bool GarrisonAutoRecruitmentIsEnabled { get; }

        public NetworkChangeTownGarrisonAutoRecruitmentIsEnabled(string townId, bool garrisonAutoRecruitmentIsEnabled)
        {
            TownId = townId;
            GarrisonAutoRecruitmentIsEnabled = garrisonAutoRecruitmentIsEnabled;
        }
    }
}
