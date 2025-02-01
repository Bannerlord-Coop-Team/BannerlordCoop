using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventSides.Messages;
internal record MapEventSideDestroyed(MapEventSide Instance) : IEvent
{
    public MapEventSide Instance { get; } = Instance;
}
