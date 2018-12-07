using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema
{
    public interface ISchemaType
    {
        Type ContextType { get; }
        string Name { get; }

        Field GetField(string identifier, params string[] arguments);
        IEnumerable<Field> GetFields();
        bool HasField(string identifier, params string[] arguments);
        void AddFields(List<Field> fields);
        void AddField(Field field);
        bool HasFieldByNameOnly(string identifier);
        IEnumerable<Field> GetFieldsByNameOnly(string identifier);
    }

    public class MutationType : IMethodType
    {
        private readonly ISchemaType returnType;
        private readonly object mutationClassInstance;
        private readonly MethodInfo method;
        private Dictionary<string, Type> argumentTypes = new Dictionary<string, Type>();
        private object argInstance;

        public object Call(object[] args, Dictionary<string, ExpressionResult> gqlRequestArgs)
        {
            var allArgs = args.ToList();
            AssignArgValues(gqlRequestArgs);
            allArgs.Add(argInstance);
            var result = method.Invoke(mutationClassInstance, allArgs.ToArray());
            return result;
        }

        private void AssignArgValues(Dictionary<string, ExpressionResult> gqlRequestArgs)
        {
            Type argType = argInstance.GetType();
            foreach (var key in gqlRequestArgs.Keys)
            {
                var foundProp = false;
                foreach (var prop in argType.GetProperties())
                {
                    if (key.ToLower() == prop.Name.ToLower())
                    {
                        object value = GetValue(gqlRequestArgs, prop, prop.PropertyType);
                        prop.SetValue(argInstance, value);
                        foundProp = true;
                    }
                }
                if (!foundProp)
                {
                    foreach (var field in argType.GetFields())
                    {
                        if (key.ToLower() == field.Name.ToLower())
                        {
                            object value = GetValue(gqlRequestArgs, field, field.FieldType);
                            field.SetValue(argInstance, value);
                            foundProp = true;
                        }
                    }
                }
                if (!foundProp)
                {
                    throw new EntityQuerySchemaError($"Could not find property or field {key} on in schema object {argType.Name}");
                }
            }
        }

        /// <summary>
        /// Used at runtime below
        /// </summary>
        /// <param name="input"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private static List<T> ConvertArray<T>(Array input)
        {
            return input.Cast<T>().ToList(); // Using LINQ for simplicity
        }

        private object GetValue(Dictionary<string, ExpressionResult> gqlRequestArgs, MemberInfo member, Type memberType)
        {
            object value = Expression.Lambda(gqlRequestArgs[member.Name]).Compile().DynamicInvoke();
            if (value != null)
            {
                Type type = value.GetType();
                if (type.IsArray && memberType.IsEnumerable())
                {
                    var arr = (Array)value;
                    var convertMethod = typeof(MutationType).GetMethod("ConvertArray", BindingFlags.NonPublic | BindingFlags.Static);
                    var generic = convertMethod.MakeGenericMethod(new[] {memberType.GetGenericArguments()[0]});
                    value = generic.Invoke(null, new object[] { value });
                }
                else if (type == typeof(Newtonsoft.Json.Linq.JObject))
                {
                    value = ((Newtonsoft.Json.Linq.JObject)value).ToObject(memberType);
                }
                else
                {
                    value = ExpressionUtil.ChangeType(value, memberType);
                }
            }
            return value;
        }

        public Type ContextType => ReturnType.ContextType;

        public string Name => ReturnType.Name;

        public ISchemaType ReturnType => returnType;

        public MutationType(ISchemaType returnType, object mutationClassInstance, MethodInfo method)
        {
            this.returnType = returnType;
            this.mutationClassInstance = mutationClassInstance;
            this.method = method;

            var methodArg = method.GetParameters().ElementAt(1);
            this.argInstance = Activator.CreateInstance(methodArg.ParameterType);
        }

        public void AddField(Field field)
        {
            throw new NotImplementedException();
        }

        public void AddFields(List<Field> fields)
        {
            throw new NotImplementedException();
        }

        public Field GetField(string identifier, params string[] arguments)
        {
            return ReturnType.GetField(identifier, arguments);
        }

        public IEnumerable<Field> GetFields()
        {
            return ReturnType.GetFields();
        }

        public bool HasField(string identifier, params string[] arguments)
        {
            return ReturnType.HasField(identifier, arguments);
        }

        public Type GetArgumentType(string argName)
        {
            return argumentTypes[argName];
        }
    }

    public class SchemaType<TBaseType> : ISchemaType
    {
        public Type ContextType { get; protected set; }
        public string Name { get; protected set; }
        private string _description;
        private Dictionary<string, Field> _fieldsByKey = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<Field>> _fieldsByName = new Dictionary<string, List<Field>>(StringComparer.OrdinalIgnoreCase);
        private readonly Expression<Func<TBaseType, bool>> _filter;

        public SchemaType(string name, string description, Expression<Func<TBaseType, bool>> filter = null)
        {
            ContextType = typeof(TBaseType);
            Name = name;
            _description = description;
            _filter = filter;
            AddField("__typename", t => name, "Type name");
        }

        /// <summary>
        /// Add all public Properties and Fields from the base type
        /// </summary>
        public void AddAllFields()
        {
            BuildFieldsFromBase(typeof(TBaseType));
        }
        public void AddFields(List<Field> fields)
        {
            foreach (var f in fields)
            {
                AddField(f);
            }
        }
        public void AddField<TReturn>(Expression<Func<TBaseType, TReturn>> fieldSelection, string description, string returnSchemaType = null)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            AddField(exp.Member.Name, fieldSelection, description, returnSchemaType);
        }
        public void AddField(Field field)
        {
            if (_fieldsByKey.ContainsKey(field.Key))
                throw new EntityQuerySchemaError($"Field {field.Name} already exists on type {this.Name} with the same argument names. Use ReplaceField() if this is intended.");

            _fieldsByKey.Add(field.Key, field);
            if (!_fieldsByName.ContainsKey(field.Name))
                _fieldsByName.Add(field.Name, new List<Field>());
            _fieldsByName[field.Name].Add(field);
        }
        public void AddField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> fieldSelection, string description, string returnSchemaType = null)
        {
            var field = new Field(name, fieldSelection, description, returnSchemaType);
            this.AddField(field);
        }

        /// <summary>
        /// Add a field with arguments.
        ///     field(arg: val)
        /// </summary>
        /// <param name="name">Field name</param>
        /// <param name="argTypes">Anonymous object defines the names and types of each argument</param>
        /// <param name="selectionExpression">The expression that selects the data from TBaseType using the arguments</param>
        /// <param name="returnSchemaType">The schema type to return, it defines the fields available on the return object. If null, defaults to TReturn type mapped in the schema.</param>
        /// <typeparam name="TParams">Type describing the arguments</typeparam>
        /// <typeparam name="TReturn">The return entity type that is mapped to a type in the schema</typeparam>
        /// <returns></returns>
        public void AddField<TParams, TReturn>(string name, TParams argTypes, Expression<Func<TBaseType, TParams, TReturn>> selectionExpression, string description, string returnSchemaType = null)
        {
            var field = new Field(name, selectionExpression, description, returnSchemaType, argTypes);
            this.AddField(field);
        }

        /// <summary>
        /// Replaces a field by Name and Argument Names (that is the key)
        /// </summary>
        /// <param name="name"></param>
        /// <param name="argTypes"></param>
        /// <param name="selectionExpression"></param>
        /// <param name="description"></param>
        /// <param name="returnSchemaType"></param>
        /// <typeparam name="TParams"></typeparam>
        /// <typeparam name="TReturn"></typeparam>
        /// <returns></returns>
        public void ReplaceField<TParams, TReturn>(string name, TParams argTypes, Expression<Func<TBaseType, TParams, TReturn>> selectionExpression, string description, string returnSchemaType = null)
        {
            var field = new Field(name, selectionExpression, description, returnSchemaType, argTypes);
            var oldField = _fieldsByKey.ContainsKey(field.Key) ? _fieldsByKey[field.Key] : null;
            _fieldsByKey[field.Key] = field;
            if (oldField != null && _fieldsByName.ContainsKey(field.Name))
            {
                _fieldsByName[field.Name].Remove(oldField);
            }
        }

        /// <summary>
        /// Checks for a field by name only. There could be multiple fields with the same name but different arguments (overloads)
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public bool HasFieldByNameOnly(string identifier)
        {
            return _fieldsByName.ContainsKey(identifier);
        }

        public IEnumerable<Field> GetFieldsByNameOnly(string identifier)
        {
            return _fieldsByName[identifier];
        }

        private void BuildFieldsFromBase(Type contextType)
        {
            foreach (var f in ContextType.GetProperties())
            {
                if (!_fieldsByKey.ContainsKey(f.Name))
                {
                    var parameter = Expression.Parameter(ContextType);
                    this.AddField(new Field(f.Name, Expression.Lambda(Expression.Property(parameter, f.Name), parameter), string.Empty, string.Empty));
                }
            }
            foreach (var f in ContextType.GetFields())
            {
                if (!_fieldsByKey.ContainsKey(f.Name))
                {
                    var parameter = Expression.Parameter(ContextType);
                    this.AddField(new Field(f.Name, Expression.Lambda(Expression.Field(parameter, f.Name), parameter), string.Empty, string.Empty));
                }
            }
        }

        public Field GetField(string identifier, params string[] arguments)
        {
            var key = Field.MakeFieldKey(identifier, arguments);
            if (_fieldsByKey.ContainsKey(key))
                return _fieldsByKey[key];
            // they could be looking for a field that has default argument values
            if (_fieldsByName.ContainsKey(identifier))
            {
                var probableFields = _fieldsByName[identifier].Where(f => f.RequiredArgumentNames.All(r => arguments.Contains(r)));
                if (probableFields.Count() > 1)
                {
                    throw new EntityGraphQLCompilerException($"Field {identifier}({string.Join(", ", arguments)}) is ambiguous, please provide more arguments. Possible fields {ListFields(identifier)}");
                }
                return probableFields.First();
            }
            throw new EntityGraphQLCompilerException($"Field {identifier}({string.Join(", ", arguments)}) not found");
        }

        private string ListFields(string identifier)
        {
            var fields = _fieldsByName[identifier].Select(f => f.Name + "(" + string.Join(", ", f.ArgumentNames) + ")");
            return string.Join(", ", fields);
        }

        public IEnumerable<Field> GetFields()
        {
            return _fieldsByKey.Values;
        }
        /// <summary>
        /// Checks if type has a field with the given name and the given arguments
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public bool HasField(string identifier, params string[] arguments)
        {
            var key = Field.MakeFieldKey(identifier, arguments);
            return _fieldsByKey.ContainsKey(key);
        }

        public void RemoveField(string name, params string[] arguments)
        {
            var key = Field.MakeFieldKey(name, arguments);
            if (_fieldsByKey.ContainsKey(key))
            {
                var oldField = _fieldsByKey[key];
                _fieldsByKey.Remove(key);
                if (_fieldsByName.ContainsKey(name))
                {
                    _fieldsByName[name].Remove(oldField);
                }
            }
        }
        public void RemoveField(Expression<Func<TBaseType, object>> fieldSelection)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            RemoveField(exp.Member.Name);
        }
    }
}