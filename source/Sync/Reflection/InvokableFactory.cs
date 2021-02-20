using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Sync.Invokable;

namespace Sync.Reflection
{
    public static class InvokableFactory
    {
        /// <summary>
        ///     Returns an untyped getter for a property or field in an instance.
        /// </summary>
        /// <typeparam name="TDeclaring">Type of the instance containing the member.</typeparam>
        /// <param name="memberInfo"></param>
        /// <returns></returns>
        public static Func<TDeclaring, object> CreateUntypedGetter<TDeclaring>(
            MemberInfo memberInfo)
        {
            var instanceType = memberInfo.DeclaringType;
            if (instanceType == null) throw new ArgumentNullException(nameof(memberInfo.DeclaringType));

            var arg0 = Expression.Parameter(typeof(TDeclaring), "arg0");

            // `TDeclaring` might be a base class or interface of `instanceType`.
            MemberExpression memberAccess;
            if (instanceType == typeof(TDeclaring))
            {
                memberAccess = Expression.MakeMemberAccess(arg0, memberInfo);
            }
            else
            {
                // The member resides in `targetType` => convert
                var arg0Converted = Expression.Convert(arg0, instanceType);
                memberAccess = Expression.MakeMemberAccess(arg0Converted, memberInfo);
            }

            var body = Expression.Convert(memberAccess, typeof(object));
            var lambda =
                Expression.Lambda<Func<TDeclaring, object>>(body, arg0);
            return lambda.Compile();
        }

        /// <summary>
        ///     Returns an untyped setter for a property or field in an instance.
        /// </summary>
        /// <typeparam name="TDeclaring">Type of the instance containing the member.</typeparam>
        /// <param name="memberInfo"></param>
        /// <returns></returns>
        public static Action<TDeclaring, object> CreateUntypedSetter<TDeclaring>(
            MemberInfo memberInfo)
        {
            var instanceType = memberInfo.DeclaringType;
            if (instanceType == null) throw new ArgumentNullException(nameof(memberInfo.DeclaringType));

            var arg0 = Expression.Parameter(typeof(TDeclaring), "arg0");

            MemberExpression memberAccess;
            // `TDeclaring` might be a base class or interface of `instanceType`.
            if (instanceType == typeof(TDeclaring))
            {
                memberAccess = Expression.MakeMemberAccess(arg0, memberInfo);
            }
            else
            {
                // The member resides in `targetType` => convert
                var arg0Converted = Expression.Convert(arg0, instanceType);
                memberAccess = Expression.MakeMemberAccess(arg0Converted, memberInfo);
            }

            var arg1 = Expression.Parameter(typeof(object), "arg1");
            var exConvertToUnderlying = Expression.Convert(
                arg1,
                memberInfo.GetUnderlyingType());
            var body = Expression.Assign(memberAccess, exConvertToUnderlying);
            var lambda =
                Expression.Lambda<Action<TDeclaring, object>>(body, arg0, arg1);
            return lambda.Compile();
        }

        /// <summary>
        ///     Returns an member method call of the form `object Method(TDeclaring)`.
        /// </summary>
        /// <typeparam name="TDeclaring">Type of the instance containing the member.</typeparam>
        /// <param name="method"></param>
        /// <returns></returns>
        public static Func<TDeclaring, object> CreateCallWithReturn<TDeclaring>(MethodInfo method)
        {
            var instanceType = method.DeclaringType;
            var arg0 = Expression.Parameter(typeof(TDeclaring), "arg0");

            MethodCallExpression exCall;
            // `TDeclaring` might be a base class or interface of `instanceType`.
            if (instanceType == typeof(TDeclaring))
            {
                exCall = Expression.Call(arg0, method);
            }
            else if (method.IsDefined(typeof(ExtensionAttribute), false))
            {
                exCall = Expression.Call(null, method, arg0);
            }
            else
            {
                // The member resides in `targetType` => convert
                var arg0Converted = Expression.Convert(arg0, instanceType);
                exCall = Expression.Call(arg0Converted, method);
            }

            var body = Expression.Convert(exCall, typeof(object));

            var lambda =
                Expression.Lambda<Func<TDeclaring, object>>(body, arg0);
            return lambda.Compile();
        }

        /// <summary>
        ///     Returns a member method call of the form `void Method(TDeclaring, TParam)`.
        /// </summary>
        /// <typeparam name="TDeclaring">Type of the instance containing the member.</typeparam>
        /// <typeparam name="TParam">Type of the parameter for the call.</typeparam>
        /// <param name="method"></param>
        /// <returns></returns>
        public static Action<TDeclaring, TParam> CreateCall<TDeclaring, TParam>(MethodInfo method)
        {
            var instanceType = method.DeclaringType;
            var arg0 = Expression.Parameter(typeof(TDeclaring), "arg0");
            var arg1 = Expression.Parameter(typeof(TParam), "arg1");

            var exConvertToParam0 = Expression.Convert(
                arg1,
                method.GetParameters()[0].ParameterType);

            MethodCallExpression exCall;
            // `TDeclaring` might be a base class or interface of `instanceType`.
            if (instanceType == typeof(TDeclaring))
            {
                exCall = Expression.Call(arg0, method, exConvertToParam0);
            }
            else if (method.IsDefined(typeof(ExtensionAttribute), false))
            {
                var exConvertToParam1 = Expression.Convert(
                    arg1,
                    method.GetParameters()[1].ParameterType);
                exCall = Expression.Call(null, method, arg0, exConvertToParam1);
            }
            else
            {
                // The member resides in `targetType` => convert
                var arg0Converted = Expression.Convert(arg0, instanceType);
                exCall = Expression.Call(arg0Converted, method, exConvertToParam0);
            }

            var lambda =
                Expression.Lambda<Action<TDeclaring, TParam>>(exCall, arg0, arg1);
            return lambda.Compile();
        }

