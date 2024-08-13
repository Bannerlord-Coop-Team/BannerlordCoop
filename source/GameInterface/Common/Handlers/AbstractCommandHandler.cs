using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Common.Logging;
using Common.Messaging;
using GameInterface.Common.Commands;
using GameInterface.Common.Commands.MBTypes;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Common.Handlers;

public abstract class AbstractCommandHandler<THandlerClass, TTarget> : IHandler where TTarget : class
{
    private static readonly ILogger Logger = LogManager.GetLogger<THandlerClass>();
    
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    private readonly Dictionary<string, Action<TTarget, object>> setterFunctions = new();
    
    public AbstractCommandHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        
        messageBroker.Subscribe<GenericChangeCommand<THandlerClass>>(Handle);
        messageBroker.Subscribe<TextObjectChangeCommand<THandlerClass>>(HandleTextObject);
        messageBroker.Subscribe<GenericChangeCommand<THandlerClass, string>>(HandleEquipmentObject, "Equipment");
        messageBroker.Subscribe<GenericChangeCommand<THandlerClass, long>>(HandleCampaignTimeObject, "CampaignTime");
        messageBroker.Subscribe<GenericChangeCommand<THandlerClass, string>>(HandleTrackedObject, "TrackedObject");

        InitializeSetters();
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GenericChangeCommand<THandlerClass>>(Handle);
        messageBroker.Unsubscribe<TextObjectChangeCommand<THandlerClass>>(HandleTextObject);
        messageBroker.Unsubscribe<GenericChangeCommand<THandlerClass, string>>(HandleEquipmentObject, "Equipment");
        messageBroker.Unsubscribe<GenericChangeCommand<THandlerClass, long>>(HandleCampaignTimeObject, "CampaignTime");
        messageBroker.Unsubscribe<GenericChangeCommand<THandlerClass, string>>(HandleTrackedObject, "TrackedObject");
    }

    private void InitializeSetters()
    {
        var propertyOrFieldNames = GetPropertyOrFieldNames();
        var properties = typeof(TTarget).GetProperties().Where(property => propertyOrFieldNames.Contains(property.Name)).ToList();

        foreach (var property in properties)
        {
            var targetParameter = Expression.Parameter(typeof(TTarget));
            var valueParameter = Expression.Parameter(typeof(object));

            var targetPropertyOrField = Expression.PropertyOrField(targetParameter, property.Name);
            var valueExpression = Expression.Convert(valueParameter, property.PropertyType);

            var functionBinaryExpression = Expression.MakeBinary(ExpressionType.Assign, targetPropertyOrField, valueExpression);
            
            var functionAsExpression = Expression.Lambda<Action<TTarget, object>>(functionBinaryExpression, targetParameter, valueParameter);
            
            setterFunctions.Add(property.Name, functionAsExpression.Compile());
        }
        
        
        var fields = typeof(TTarget).GetFields(BindingFlags.Instance).Where(field => propertyOrFieldNames.Contains(field.Name));

        foreach (var field in fields)
        {
            var targetParameter = Expression.Parameter(typeof(TTarget));
            var valueParameter = Expression.Parameter(typeof(object));

            var targetPropertyOrField = Expression.PropertyOrField(targetParameter, field.Name);
            var valueExpression = Expression.Convert(valueParameter, field.FieldType);

            var functionBinaryExpression = Expression.MakeBinary(ExpressionType.Assign, targetPropertyOrField, valueExpression);
            
            var functionAsExpression = Expression.Lambda<Action<TTarget, object>>(functionBinaryExpression, targetParameter, valueParameter);
            
            setterFunctions.Add(field.Name, functionAsExpression.Compile());
        }
    }

    private void Handle(MessagePayload<GenericChangeCommand<THandlerClass>> payload)
    {
        var data = payload.What;
        DynamicInvoke(data, data.Value);
    }

    private void HandleTextObject(MessagePayload<TextObjectChangeCommand<THandlerClass>> payload)
    {
        var data = payload.What;
        DynamicInvoke(data, new TextObject(data.Value));
    }

    private void HandleEquipmentObject(MessagePayload<GenericChangeCommand<THandlerClass, string>> payload)
    {
        var data = payload.What;
        DynamicInvoke(data, Equipment.CreateFromEquipmentCode(data.Value));
    }

    private void HandleCampaignTimeObject(MessagePayload<GenericChangeCommand<THandlerClass, long>> payload)
    {
        var data = payload.What;
        DynamicInvoke(data, new CampaignTime(data.Value));
    }

    private void HandleTrackedObject(MessagePayload<GenericChangeCommand<THandlerClass, string>> payload)
    {
        var data = payload.What;

        if (objectManager.TryGetObject<object>(data.Value, out var trackedObject))
        {
            DynamicInvoke(data, trackedObject);
        }
    }

    private void DynamicInvoke<T>(T data, object value) where T : ITargetCommand
    {
        if (objectManager.TryGetObject<TTarget>(data.Id, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(TTarget), data.Id);
        }

        setterFunctions.TryGetValue(data.Target, out var func);

        if (func == null)
        {
            Logger.Error($"No setter function defined for {data.Target}");
            return;
        }

        func!.Invoke(instance, value);
    }

    public abstract HashSet<string> GetPropertyOrFieldNames();
}