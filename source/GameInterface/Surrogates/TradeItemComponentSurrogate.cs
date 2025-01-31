using Common.Util;
using ProtoBuf;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Surrogates
{
    [ProtoContract]
    internal struct TradeItemComponentSurrogate
    {
        [ProtoMember(1)]
        public ItemObject Item { get; set; }
        [ProtoMember(2)]
        public ItemModifierGroup ItemModifierGroup { get; set; }


        public TradeItemComponentSurrogate(TradeItemComponent tradeItemComponent)
        {
            if (tradeItemComponent == null)
            {
                Item = ObjectHelper.SkipConstructor<ItemObject>();
                ItemModifierGroup = ObjectHelper.SkipConstructor<ItemModifierGroup>();
            }
            else
            {
                Item = tradeItemComponent.Item;
                ItemModifierGroup = tradeItemComponent.ItemModifierGroup;
            }
        }

        public static implicit operator TradeItemComponentSurrogate(TradeItemComponent tradeItemComponent)
        {
            return new TradeItemComponentSurrogate(tradeItemComponent);
        }

        public static implicit operator TradeItemComponent(TradeItemComponentSurrogate surrogate)
        {
            return new TradeItemComponent();
        }
    }
}