        /// <summary>
        ///     Returns a member method call on `instance` of the form `object Method(TDeclaring)`.
        /// </summary>
        /// <typeparam name="TDeclaring">Type of the instance containing the member.</typeparam>
        /// <param name="method"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static Func<TDeclaring, object> CreateCallWithReturn<TDeclaring>(
            MethodInfo method,
            object instance)
        {
            var exInstance = Expression.Constant(instance);
            var exParameter0 = Expression.Parameter(typeof(TDeclaring), "buffer");

            var exBody = Expression.Call(exInstance, method, exParameter0);
            var exConvertToObject = Expression.Convert(exBody, typeof(object));

            var lambda =
                Expression.Lambda<Func<TDeclaring, object>>(exConvertToObject, exParameter0);
            return lambda.Compile();
        }

        /// <summary>
        ///     Returns a member method call on `instance`of the form `void Method(TDeclaring, object)`.
        /// </summary>
        /// <typeparam name="TDeclaring">Type of the instance containing the member.</typeparam>
        /// <param name="method"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static Action<TDeclaring, object> CreateCall<TDeclaring>(
            MethodInfo method,
            object instance)
        {
            var exInstance = Expression.Constant(instance);
            var exParameter0 = Expression.Parameter(typeof(TDeclaring), "buffer");
            var exParameter1 = Expression.Parameter(typeof(object), "p1");
            var exConvertParam1 = Expression.Convert(
                exParameter1,
                method.GetParameters()[1].ParameterType);
            var exBody = Expression.Call(
                exInstance,
                method,
                exParameter0,
                exConvertParam1);
            var lambda =
                Expression.Lambda<Action<TDeclaring, object>>(exBody, exParameter0, exParameter1);
            return lambda.Compile();
        }

        public static Func<TValue> CreateGetter<TValue>(MemberInfo memberInfo, object instance)
        {
            var exInstance = Expression.Constant(instance);
            var exMemberAccess = Expression.MakeMemberAccess(exInstance, memberInfo);
            var body = Expression.Convert(exMemberAccess, typeof(TValue));
            return Expression.Lambda<Func<TValue>>(body).Compile();
        }

        public static Func<TDeclaring, TValue> CreateGetter<TDeclaring, TValue>(MemberInfo memberInfo)
        {
            var instanceType = memberInfo.DeclaringType;
            var arg0 = Expression.Parameter(typeof(TDeclaring), "arg0");
            var exMemberAccess = Expression.MakeMemberAccess(arg0, memberInfo);
            var body = Expression.Convert(exMemberAccess, typeof(TValue));
            return Expression.Lambda<Func<TDeclaring, TValue>>(body, arg0).Compile();
        }

        public static Action<object, object[]> CreateStandInCaller(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var argInstance = Expression.Parameter(typeof(object), "instance");
            var argInstanceConverted = Expression.Convert(
                argInstance,
                parameters[0].ParameterType);
            var args = Expression.Parameter(typeof(object[]), "args");

            // Unpack parameters
            var exArgs = new List<Expression>
            {
                argInstanceConverted
            };
            for (var i = 1; i < method.GetParameters().Length; ++i)
            {
                var param = method.GetParameters()[i];
                var arrayElement = Expression.ArrayIndex(
                    args,
                    Expression.Constant(i - 1));
                var arrayElementConverted =
                    Expression.Convert(arrayElement, param.ParameterType);
                exArgs.Add(arrayElementConverted);
            }

            // Standins are always static with the first argument being the instance.
            var exCall = Expression.Call(null, method, exArgs);

            var lambda =
                Expression.Lambda<Action<object, object[]>>(exCall, argInstance, args);
            return lambda.Compile();
        }

        public static Action<object[]> CreateStaticStandInCaller(MethodInfo method)
        {
            var args = Expression.Parameter(typeof(object[]), "args");

            // Unpack parameters
            var exArgs = new List<Expression>();
            for (var i = 0; i < method.GetParameters().Length; ++i)
            {
                var param = method.GetParameters()[i];
                var arrayElement = Expression.ArrayIndex(args, Expression.Constant(i));
                var arrayElementConverted =
                    Expression.Convert(arrayElement, param.ParameterType);
                exArgs.Add(arrayElementConverted);
            }

            // Standins are always static with the first argument being the instance.
            var exCall = Expression.Call(null, method, exArgs);

            var lambda = Expression.Lambda<Action<object[]>>(exCall, args);
            return lambda.Compile();
        }

        public static DynamicMethod CreateStandIn(PatchedInvokable patchedInvokable)
        {
            var parameters = patchedInvokable.Original.GetParameters()
                .Select(info => info.ParameterType)
                .ToList();
            if (!patchedInvokable.Original.IsStatic)
                parameters.Insert(
                    0,
                    patchedInvokable.Original.DeclaringType); // First argument is the instance

            var returnType = patchedInvokable.Original is MethodInfo methodInfo
                ? methodInfo.ReturnType
                : typeof(void);

            var dyn = new DynamicMethod(
                "Original",
                MethodAttributes.Static | MethodAttributes.Public,
                CallingConventions.Standard,
                returnType,
                parameters.ToArray(),
                patchedInvokable.Original.DeclaringType,
                true);

            // The standin as it is will never be called. But it still needs a body for the reverse patching.
            var il = dyn.GetILGenerator();
            il.ThrowException(typeof(StandInNotPatchedException));

            return dyn;
        }
    }

    public class StandInNotPatchedException : Exception
    {
        public StandInNotPatchedException() : base(
            "Dynamically generated stand in method was not patched by harmony.")
        {
        }
    }
}