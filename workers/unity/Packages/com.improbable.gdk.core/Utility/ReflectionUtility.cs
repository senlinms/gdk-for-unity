using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Improbable.Gdk.Core
{
    internal static class ReflectionUtility
    {
        private static readonly List<Type> allNonAbstractTypes = new List<Type>();

        private static readonly Dictionary<InterfaceAndAttributes, List<Type>> propertiesToType =
            new Dictionary<InterfaceAndAttributes, List<Type>>();

        /// <summary>
        ///     Get all non-abstract types that implement a given interface and have the specified attributes
        /// </summary>
        /// <param name="interfaceType"> The interface that should be implemented by each type</param>
        /// <param name="attributes"> An array of attributes that must be present on each type</param>
        public static List<Type> GetNonAbstractTypes(Type interfaceType, params Type[] attributes)
        {
            var properties = new InterfaceAndAttributes(interfaceType, attributes);
            if (!propertiesToType.TryGetValue(properties, out var types))
            {
                types = FindTypesWithProperties(properties);
                propertiesToType.Add(properties, types);
            }

            return types;
        }

        private static List<Type> FindTypesWithProperties(InterfaceAndAttributes properties)
        {
            List<Type> types = new List<Type>();
            foreach (var type in GetAllNonAbstractTypes())
            {
                if (properties.InterfaceType.IsAssignableFrom(type))
                {
                    // Check all specified attributes are present
                    var hasAttributes =
                        properties.Attributes.All(attribute => type.GetCustomAttribute(attribute, true) != null);

                    if (hasAttributes)
                    {
                        types.Add(type);
                    }
                }
            }

            return types;
        }


        private static List<Type> GetAllNonAbstractTypes()
        {
            // If there are no known types then search all loaded assemblies
            if (allNonAbstractTypes.Count > 0)
            {
                return allNonAbstractTypes;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!type.IsAbstract)
                        {
                            allNonAbstractTypes.Add(type);
                        }
                    }
                }
                catch (ReflectionTypeLoadException e)
                {
                    // If the assembly contains types that couldn't be loaded print an error by still move forward with all types it could load
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"Could not load all types from assembly {assembly.FullName}");
                    for (int i = 0; i < e.Types.Length; ++i)
                    {
                        if (e.Types[i] != null)
                        {
                            // Still add loaded types as a best effort
                            if (!e.Types[i].IsAbstract)
                            {
                                allNonAbstractTypes.Add(e.Types[i]);
                            }

                            continue;
                        }

                        sb.AppendLine($"Type \"{e.Types[i]}\" failed with error \"{e.LoaderExceptions[i].Message}\"");
                    }

                    Debug.LogError(sb.ToString());
                }
            }

            return allNonAbstractTypes;
        }

        private readonly struct InterfaceAndAttributes : IEquatable<InterfaceAndAttributes>
        {
            public readonly Type InterfaceType;
            public readonly Type[] Attributes;

            public InterfaceAndAttributes(Type interfaceType, Type[] attributes)
            {
                InterfaceType = interfaceType;
                Attributes = attributes;
            }

            public bool Equals(InterfaceAndAttributes other)
            {
                return Equals(InterfaceType, other.InterfaceType) && Equals(Attributes, other.Attributes);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                {
                    return false;
                }

                return obj is InterfaceAndAttributes other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((InterfaceType != null ? InterfaceType.GetHashCode() : 0) * 397) ^
                        (Attributes != null ? Attributes.GetHashCode() : 0);
                }
            }
        }
    }
}
