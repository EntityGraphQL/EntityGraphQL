using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Schema
{
    public class SchemaType<TBaseType> : BaseSchemaTypeWithFields<Field>
    {
        public override Type TypeDotnet { get; }
        public override bool IsInput { get; }
        public override bool IsEnum { get; }
        public override bool IsScalar { get; }

        public SchemaType(ISchemaProvider schema, string name, string? description, RequiredAuthorization? requiredAuthorization, bool isInput = false, bool isEnum = false, bool isScalar = false)
            : this(schema, typeof(TBaseType), name, description, requiredAuthorization, isInput, isEnum, isScalar)
        {
        }

        public SchemaType(ISchemaProvider schema, Type dotnetType, string name, string? description, RequiredAuthorization? requiredAuthorization, bool isInput = false, bool isEnum = false, bool isScalar = false)
            : base(schema, name, description, requiredAuthorization)
        {
            TypeDotnet = dotnetType;
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
        public override ISchemaType AddAllFields(bool autoCreateNewComplexTypes = false, bool autoCreateEnumTypes = true)
        {
            if (IsEnum)
            {
                foreach (var field in TypeDotnet.GetFields())
                {
                    if (field.Name == "value__")
                        continue;

                    var enumName = Enum.Parse(TypeDotnet, field.Name).ToString();
                    var description = (field.GetCustomAttribute(typeof(DescriptionAttribute)) as DescriptionAttribute)?.Description;
                    var schemaField = new Field(Schema, enumName, null, description, new GqlTypeInfo(() => Schema.GetSchemaType(TypeDotnet, null), TypeDotnet), Schema.AuthorizationService.GetRequiredAuthFromMember(field));
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
                var fields = SchemaBuilder.GetFieldsFromObject(TypeDotnet, Schema, autoCreateEnumTypes, Schema.SchemaFieldNamer, autoCreateNewComplexTypes, true);
                AddFields(fields);
            }
            return this;
        }

        public Field AddField(Field field)
        {
            if (FieldsByName.ContainsKey(field.Name))
                throw new EntityQuerySchemaException($"Field {field.Name} already exists on type {this.Name}. Use ReplaceField() if this is intended.");

            FieldsByName.Add(field.Name, field);
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
            return AddField(Schema.SchemaFieldNamer(exp.Member.Name), fieldSelection, description);
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
            var requiredAuth = Schema.AuthorizationService.GetRequiredAuthFromExpression(fieldSelection);

            var field = new Field(Schema, name, fieldSelection, description, SchemaBuilder.MakeGraphQlType(Schema, typeof(TReturn), null), requiredAuth);
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
            var requiredAuth = Schema.AuthorizationService.GetRequiredAuthFromExpression(fieldSelection);

            var field = new Field(Schema, name, fieldSelection, description, argTypes, SchemaBuilder.MakeGraphQlType(Schema, typeof(TReturn), null), requiredAuth);
            this.AddField(field);
            return field;
        }

        /// <summary>
        /// Add a field definition. Use the Resolve<>() or ResolveWithService<>() chain method to build the resolve expression. This lets you add dependencies on other services
        /// Throws an exception if the field already exists
        /// </summary>
        /// <param name="name">Name of the field in the schema. Is used as passed. Case sensitive</param>
        /// <param name="description">Description of the field for schema documentation</param>
        /// <returns>The field object to perform further configuration</returns>
        public FieldToResolve<TBaseType> AddField(string name, string? description)
        {
            var field = new FieldToResolve<TBaseType>(Schema, name, description, null);
            AddField(field);
            return field;
        }

        /// <summary>
        /// Add a field with arguments. Add a field definition. Use the Resolve<>() or ResolveWithService<>() chain method to build the resolve expression. This lets you add dependencies on other services
        ///     field(arg: val)
        /// Throws an exception if the field already exists
        /// </summary>
        /// <typeparam name="TParams"></typeparam>
        /// <param name="name">Name of the field in the schema. Is used as passed. Case sensitive</param>
        /// <param name="argTypes">An object that represents the arguments available for the field including default values or required fields. Anonymous objects are supported</param>
        /// <param name="fieldSelection">The expression to resolve the field value from this current schema type. e.g. ctx => ctx.LotsOfPeople.Where(p => p.Age > 50)</param>
        /// <param name="description">Description of the field for schema documentation</param>
        /// <returns>The field object to perform further configuration</returns>
        public FieldToResolveWithArgs<TBaseType, TParams> AddField<TParams>(string name, TParams argTypes, string? description)
        {
            var field = new FieldToResolveWithArgs<TBaseType, TParams>(Schema, name, description, argTypes);
            AddField(field);
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
            var requiredAuth = Schema.AuthorizationService.GetRequiredAuthFromExpression(fieldSelection);

            var field = new Field(Schema, name, fieldSelection, description, SchemaBuilder.MakeGraphQlType(Schema, typeof(TReturn), null), requiredAuth);
            FieldsByName[field.Name] = field;
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
            var requiredAuth = Schema.AuthorizationService.GetRequiredAuthFromExpression(fieldSelection);

            var field = new Field(Schema, name, fieldSelection, description, argTypes, SchemaBuilder.MakeGraphQlType(Schema, typeof(TReturn), null), requiredAuth);
            FieldsByName[field.Name] = field;
            return field;
        }

        /// <summary>
        /// Replaces a field matching the name with this new field. If the field does not exist, it will be added.
        /// </summary>
        /// <param name="name">Name of the field in the schema. Is used as passed. Case sensitive</param>
        /// <param name="description">Description of the field for schema documentation</param>
        /// <returns>The field object to perform further configuration</returns>
        public FieldToResolve<TBaseType> ReplaceField(string name, string? description)
        {
            var field = new FieldToResolve<TBaseType>(Schema, name, description, null);
            FieldsByName[field.Name] = field;
            return field;
        }

        /// <summary>
        /// Replaces a field by name with this new field with arguments. If the field does not exist, it will be added.
        /// </summary>
        /// <typeparam name="TParams"></typeparam>
        /// <param name="name">Name of the field in the schema. Is used as passed. Case sensitive</param>
        /// <param name="argTypes">An object that represents the arguments available for the field including default values or required fields. Anonymous objects are supported</param>
        /// <param name="description">Description of the field for schema documentation</param>
        /// <returns>The field object to perform further configuration</returns>
        public FieldToResolveWithArgs<TBaseType, TParams> ReplaceField<TParams>(string name, TParams argTypes, string? description)
        {
            var field = new FieldToResolveWithArgs<TBaseType, TParams>(Schema, name, description, argTypes);
            FieldsByName[field.Name] = field;
            return field;
        }

        /// <summary>
        /// Get a field by a simple member expression on the real type. The name is changed with fieldNamer
        /// </summary>
        /// <param name="fieldSelection"></param>
        /// <returns>The field object for further configuration</returns>
        public Field GetField(Expression<Func<TBaseType, object>> fieldSelection)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            return GetField(Schema.SchemaFieldNamer(exp.Member.Name), null);
        }
        public new Field GetField(string identifier, QueryRequestContext? requestContext)
        {
            return (Field)base.GetField(identifier, requestContext);
        }

        /// <summary>
        /// Remove a field by a member expression on the real type. The name is changed with fieldNamer for look up
        /// </summary>
        /// <param name="fieldSelection"></param>
        public void RemoveField(Expression<Func<TBaseType, object>> fieldSelection)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            RemoveField(Schema.SchemaFieldNamer(exp.Member.Name));
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
