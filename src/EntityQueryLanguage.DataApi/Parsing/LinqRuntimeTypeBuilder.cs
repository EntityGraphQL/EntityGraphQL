using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using EntityQueryLanguage.Extensions;

namespace EntityQueryLanguage.DataApi.Parsing
{
    public static class LinqRuntimeTypeBuilder
    {
        private static AssemblyName _assemblyName = new AssemblyName() { Name = "Eql.DynamicTypes" };
        private static ModuleBuilder _moduleBuilder = null;
        private static Dictionary<string, Type> builtTypes = new Dictionary<string, Type>();
        static LinqRuntimeTypeBuilder()
        {
            _moduleBuilder = AssemblyBuilder.DefineDynamicAssembly(_assemblyName, AssemblyBuilderAccess.Run).DefineDynamicModule(_assemblyName.Name);
        }

        private static string GetTypeKey(Dictionary<string, Type> fields)
        {
            //TODO: optimize the type caching -- if fields are simply reordered, that doesn't mean that they're actually different types, so this needs to be smarter
            string key = string.Empty;
            foreach (var field in fields)
            {
                key = MakeKey(key, field.Value);
            }

            return $":eql_anon:{key}";
        }

        private static string MakeKey(string key, Type field)
        {
            if (field.IsNullableType())
                key += "N" + field.GetGenericArguments()[0].Name;
            else if (field.IsEnumerable())
                key += "L" + field.GetGenericArguments()[0].Name;
            else
                key += field.Name;

            return key;
        }

        public static Type GetDynamicType(Dictionary<string, Type> fields)
        {
            if (null == fields)
                throw new ArgumentNullException("fields");
            if (0 == fields.Count)
                throw new ArgumentOutOfRangeException("fields", "fields must have at least 1 field definition");

            try
            {
                Monitor.Enter(builtTypes);
                string className = GetTypeKey(fields);

                if (builtTypes.ContainsKey(className))
                    return builtTypes[className];

                var typeBuilder = _moduleBuilder.DefineType(className, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable);

                foreach (var field in fields)
                    typeBuilder.DefineField(field.Key, field.Value, FieldAttributes.Public);

                builtTypes[className] = typeBuilder.CreateTypeInfo().AsType();
                return builtTypes[className];
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex);
            }
            finally
            {
                Monitor.Exit(builtTypes);
            }

            return null;
        }
    }
}