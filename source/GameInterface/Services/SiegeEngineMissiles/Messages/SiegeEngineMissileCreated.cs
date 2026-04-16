using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEngineMissiles.Messages
{
    /// <summary>
    /// An event that is published internally when SiegeEngineMissile is created.
    /// </summary>
    internal class SiegeEngineMissileCreated : IEvent
    {
        public SiegeEngineMissileCreated(SiegeEvent.SiegeEngineMissile data)
        {
            Data = data;
        }

        public SiegeEvent.SiegeEngineMissile Data { get; }
    }
}
