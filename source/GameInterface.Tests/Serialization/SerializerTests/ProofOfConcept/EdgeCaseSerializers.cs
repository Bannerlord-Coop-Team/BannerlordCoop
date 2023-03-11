using GameInterface.Serialization;
using System;

namespace GameInterface.Tests.Serialization.SerializerTests.ProofOfConcept
{

    [Serializable]
    public class BasicClassBinaryPackage
    {

    }
    

    [Serializable]
    internal class ClassABinaryPackage : BinaryPackageBase<TestClassA>
    {
        [NonSerialized]
        private bool IsPacked = false;

        ClassBBinaryPackage classBPackage;

        public ClassABinaryPackage(TestClassA classA, BinaryPackageFactory packageFactory) 
            : base(classA, packageFactory) { }

        protected override void PackInternal()
        {
            if (IsPacked == true) return;

            IsPacked = true;
            classBPackage = BinaryPackageFactory.GetBinaryPackage<ClassBBinaryPackage>(Object.testClassB);
            classBPackage.Pack();
        }

        protected override void UnpackInternal()
        {
            Object.testClassB = classBPackage.Unpack<TestClassB>();
        }
    }

    [Serializable]
    internal class ClassBBinaryPackage : BinaryPackageBase<TestClassB>
    {
        [NonSerialized]
        private bool IsPacked = false;

        ClassABinaryPackage classAPackage;
        public ClassBBinaryPackage(TestClassB classB, BinaryPackageFactory packageFactory) 
            : base(classB, packageFactory) { }

        protected override void PackInternal()
        {
            if (IsPacked == true) return;

            IsPacked = true;
            classAPackage = BinaryPackageFactory.GetBinaryPackage<ClassABinaryPackage>(Object.testClassA);
            classAPackage.Pack();
        }

        protected override void UnpackInternal()
        {
            Object.testClassA = classAPackage.Unpack<TestClassA>();
        }
    }
}
