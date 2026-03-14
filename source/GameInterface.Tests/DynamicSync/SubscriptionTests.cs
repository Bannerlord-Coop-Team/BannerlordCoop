using Autofac;
using Common.Messaging;
using Common.Network;
using GameInterface.DynamicSync;
using GameInterface.DynamicSync.Builders;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.DynamicSync.Utils;
using HarmonyLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace GameInterface.Tests.DynamicSync;
public class SubscriptionTests
{
    readonly Mock<IMessageBroker> messageBrokerMock = new Mock<IMessageBroker>();
    readonly Mock<IObjectManager> objectManagerMock = new Mock<IObjectManager>();
    readonly Mock<INetwork> networkMock = new Mock<INetwork>();

    readonly IContainer container;
    public SubscriptionTests()
    {
        container = DynamicSyncTestContainerBuilder.Build(objectManagerMock);
    }

    [Fact]
    public void LocalMessageSubscribes()
    {
        var dynamicSyncRegistry = container.Resolve<DynamicSyncRegistry>();

        var fieldInfo = AccessTools.Field(typeof(FieldTestClass), "MyField");

        dynamicSyncRegistry.AddField(fieldInfo);

        var builder = container.Resolve<DynamicSyncBuilder>();

        builder.Build();

        var handlerInstance = CreateHandler(fieldInfo);

        VerifySubscription(fieldInfo);



    }

    private Type GetHandlerType(FieldInfo fieldInfo)
    {
        const string ASM_NAME = "DynamicSync";
        var asm = AppDomain.CurrentDomain.GetAssemblies().Single(asm => asm.GetName().Name == ASM_NAME);

        var types = asm.GetTypes();

        var handlerName = $"{ASM_NAME}.{fieldInfo.DeclaringType.Name}_Handler";

        return asm.GetType(handlerName) ?? throw new NullReferenceException();
    }

    private object CreateHandler(FieldInfo fieldInfo)
    {
        var handlerType = GetHandlerType(fieldInfo);

        return Activator.CreateInstance(handlerType, new object[] { messageBrokerMock.Object, objectManagerMock.Object, networkMock.Object }) 
                ?? throw new NullReferenceException("Unable to create handler");
    }

    private void VerifySubscription(FieldInfo fieldInfo)
    {

        var invocations = messageBrokerMock.Invocations.Where(i =>
            i.Method.Name == "Subscribe" &&
            i.Method.IsGenericMethod
        ).ToArray();

        ;
    }
}
