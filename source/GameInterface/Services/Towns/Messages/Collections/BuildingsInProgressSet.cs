using GameInterface.Utils;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record BuildingsInProgressSet : GenericSetEvent<Town, Queue<Building>>
    {
        public BuildingsInProgressSet(Town instance, Queue<Building> value) : base(instance, value)
        {
        }
    }
}
