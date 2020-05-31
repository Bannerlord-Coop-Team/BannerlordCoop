using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

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
            Type instanceType = memberInfo.DeclaringType;
            ParameterExpression arg0 = Expression.Parameter(typeof(TDeclaring), "arg0");

            // `TDeclaring` might be a base class or interface of `instanceType`.
            MemberExpression memberAccess = null;
            if (instanceType == typeof(TDeclaring))
            {
                memberAccess = Expression.MakeMemberAccess(arg0, memberInfo);
            }
            else
            {
                // The member resides in `targetType` => convert
                UnaryExpression arg0Converted = Expression.Convert(arg0, instanceType);
                memberAccess = Expression.MakeMemberAccess(arg0Converted, memberInfo);
            }

            UnaryExpression body = Expression.Convert(memberAccess, typeof(object));
            Expression<Func<TDeclaring, object>> lambda =
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
            Type instanceType = memberInfo.DeclaringType;
            ParameterExpression arg0 = Expression.Parameter(typeof(TDeclaring), "arg0");

            MemberExpression memberAccess = null;
            // `TDeclaring` might be a base class or interface of `instanceType`.
            if (instanceType == typeof(TDeclaring))
            {
                memberAccess = Expression.MakeMemberAccess(arg0, memberInfo);
            }
            else
            {
                // The member resides in `targetType` => convert
                UnaryExpression arg0Converted = Expression.Convert(arg0, instanceType);
                memberAccess = Expression.MakeMemberAccess(arg0Converted, memberInfo);
            }

            ParameterExpression arg1 = Expression.Parameter(typeof(object), "arg1");
            UnaryExpression exConvertToUnderlying = Expression.Convert(
                arg1,
                memberInfo.GetUnderlyingType());
            BinaryExpression body = Expression.Assign(memberAccess, exConvertToUnderlying);
            Expression<Action<TDeclaring, object>> lambda =
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
            Type instanceType = method.DeclaringType;
            ParameterExpression arg0 = Expression.Parameter(typeof(TDeclaring), "arg0");

            MethodCallExpression exCall = null;
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
                UnaryExpression arg0Converted = Expression.Convert(arg0, instanceType);
                exCall = Expression.Call(arg0Converted, method);
            }

            UnaryExpression body = Expression.Convert(exCall, typeof(object));

            Expression<Func<TDeclaring, object>> lambda =
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
            Type instanceType = method.DeclaringType;
            ParameterExpression arg0 = Expression.Parameter(typeof(TDeclaring), "arg0");
            ParameterExpression arg1 = Expression.Parameter(typeof(TParam), "arg1");

            UnaryExpression exConvertToParam0 = Expression.Convert(
                arg1,
                method.GetParameters()[0].ParameterType);

            MethodCallExpression exCall = null;
            // `TDeclaring` might be a base class or interface of `instanceType`.
            if (instanceType == typeof(TDeclaring))
            {
                exCall = Expression.Call(arg0, method, exConvertToParam0);
            }
            else if (method.IsDefined(typeof(ExtensionAttribute), false))
            {
                UnaryExpression exConvertToParam1 = Expression.Convert(
                    arg1,
                    method.GetParameters()[1].ParameterType);
                exCall = Expression.Call(null, method, arg0, exConvertToParam1);
            }
            else
            {
                // The member resides in `targetType` => convert
                UnaryExpression arg0Converted = Expression.Convert(arg0, instanceType);
                exCall = Expression.Call(arg0Converted, method, exConvertToParam0);
            }

            Expression<Action<TDeclaring, TParam>> lambda =
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
            ConstantExpression exInstance = Expression.Constant(instance);
            ParameterExpression exParameter0 = Expression.Parameter(typeof(TDeclaring), "buffer");

            MethodCallExpression exBody = Expression.Call(exInstance, method, exParameter0);
            UnaryExpression exConvertToObject = Expression.Convert(exBody, typeof(object));

            Expression<Func<TDeclaring, object>> lambda =
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
            ConstantExpression exInstance = Expression.Constant(instance);
            ParameterExpression exParameter0 = Expression.Parameter(typeof(TDeclaring), "buffer");
            ParameterExpression exParameter1 = Expression.Parameter(typeof(object), "p1");
            UnaryExpression exConvertParam1 = Expression.Convert(
                exParameter1,
                method.GetParameters()[1].ParameterType);
            MethodCallExpression exBody = Expression.Call(
                exInstance,
                method,
                exParameter0,
                exConvertParam1);
            Expression<Action<TDeclaring, object>> lambda =
                Expression.Lambda<Action<TDeclaring, object>>(exBody, exParameter0, exParameter1);
            return lambda.Compile();
        }

        public static Func<TValue> CreateGetter<TValue>(MemberInfo memberInfo, object instance)
        {
            ConstantExpression exInstance = Expression.Constant(instance);
            MemberExpression exMemberAccess = Expression.MakeMemberAccess(exInstance, memberInfo);
            UnaryExpression body = Expression.Convert(exMemberAccess, typeof(TValue));
            return Expression.Lambda<Func<TValue>>(body).Compile();
        }

        public static Action<object, object[]> CreateStandInCaller(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            ParameterExpression argInstance = Expression.Parameter(typeof(object), "instance");
            UnaryExpression argInstanceConverted = Expression.Convert(
                argInstance,
                parameters[0].ParameterType);
            ParameterExpression args = Expression.Parameter(typeof(object[]), "args");

            // Unpack parameters
            List<Expression> exArgs = new List<Expression>();
            exArgs.Add(argInstanceConverted);
            for (int i = 1; i < method.GetParameters().Length; ++i)
            {
                ParameterInfo param = method.GetParameters()[i];
                BinaryExpression arrayElement = Expression.ArrayIndex(
                    args,
                    Expression.Constant(i - 1));
                UnaryExpression arrayElementConverted =
                    Expression.Convert(arrayElement, param.ParameterType);
                exArgs.Add(arrayElementConverted);
            }

            // Standins are always static with the first argument being the instance.
            MethodCallExpression exCall = Expression.Call(null, method, exArgs);

            Expression<Action<object, object[]>> lambda =
                Expression.Lambda<Action<object, object[]>>(exCall, argInstance, args);
            return lambda.Compile();
        }

        public static Action<object[]> CreateStaticStandInCaller(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            ParameterExpression args = Expression.Parameter(typeof(object[]), "args");

            // Unpack parameters
            List<Expression> exArgs = new List<Expression>();
            for (int i = 0; i < method.GetParameters().Length; ++i)
            {
                ParameterInfo param = method.GetParameters()[i];
                BinaryExpression arrayElement = Expression.ArrayIndex(args, Expression.Constant(i));
                UnaryExpression arrayElementConverted =
                    Expression.Convert(arrayElement, param.ParameterType);
                exArgs.Add(arrayElementConverted);
            }

            // Standins are always static with the first argument being the instance.
            MethodCallExpression exCall = Expression.Call(null, method, exArgs);

            Expression<Action<object[]>> lambda = Expression.Lambda<Action<object[]>>(exCall, args);
            return lambda.Compile();
        }

        public static DynamicMethod CreateStandIn(SyncMethod method)
        {
            List<Type> parameters = method.MemberInfo.GetParameters()
                                          .Select(info => info.ParameterType)
                                          .ToList();
            if (!method.MemberInfo.IsStatic)
            {
                parameters.Insert(
                    0,
                    method.MemberInfo.DeclaringType); // First argument is the instance
            }

            DynamicMethod dyn = new DynamicMethod(
                "Original",
                MethodAttributes.Static | MethodAttributes.Public,
                CallingConventions.Standard,
                method.MemberInfo.ReturnType,
                parameters.ToArray(),
                method.MemberInfo.DeclaringType,
                true);

            // The standin as it is will never be called. But it still needs a body for the reverse patching.
            ILGenerator il = dyn.GetILGenerator(64);
            il.ThrowException(typeof(NotImplementedException));

            return dyn;
        }
    }
}
