using System;
using GameInterface.Serialization.Dynamic;
using ProtoBuf.Meta;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization
{
    public class DynamicModelGeneratorTests
    {
        /// <summary>
        ///     Check that the dynamic serializer generator is throwing an exception
        ///     when passing an array of fields unused (non-existent) about the item type.
        /// </summary>
        [Fact]
        public void ExcludedFieldNotUsed()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();
            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            var excludedFields = new string[]
            {
                "ItemComponent"
            };
            
            Exception exception = Assert.Throws<Exception>(() => generator.CreateDynamicSerializer<ItemObject>(excludedFields));
            Assert.True(exception != null, exception.Message);
            
        }
        
        /// <summary>
        ///     Check that the dynamic serializer generator is sending no exception when an
        ///     proper excluded field array is being pass.
        /// </summary>
        [Fact]
        public void ExcludedFieldUsed()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();
            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            var excludedFields = new string[]
            {
                "<ItemComponent>k__BackingField"
            };
            
            Exception exception = Record.Exception(() => generator.CreateDynamicSerializer<ItemObject>(excludedFields));
            Assert.True(exception == null);
        }

        /// <summary>
        ///     Check the dynamic serializer generator is working fine when no excluded
        ///     array is being pass.
        /// </summary>
        [Fact]
        public void ExcludedFieldNull()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();
            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);
            
            Exception exception = Record.Exception(() => generator.CreateDynamicSerializer<ItemObject>(null));
            Assert.True(exception == null);
        }
    }
}