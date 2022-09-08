using GameInterface.Serialization.Dynamic;
using System.Reflection.Emit;
using TaleWorlds.Core;
using static TaleWorlds.Core.HorseComponent;

namespace GameInterface.Serialization.Dynamic.Serializers
{
    internal class ItemComponentDynamicSerializer : IDynamicSerializer
    {
        public ItemComponentDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            // Derived ItemComponents
            modelGenerator.CreateDynamicSerializer<ArmorComponent>();
            modelGenerator.CreateDynamicSerializer<HorseComponent>();
            modelGenerator.CreateDynamicSerializer<SaddleComponent>();
            modelGenerator.CreateDynamicSerializer<TradeItemComponent>();
            modelGenerator.CreateDynamicSerializer<WeaponComponent>();

            // Internal classes
            modelGenerator.CreateDynamicSerializer<WeaponComponentData>();
            modelGenerator.CreateDynamicSerializer<MaterialProperty>();

            modelGenerator.CreateDynamicSerializer<ItemComponent>()
                .AddDerivedType<ArmorComponent>()
                .AddDerivedType<HorseComponent>()
                .AddDerivedType<SaddleComponent>()
                .AddDerivedType<TradeItemComponent>()
                .AddDerivedType<WeaponComponent>();

        }
    }
}
