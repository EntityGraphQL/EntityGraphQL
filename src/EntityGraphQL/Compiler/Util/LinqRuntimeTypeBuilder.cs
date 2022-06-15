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
        private static readonly Dictionary<Guid, Type> builtTypes = new();
        // We build a class name based on all the selected fields so we can cache the anonymous types we built
        // Names can't be > 1024 length, so we store them against Guids
        private static readonly Dictionary<string, Guid> typesByName = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetTypeKey(Dictionary<string, Type> fields)
        {
            return fields.OrderBy(f => f.Key).Aggregate("anon.", (current, field) => current + field.Key + field.Value.GetHashCode());
        }

        public static Type GetDynamicType(Dictionary<string, Type> fields, string? typeName = null, Type? parentType = null)
        {
            if (null == fields)
                throw new ArgumentNullException(nameof(fields));
            if (0 == fields.Count && parentType == null)
                throw new ArgumentOutOfRangeException(nameof(fields), "fields must have at least 1 field definition");

            string className = typeName != null ? $"anon.{typeName}" : GetTypeKey(fields);
            lock (typesByName)
            {
                if (!typesByName.ContainsKey(className))
                {
                    typesByName[className] = Guid.NewGuid();
                }
                var classId = typesByName[className];

                if (builtTypes.ContainsKey(classId))
                    return builtTypes[classId];

                var typeBuilder = moduleBuilder.DefineType(classId.ToString(), TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable, parentType);
                
                foreach (var field in fields)
                {
                    if (parentType != null && parentType.GetField(field.Key) != null)
                        continue;

                    typeBuilder.DefineField(field.Key, field.Value, FieldAttributes.Public);
                }

                builtTypes[classId] = typeBuilder.CreateTypeInfo().AsType();
                return builtTypes[classId];
            }
        }
    }
}