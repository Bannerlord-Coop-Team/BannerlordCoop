using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Map;

namespace GameInterface.Utils.AutoSync;

internal interface IAutoSync
{
    public void SyncProperty(PropertyInfo property);
}
internal class AutoSync : IAutoSync
{
    private readonly Harmony harmony;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly PropertySync propertySync = new PropertySync();

    public AutoSync(Harmony harmony, INetwork network, IMessageBroker messageBroker)
    {
        this.harmony = harmony;
        this.network = network;
        this.messageBroker = messageBroker;
    }

    public void SyncProperty(PropertyInfo property)
    {
        //propertySync.SyncProperty(property);



        //var type = property.DeclaringType;
        //var methodName = property.Name;
        //var method = type.GetMethod("set_" + methodName);
        //if (method == null)
        //{
        //    throw new Exception($"Property {methodName} does not have a setter");
        //}
        //var originalMethod = method.GetMethodBody().GetILAsByteArray();
        //var prefix = new HarmonyMethod(typeof(AutoSync), nameof(Prefix));
        //harmony.Patch(method, prefix: prefix);
    }

    private void ValidateProperty(PropertyInfo property)
    {
        if (property.GetSetMethod() == null)
        {
            throw new Exception($"Property {property.Name} does not have a setter, try using a p");
        }
    }



}

public class PropertySync
{
    private static readonly ILogger Logger = LogManager.GetLogger<PropertySync>();

    private readonly Module AutoSyncModule = typeof(PropertySync).Module;

    public void SyncProperty(PropertyInfo property)
    {
        ValidateProperty(property);

        var setMethod = property.GetSetMethod() ?? property.GetSetMethod(true);
        var declaringType = property.DeclaringType;
    }

    private void ValidateProperty(PropertyInfo property)
    {
        var setMethod = property.GetSetMethod() ?? property.GetSetMethod(true);

        if (setMethod == null)
        {
            throw new Exception($"Property {property.Name} does not have a setter, try looking for where the data comes from in the property getter");
        }
    }
}
