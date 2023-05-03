using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Common.Extensions
{
    public static class FastInvoke
    {
        public static Func<T, ReturnType> BuildUntypedGetter<T, ReturnType>(this MemberInfo memberInfo)
        {
            if (memberInfo == null) throw new ArgumentNullException("member");

            var targetType = memberInfo.DeclaringType;
            var exInstance = Expression.Parameter(targetType, "instance");

            var exMemberAccess = Expression.MakeMemberAccess(exInstance, memberInfo);
            var lambda = Expression.Lambda<Func<T, ReturnType>>(exMemberAccess, exInstance);

            var action = lambda.Compile();
            return action;
        }

        public static Action<TInstance, TValue> BuildUntypedSetter<TInstance, TValue>(this MemberInfo memberInfo)
        {
            if (memberInfo == null) throw new ArgumentNullException("member");

            var targetType = memberInfo.DeclaringType;
            var exInstance = Expression.Parameter(targetType, "instance");

            var exMemberAccess = Expression.MakeMemberAccess(exInstance, memberInfo);

            var exValue = Expression.Parameter(typeof(TValue), "value");
            var exBody = Expression.Assign(exMemberAccess, exValue);

            var lambda = Expression.Lambda<Action<TInstance, TValue>>(exBody, exInstance, exValue);
            var action = lambda.Compile();
            return action;
        }

        private static Type GetUnderlyingType(this MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Event:
                    return ((EventInfo)member).EventHandlerType;
                case MemberTypes.Field:
                    return ((FieldInfo)member).FieldType;
                case MemberTypes.Method:
                    return ((MethodInfo)member).ReturnType;
                case MemberTypes.Property:
                    return ((PropertyInfo)member).PropertyType;
                default:
                    throw new ArgumentException
                    (
                     "Input MemberInfo must be if type EventInfo, FieldInfo, MethodInfo, or PropertyInfo"
                    );
            }
        }
    }
}
