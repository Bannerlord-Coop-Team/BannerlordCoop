using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class MBBodyPropertySerializer : ICustomSerializer
    {
        BodyPropertySerializer bodyPropertyMin;
        BodyPropertySerializer bodyPropertyMax;
        public MBBodyPropertySerializer(MBBodyProperty mbBodyProperty)
        {
            bodyPropertyMin = new BodyPropertySerializer(mbBodyProperty.BodyPropertyMin);
            bodyPropertyMax = new BodyPropertySerializer(mbBodyProperty.BodyPropertyMax);
        }

        public object Deserialize()
        {
            BodyProperties bodyPropertyMin = (BodyProperties)this.bodyPropertyMin.Deserialize();
            BodyProperties bodyPropertyMax = (BodyProperties)this.bodyPropertyMax.Deserialize();
            MBBodyProperty mBBodyProperty = new MBBodyProperty();
            mBBodyProperty.Init(bodyPropertyMin, bodyPropertyMax);
            return mBBodyProperty;
        }

        public void ResolveReferenceGuids()
        {
            // no refs
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }

    [Serializable]
    public class BodyPropertySerializer : ICustomSerializer
    {
        string data;
        public BodyPropertySerializer(BodyProperties bodyProperties)
        {
            data = bodyProperties.ToString();
        }

        public object Deserialize()
        {
            BodyProperties newProp = new BodyProperties();
            BodyProperties.FromString(data, out newProp);
            return newProp;
        }

        public void ResolveReferenceGuids()
        {
            // No refs
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }
}