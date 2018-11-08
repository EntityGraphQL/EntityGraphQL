using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityQueryLanguage.Compiler;
using EntityQueryLanguage.Extensions;

namespace EntityQueryLanguage.Schema
{
    public interface ISchemaType
    {
        Type ContextType { get; }
        string Name { get; }

        Field GetField(string identifier);
        IEnumerable<Field> GetFields();
        bool HasField(string identifier);
        void AddFields(List<Field> fields);
        void AddField(Field field);
    }

    public class MutationType : ISchemaType, IMethodType
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

        private static List<T> ConvertArray<T>(Array input)
        {
            return input.Cast<T>().ToList(); // Using LINQ for simplicity
        }

        private void AssignArgValues(Dictionary<string, ExpressionResult> gqlRequestArgs)
        {
            Type argType = argInstance.GetType();
            foreach (var key in gqlRequestArgs.Keys)
            {
                var foundProp = false;
                foreach (var prop in argType.GetProperties())
                {
                    if (key == prop.Name)
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
                        if (key == field.Name)
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

        public Field GetField(string identifier)
        {
            return ReturnType.GetField(identifier);
        }

        public IEnumerable<Field> GetFields()
        {
            return ReturnType.GetFields();
        }

        public bool HasField(string identifier)
        {
            return ReturnType.HasField(identifier);
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
        private Dictionary<string, Field> _fields = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
        private readonly Expression<Func<TBaseType, bool>> _filter;

        public SchemaType(string name, string description, Expression<Func<TBaseType, bool>> filter = null)
        {
            ContextType = typeof(TBaseType);
            Name = name;
            _description = description;
            _filter = filter;
            AddField("__typename", t => name, "Type name");
        }

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
            if (_fields.ContainsKey(field.Name))
                throw new EntityQuerySchemaError($"Field {field.Name} already exists on type {this.Name}. Use ReplaceField() if this is intended.");

            _fields.Add(field.Name, field);
        }
        public void AddField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> fieldSelection, string description, string returnSchemaType = null)
        {
            var field = new Field(name, fieldSelection, description, returnSchemaType);
            this.AddField(field);
        }

        public void ReplaceField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> fieldSelection, string description, string returnSchemaType = null)
        {
            var field = new Field(name, fieldSelection, description, returnSchemaType);
            _fields[field.Name] = field;
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

        public void ReplaceField<TParams, TReturn>(string name, TParams argTypes, Expression<Func<TBaseType, TParams, TReturn>> selectionExpression, string description, string returnSchemaType = null)
        {
            var field = new Field(name, selectionExpression, description, returnSchemaType, argTypes);
            _fields[field.Name] = field;
        }

        private void BuildFieldsFromBase(Type contextType)
        {
            foreach (var f in ContextType.GetProperties())
            {
                if (!_fields.ContainsKey(f.Name))
                {
                    var parameter = Expression.Parameter(ContextType);
                    _fields.Add(f.Name, new Field(f.Name, Expression.Lambda(Expression.Property(parameter, f.Name), parameter), string.Empty, string.Empty));
                }
            }
            foreach (var f in ContextType.GetFields())
            {
                if (!_fields.ContainsKey(f.Name))
                {
                    var parameter = Expression.Parameter(ContextType);
                    _fields.Add(f.Name, new Field(f.Name, Expression.Lambda(Expression.Field(parameter, f.Name), parameter), string.Empty, string.Empty));
                }
            }
        }

        public Field GetField(string identifier)
        {
            return _fields[identifier];
        }
        public IEnumerable<Field> GetFields()
        {
            return _fields.Values;
        }
        public bool HasField(string identifier)
        {
            return _fields.ContainsKey(identifier);
        }

        public void RemoveField(string name)
        {
            if (_fields.ContainsKey(name))
                _fields.Remove(name);
        }
        public void RemoveField(Expression<Func<TBaseType, object>> fieldSelection)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            RemoveField(exp.Member.Name);
        }
    }
}