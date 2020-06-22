﻿using System;
using System.Reflection;

namespace CustomAvatar.Utilities
{
    internal static class ReflectionExtensions
    {
        internal static T GetFieldValue<T>(this object obj, string fieldName)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            if (string.IsNullOrEmpty(fieldName)) throw new ArgumentNullException(nameof(fieldName));

            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);

            if (field == null)
            {
                throw new InvalidOperationException($"Public instance field '{fieldName}' does not exist");
            }

            return (T) field.GetValue(obj);
        }

        internal static TResult GetPrivateField<TResult>(this object obj, string fieldName)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Static);

            if (field == null)
            {
                throw new InvalidOperationException($"Private instance field '{fieldName}' does not exist on {obj.GetType().FullName}");
            }

            return (TResult) field.GetValue(obj);
        }

        internal static void SetPrivateField<TSubject>(this TSubject obj, string fieldName, object value)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            FieldInfo field = typeof(TSubject).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Static);

            if (field == null)
            {
                throw new InvalidOperationException($"Private instance field '{fieldName}' does not exist on {typeof(TSubject).FullName}");
            }

            field.SetValue(obj, value);
        }

        internal static void InvokePrivateMethod<TSubject>(this TSubject obj, string methodName, params object[] args)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            MethodInfo method = typeof(TSubject).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null)
            {
                throw new InvalidOperationException($"Private instance method '{methodName}' does not exist on {typeof(TSubject).FullName}");
            }

            method.Invoke(obj, args);
        }

        internal static TDelegate CreatePrivateMethodDelegate<TDelegate>(this Type type, string methodName) where TDelegate : Delegate
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);

            if (method == null)
            {
                throw new InvalidOperationException($"Method '{methodName}' does not exist on {type.FullName}");
            }

            return (TDelegate) Delegate.CreateDelegate(typeof(TDelegate), method);
        }
    }
}
