using Autofac;
using GameInterface.Serialization;
using GameInterface.Tests.Bootstrap.Modules;
using System;
using Xunit;

namespace GameInterface.Tests.Serialization
{
    public class BinaryPackageFactoryTests
    {
        private readonly IBinaryPackageFactory factory;

        public BinaryPackageFactoryTests()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<SerializationTestModule>();
            factory = builder.Build().Resolve<IBinaryPackageFactory>();
        }

        [Fact]
        public void GetBinaryPackage_PacksCurrentStateOnEachRootCall()
        {
            var value = new MutablePackageValue { Value = 1 };

            var firstPackage = factory.GetBinaryPackage<MutablePackageValueBinaryPackage>(value);
            value.Value = 2;
            var secondPackage = factory.GetBinaryPackage<MutablePackageValueBinaryPackage>(value);

            Assert.NotSame(firstPackage, secondPackage);
            Assert.Equal(1, firstPackage.PackedValue);
            Assert.Equal(2, secondPackage.PackedValue);
        }

        [Fact]
        public void GetBinaryPackage_ReusesPackageWithinRecursiveGraph()
        {
            var value = new RecursivePackageValue();
            value.Self = value;

            var package = factory.GetBinaryPackage<RecursivePackageValueBinaryPackage>(value);

            Assert.Same(package, package.SelfPackage);
        }

        [Fact]
        public void GetBinaryPackage_ClearsPackagesWhenPackingThrows()
        {
            Assert.Throws<InvalidOperationException>(() =>
                factory.GetBinaryPackage(new ThrowingPackageValue()));
            Assert.Throws<InvalidOperationException>(() =>
                factory.GetBinaryPackage(new ThrowingPackageValue()));
        }
    }

    public class MutablePackageValue
    {
        public int Value { get; set; }
    }

    public class MutablePackageValueBinaryPackage : BinaryPackageBase<MutablePackageValue>
    {
        public int PackedValue { get; private set; }

        public MutablePackageValueBinaryPackage(
            MutablePackageValue obj,
            IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            PackedValue = Object.Value;
        }

        protected override void UnpackInternal()
        {
            Object.Value = PackedValue;
        }
    }

    public class RecursivePackageValue
    {
        public RecursivePackageValue Self { get; set; } = null!;
    }

    public class RecursivePackageValueBinaryPackage : BinaryPackageBase<RecursivePackageValue>
    {
        public IBinaryPackage SelfPackage { get; private set; } = null!;

        public RecursivePackageValueBinaryPackage(
            RecursivePackageValue obj,
            IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            SelfPackage = BinaryPackageFactory.GetBinaryPackage(Object.Self);
        }

        protected override void UnpackInternal()
        {
            Object.Self = (RecursivePackageValue)SelfPackage.Unpack(BinaryPackageFactory);
        }
    }

    public class ThrowingPackageValue
    {
    }

    public class ThrowingPackageValueBinaryPackage : BinaryPackageBase<ThrowingPackageValue>
    {
        public ThrowingPackageValueBinaryPackage(
            ThrowingPackageValue obj,
            IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            throw new InvalidOperationException("Packing failed.");
        }

        protected override void UnpackInternal()
        {
        }
    }
}
