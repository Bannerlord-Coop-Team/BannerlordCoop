using Common.Util;
using ProtoBuf;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Surrogates
{
    [ProtoContract]
    internal struct SaddleComponentSurrogate
    {
        [ProtoMember(1)]
        public ItemObject Item { get; set; }
        [ProtoMember(2)]
        public ItemModifierGroup ItemModifierGroup { get; set; }


        public SaddleComponentSurrogate(SaddleComponent saddleComponent)
        {
            if (saddleComponent == null)
            {
                Item = ObjectHelper.SkipConstructor<ItemObject>();
                ItemModifierGroup = ObjectHelper.SkipConstructor<ItemModifierGroup>();
            }
            else
            {
                Item = saddleComponent.Item;
                ItemModifierGroup = saddleComponent.ItemModifierGroup;
            }
        }

        public static implicit operator SaddleComponentSurrogate(SaddleComponent saddleComponent)
        {
            return new SaddleComponentSurrogate(saddleComponent);
        }

        public static implicit operator SaddleComponent(SaddleComponentSurrogate surrogate)
        {
            return new SaddleComponent(surrogate);
        }
    }
}
