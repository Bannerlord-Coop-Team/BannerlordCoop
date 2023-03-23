using GameInterface.Serialization.External;
using GameInterface.Serialization;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;
using Common.Serialization;

namespace Missions.Services.Network.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public readonly struct Mat3Surrogate
    {
        [ProtoMember(1)]
        public byte[] data { get; }

        public Mat3Surrogate(Mat3 obj)
        {
            var factory = new BinaryPackageFactory();
            var orientation = new Mat3BinaryPackage(obj, factory);
            orientation.Pack();

            data = BinaryFormatterSerializer.Serialize(orientation);
        }

        private Mat3 Deserialize()
        {
            var factory = new BinaryPackageFactory();
            var orientation = BinaryFormatterSerializer.Deserialize<Mat3BinaryPackage>(data);
            orientation.BinaryPackageFactory = factory;

            return orientation.Unpack<Mat3>();
        }

        public static implicit operator Mat3Surrogate(Mat3 obj)
        {
            return new Mat3Surrogate(obj);
        }

        public static implicit operator Mat3(Mat3Surrogate surrogate)
        {
            return surrogate.Deserialize();
        }
    }
}
