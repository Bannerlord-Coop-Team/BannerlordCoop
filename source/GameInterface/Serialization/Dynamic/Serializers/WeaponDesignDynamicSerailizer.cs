using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Dynamic.Serializers
{
    internal class WeaponDesignDynamicSerailizer : IDynamicSerializer
    {
        public WeaponDesignDynamicSerailizer(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.CreateDynamicSerializer<WeaponDesign>();
            modelGenerator.CreateDynamicSerializer<WeaponDesignElement>();
            modelGenerator.CreateDynamicSerializer<WeaponDescription>();
            modelGenerator.CreateDynamicSerializer<CraftingPiece>();
            modelGenerator.CreateDynamicSerializer<PieceData>();
            modelGenerator.CreateDynamicSerializer<BladeData>();
        }
    }
}
