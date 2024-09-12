﻿using GameInterface.Services.Registry;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.WorkshopTypes
{
    internal class WorkshopTypeRegistry : RegistryBase<WorkshopType>
    {
        public WorkshopTypeRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            foreach (WorkshopType workshopType in WorkshopType.All)
            {
                RegisterNewObject(workshopType, out var _);
            }
        }

        protected override string GetNewId(WorkshopType party)
        {
            return Guid.NewGuid().ToString();
        }
    }
}
