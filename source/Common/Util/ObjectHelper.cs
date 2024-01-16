using System;
using System.Runtime.Serialization;

namespace Common.Util;

/// <summary>
/// Helper class for creating and manipulating objects
/// </summary>
public class ObjectHelper
{
    /// <summary>
    /// Creates an object skipping the constructor
    /// </summary>
    /// <typeparam name="T">Type of object to create</typeparam>
    /// <returns>New instance of <typeparamref name="T"/></returns>
    public static T SkipConstructor<T>()
    {
        return (T)FormatterServices.GetUninitializedObject(typeof(T));
    }

    /// <summary>
    /// Creates a new object skipping the constructor with provided type
    /// </summary>
    /// <param name="objectType">Type to create</param>
    /// <returns>New instance of <paramref name="objectType"/></returns>
    public static object SkipConstructor(Type objectType)
    {
        return FormatterServices.GetUninitializedObject(objectType);
    }
}
