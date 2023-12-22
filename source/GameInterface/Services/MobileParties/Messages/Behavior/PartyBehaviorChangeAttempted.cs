using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Services.MobileParties.Handlers;

namespace GameInterface.Services.MobileParties.Messages.Behavior
{
    /// <summary>
    /// The game has attempted to change party behavior.
    /// </summary>
    /// <seealso cref="MobilePartyBehaviorHandler"/>
    [DontLogMessage]
    internal record PartyBehaviorChangeAttempted : IEvent
    {
        public MobileParty Party { get; }
        public PartyBehaviorUpdateData BehaviorUpdateData { get; }

        public PartyBehaviorChangeAttempted(MobileParty party, PartyBehaviorUpdateData behaviorUpdateData)
        {
            Party = party;
            BehaviorUpdateData = behaviorUpdateData;
        }
    }
}