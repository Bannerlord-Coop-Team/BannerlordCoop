using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameInterface.Registry.Auto;
internal class NotificationFactory
{
    public static IEnumerable<IEvent> CreateNotifications(Type type, string instanceId, object instance)
    {
        List<IEvent> notifications = new List<IEvent>();

        notifications.AddRange(GetInterfaceNotifications(type, instanceId, instance));

        if (type.BaseType != typeof(object) && type.BaseType != null)
        {
            notifications.AddRange(CreateBaseTypeNotifications(type.BaseType, instanceId, instance));
        }

        return notifications;
    }

    private static IEnumerable<IEvent> CreateBaseTypeNotifications(Type type, string instanceId, object instance)
    {
        List<IEvent> notifications = new List<IEvent>
        {
            CreateNotification(type, instanceId, instance)
        };

        notifications.AddRange(GetInterfaceNotifications(type, instanceId, instance));

        // Add base type if one exists
        if (type.BaseType != typeof(object) && type.BaseType != null)
        {
            notifications.AddRange(CreateBaseTypeNotifications(type.BaseType, instanceId, instance));
        }

        return notifications;
    }

    private static IEnumerable<IEvent> GetInterfaceNotifications(Type type, string instanceId, object instance)
    {
        return type.GetInterfaces().Select(interfaceType => CreateNotification(interfaceType, instanceId, instance));
    }

    private static IEvent CreateNotification(Type type, string instanceId, object instance)
    {
        return (IEvent)typeof(NotifyInstanceCreated<>).MakeGenericType(type)
                .GetConstructor(new Type[] { typeof(string), type })
                .Invoke(new object[] { instanceId, instance });
    }
}
