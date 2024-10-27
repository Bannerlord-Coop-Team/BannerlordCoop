using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Scaffolderlord.Exceptions;
using Scaffolderlord.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Scaffolderlord.Helpers
{
	public static class ReflectionHelper
	{
		public static ILogger Logger { get; set; } = NullLogger.Instance;

		public static ServiceTypeInfo GetServiceTypeInfo(string typeFullyQualifiedName, string[]? memberNames = null)
		{
			memberNames ??= Array.Empty<string>();
			Type type = Type.GetType(typeFullyQualifiedName) ?? throw new TypeNotFoundException(typeFullyQualifiedName);

			var serviceTypeInfo = new ServiceTypeInfo(type);

			foreach (var memberName in memberNames)
			{
				var memberInfo = type.GetMember(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
					.FirstOrDefault() ?? throw new MemberNotFoundException(memberName, type?.FullName);

				switch (memberInfo)
				{
					case IEnumerable:
						serviceTypeInfo.Collections.Add(memberInfo);
						break;
					case PropertyInfo propertyInfo:
						_ = propertyInfo.GetSetMethod(true) ?? throw new PropertyWithNoSetterException(memberName, type?.FullName);
						serviceTypeInfo.Properties.Add(propertyInfo);
						break;
					case FieldInfo fieldInfo:
						serviceTypeInfo.Fields.Add(fieldInfo);
						break;
					default:
						Logger.LogWarning("{member} is not a valid property,field or collection and will be ignored", memberInfo.Name);
						break;
				}

			}

			return serviceTypeInfo;
		}

		public static IEnumerable<PropertyInfo> GetPropertiesWithSetters(Type type)
		{
			var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic);

			return properties
				.Where(prop => prop.CanWrite);
		}

		public static T CreateInstance<T>(params object[] paramArray)
		{
			return (T)Activator.CreateInstance(typeof(T), args: paramArray);
		}
	}
}
