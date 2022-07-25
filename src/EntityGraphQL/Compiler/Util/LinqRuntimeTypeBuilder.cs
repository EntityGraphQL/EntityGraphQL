using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace EntityGraphQL.Compiler.Util
{
    /// <summary>
    /// Builds .NET types at runtime and caches them to be reused
    /// </summary>
    public static class LinqRuntimeTypeBuilder
    {
        private static readonly AssemblyName assemblyName = new() { Name = "EntityGraphQL.DynamicTypes" };
        private static readonly ModuleBuilder moduleBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run).DefineDynamicModule(assemblyName.Name);
        private static readonly Dictionary<string, Type> builtTypes = new();
        // We build a class name based on all the selected fields so we can cache the anonymous types we built
        // Names can't be > 1024 length, so we store them against a shorter Guid string
        private static readonly Dictionary<string, string> typesByFullName = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetTypeKey(Dictionary<string, Type> fields)
        {
            return fields.OrderBy(f => f.Key).Aggregate("Dynamic_", (current, field) => current + field.Key + field.Value.GetHashCode());
        }

        /// <summary>
        /// Build a dynamic type based on the fields. Types are cached so they only are created once
        /// </summary>
        /// <param name="fields">Field names and the type of the field.</param>
        /// <param name="description">An optional description string. Helps with debugging - e.g. the field the type is built for</param>
        /// <param name="parentType">If the type inherits from another type</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Type GetDynamicType(Dictionary<string, Type> fields, string description, Type? parentType = null)
        {
            if (null == fields)
                throw new ArgumentNullException(nameof(fields));
          
            string classFullName = GetTypeKey(fields) + parentType?.Name.GetHashCode();
            lock (typesByFullName)
            {
                if (!typesByFullName.ContainsKey(classFullName))
                {
                    typesByFullName[classFullName] = $"Dynamic_{(description != null ? $"{description}_" : "")}{Guid.NewGuid()}";
                }
                var classId = typesByFullName[classFullName];

                if (builtTypes.ContainsKey(classId))
                    return builtTypes[classId];

                var typeBuilder = moduleBuilder.DefineType(classId.ToString(), TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable, parentType);

                foreach (var field in fields)
                {
                    if (parentType != null && parentType.GetField(field.Key) != null)
                        continue;

                    typeBuilder.DefineField(field.Key, field.Value, FieldAttributes.Public);
                }

                builtTypes[classId] = typeBuilder.CreateTypeInfo()!.AsType();
                return builtTypes[classId];
            }
        }
    }
}