using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Schema
{
    public class SchemaType<TBaseType> : ISchemaType
    {
        private readonly ISchemaProvider schema;
        public Type TypeDotnet { get; protected set; }
        public string Name { get; internal set; }
        public bool IsInput { get; }
        public bool IsEnum { get; }
        public bool IsScalar { get; }
        public RequiredAuthorization? RequiredAuthorization { get; set; }

        public string? Description { get; internal set; }

        private readonly Dictionary<string, Field> fieldsByName = new();

        public SchemaType(ISchemaProvider schema, string name, string? description, RequiredAuthorization? requiredAuthorization, bool isInput = false, bool isEnum = false, bool isScalar = false)
            : this(schema, typeof(TBaseType), name, description, requiredAuthorization, isInput, isEnum, isScalar)
        {
        }

        public SchemaType(ISchemaProvider schema, Type dotnetType, string name, string? description, RequiredAuthorization? requiredAuthorization, bool isInput = false, bool isEnum = false, bool isScalar = false)
        {
            this.schema = schema;
            TypeDotnet = dotnetType;
            Name = name;
            Description = description;
            IsInput = isInput;
            IsEnum = isEnum;
            IsScalar = isScalar;
            RequiredAuthorization = requiredAuthorization;
            if (!isScalar)
                AddField("__typename", t => name, "Type name").IsNullable(false);
        }

        /// <summary>
        /// Using reflection, add all the public Fields and Properties from the dotnet type as fields on the 
        /// schema type. Quick helper method to build out schemas
        /// </summary>
        /// <param name="autoCreateNewComplexTypes"></param>
        /// <param name="autoCreateEnumTypes"></param>
        /// <returns>The schema type the fields were added to</returns>
        public ISchemaType AddAllFields(bool autoCreateNewComplexTypes = false, bool autoCreateEnumTypes = true)
        {
            if (IsEnum)
            {
                foreach (var field in TypeDotnet.GetFields())
                {
                    if (field.Name == "value__")
                        continue;

                    var enumName = Enum.Parse(TypeDotnet, field.Name).ToString();
                    var description = (field.GetCustomAttribute(typeof(DescriptionAttribute)) as DescriptionAttribute)?.Description;
                    var schemaField = new Field(schema, enumName, null, description, new GqlTypeInfo(() => schema.GetSchemaType(TypeDotnet), TypeDotnet), schema.AuthorizationService.GetRequiredAuthFromMember(field), schema.SchemaFieldNamer);
                    var obsoleteAttribute = field.GetCustomAttribute<ObsoleteAttribute>();
                    if (obsoleteAttribute != null)
                    {
                        schemaField.IsDeprecated = true;
                        schemaField.DeprecationReason = obsoleteAttribute.Message;
                    }

                    AddField(schemaField);
                }
            }
            else
            {
                var fields = SchemaBuilder.GetFieldsFromObject(TypeDotnet, schema, autoCreateEnumTypes, schema.SchemaFieldNamer, autoCreateNewComplexTypes);
                AddFields(fields);
            }
            return this;
        }
        public void AddFields(List<Field> fields)
        {
            foreach (var f in fields)
            {
                AddField(f);
            }
        }
        public Field AddField(Field field)
        {
            if (fieldsByName.ContainsKey(field.Name))
                throw new EntityQuerySchemaException($"Field {field.Name} already exists on type {this.Name}. Use ReplaceField() if this is intended.");

            fieldsByName.Add(field.Name, field);
            return field;
        }

        /// <summary>
        /// Add a field from a simple member expression type e.g. ctx => ctx.SomeMember. The member name will be converted with fieldNamer for the field name
        /// Throws an exception if the member is not a simple member expression
        /// Throws an exception if the field already exists
        /// </summary>
        /// <typeparam name="TReturn"></typeparam>
        /// <param name="fieldSelection">An expression to resolve the field. Has to be a simple member expression</param>
        /// <param name="description">Description of the field for schema documentation</param>
        /// <returns>The field object to perform further configuration</returns>
        public Field AddField<TReturn>(Expression<Func<TBaseType, TReturn>> fieldSelection, string? description)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            return AddField(schema.SchemaFieldNamer(exp.Member.Name), fieldSelection, description);
        }

        /// <summary>
        /// Add a field with an expression to resolve it.
        /// Throws an exception if the field already exists
        /// </summary>
        /// <typeparam name="TReturn"></typeparam>
        /// <param name="name">Name of the field in the schema. Is used as passed. Case sensitive</param>
        /// <param name="fieldSelection">The expression to resolve the field value from this current schema type. e.g. ctx => ctx.LotsOfPeople.Where(p => p.Age > 50)</param>
        /// <param name="description">Description of the field for schema documentation</param>
        /// <returns>The field object to perform further configuration</returns>
        public Field AddField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> fieldSelection, string? description)
        {
            var requiredAuth = schema.AuthorizationService.GetRequiredAuthFromExpression(fieldSelection);

            var field = new Field(schema, name, fieldSelection, description, SchemaBuilder.MakeGraphQlType(schema, typeof(TReturn), null), requiredAuth, schema.SchemaFieldNamer);
            this.AddField(field);
            return field;
        }

        /// <summary>
        /// Add a field with arguments. and an expression to resolve the value
        ///     field(arg: val)
        /// Throws an exception if the field already exists
        /// </summary>
        /// <typeparam name="TParams"></typeparam>
        /// <typeparam name="TReturn"></typeparam>
        /// <param name="name">Name of the field in the schema. Is used as passed. Case sensitive</param>
        /// <param name="argTypes">An object that represents the arguments available for the field including default values or required fields. Anonymous objects are supported</param>
        /// <param name="fieldSelection">The expression to resolve the field value from this current schema type. e.g. ctx => ctx.LotsOfPeople.Where(p => p.Age > 50)</param>
        /// <param name="description">Description of the field for schema documentation</param>
        /// <returns>The field object to perform further configuration</returns>
        public Field AddField<TParams, TReturn>(string name, TParams argTypes, Expression<Func<TBaseType, TParams, TReturn>> fieldSelection, string? description)
        {
            var requiredAuth = schema.AuthorizationService.GetRequiredAuthFromExpression(fieldSelection);

            var field = new Field(schema, name, fieldSelection, description, argTypes, SchemaBuilder.MakeGraphQlType(schema, typeof(TReturn), null), requiredAuth, schema.SchemaFieldNamer);
            this.AddField(field);
            return field;
        }

        /// <summary>
        /// Replaces a field matching the name with this new field. If the field does not exist, it will be added.
        /// </summary>
        /// <typeparam name="TReturn"></typeparam>
        /// <param name="name">Name of the field in the schema. Is used as passed. Case sensitive</param>
        /// <param name="fieldSelection">The expression to resolve the field value from this current schema type. e.g. ctx => ctx.LotsOfPeople.Where(p => p.Age > 50)</param>
        /// <param name="description">Description of the field for schema documentation</param>
        /// <returns>The field object to perform further configuration</returns>
        public Field ReplaceField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> fieldSelection, string? description)
        {
            var requiredAuth = schema.AuthorizationService.GetRequiredAuthFromExpression(fieldSelection);

            var field = new Field(schema, name, fieldSelection, description, SchemaBuilder.MakeGraphQlType(schema, typeof(TReturn), null), requiredAuth, schema.SchemaFieldNamer);
            fieldsByName[field.Name] = field;
            return field;
        }

        /// <summary>
        /// Replaces a field by name with this new field with arguments. If the field does not exist, it will be added.
        /// </summary>
        /// <typeparam name="TParams"></typeparam>
        /// <typeparam name="TReturn"></typeparam>
        /// <param name="name">Name of the field in the schema. Is used as passed. Case sensitive</param>
        /// <param name="argTypes">An object that represents the arguments available for the field including default values or required fields. Anonymous objects are supported</param>
        /// <param name="fieldSelection">The expression to resolve the field value from this current schema type. e.g. ctx => ctx.LotsOfPeople.Where(p => p.Age > 50)</param>
        /// <param name="description">Description of the field for schema documentation</param>
        /// <returns>The field object to perform further configuration</returns>
        public Field ReplaceField<TParams, TReturn>(string name, TParams argTypes, Expression<Func<TBaseType, TParams, TReturn>> fieldSelection, string? description)
        {
            var requiredAuth = schema.AuthorizationService.GetRequiredAuthFromExpression(fieldSelection);

            var field = new Field(schema, name, fieldSelection, description, argTypes, SchemaBuilder.MakeGraphQlType(schema, typeof(TReturn), null), requiredAuth, schema.SchemaFieldNamer);
            fieldsByName[field.Name] = field;
            return field;
        }

        /// <summary>
        /// Search for a field by name. Use HasField() to check if field exists.
        /// </summary>
        /// <param name="identifier">Field name. Case sensitive</param>
        /// <param name="requestContext">Current request context. Used by EntityGraphQL when compiling queries. If are calling this during schema configure, you can pass null</param>
        /// <returns>The field object for further configuration</returns>
        /// <exception cref="EntityGraphQLAccessException"></exception>
        /// <exception cref="EntityGraphQLCompilerException">If field if not found</exception>
        public Field GetField(string identifier, QueryRequestContext? requestContext)
        {
            if (fieldsByName.ContainsKey(identifier))
            {
                var field = fieldsByName[identifier];
                if (requestContext != null && requestContext.AuthorizationService != null && !requestContext.AuthorizationService.IsAuthorized(requestContext.User, field.RequiredAuthorization))
                {
                    throw new EntityGraphQLAccessException($"You are not authorized to access the '{identifier}' field on type '{Name}'.");
                }
                return fieldsByName[identifier];
            }

            throw new EntityGraphQLCompilerException($"Field {identifier} not found");
        }

        /// <summary>
        /// Get a field by a simple member expression on the real type. The name is changed with fieldNamer
        /// </summary>
        /// <param name="fieldSelection"></param>
        /// <returns>The field object for further configuration</returns>
        public Field GetField(Expression<Func<TBaseType, object>> fieldSelection)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            return GetField(schema.SchemaFieldNamer(exp.Member.Name), null);
        }

        /// <summary>
        /// Return all the fields defined on this type
        /// </summary>
        /// <returns>List of Field objects</returns>
        public IEnumerable<Field> GetFields()
        {
            return fieldsByName.Values;
        }
        /// <summary>
        /// Checks if this type has a field with the given name
        /// </summary>
        /// <param name="identifier">Field name. Case sensitive</param>
        /// <returns></returns>
        public bool HasField(string identifier, QueryRequestContext? requestContext)
        {
            if (fieldsByName.ContainsKey(identifier))
            {
                var field = fieldsByName[identifier];
                if (requestContext != null && requestContext.AuthorizationService != null && !requestContext.AuthorizationService.IsAuthorized(requestContext.User, field.RequiredAuthorization))
                    return false;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Remove a field by the given name. Case sensitive. If the field does not exist, nothing happens.
        /// </summary>
        /// <param name="name"></param>
        public void RemoveField(string name)
        {
            if (fieldsByName.ContainsKey(name))
            {
                fieldsByName.Remove(name);
            }
        }
        /// <summary>
        /// Remove a field by a member expression on the real type. The name is changed with fieldNamer for look up
        /// </summary>
        /// <param name="fieldSelection"></param>
        public void RemoveField(Expression<Func<TBaseType, object>> fieldSelection)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            RemoveField(schema.SchemaFieldNamer(exp.Member.Name));
        }

        /// <summary>
        /// To access this type all roles listed here are required
        /// </summary>
        /// <param name="roles"></param>
        public SchemaType<TBaseType> RequiresAllRoles(params string[] roles)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAllRoles(roles);
            return this;
        }
        /// <summary>
        /// To access this type any of the roles listed is required
        /// </summary>
        /// <param name="roles"></param>
        public SchemaType<TBaseType> RequiresAnyRole(params string[] roles)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAnyRole(roles);
            return this;
        }
        /// <summary>
        /// To access this type all policies listed here are required
        /// </summary>
        /// <param name="policies"></param>
        public SchemaType<TBaseType> RequiresAllPolicies(params string[] policies)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAllPolicies(policies);
            return this;
        }
        /// <summary>
        /// To access this type any of the policies listed is required
        /// </summary>
        /// <param name="policies"></param>
        public SchemaType<TBaseType> RequiresAnyPolicy(params string[] policies)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAnyPolicy(policies);
            return this;
        }
    }
}
