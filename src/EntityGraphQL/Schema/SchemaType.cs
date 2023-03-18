using System;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using Nullability;

namespace EntityGraphQL.Schema
{
    public class SchemaType<TBaseType> : BaseSchemaTypeWithFields<IField>
    {
        public override Type TypeDotnet { get; }

        public SchemaType(ISchemaProvider schema, string name, string? description, RequiredAuthorization? requiredAuthorization, GqlTypes gqlType = GqlTypes.QueryObject, string? baseType = null)
            : this(schema, typeof(TBaseType), name, description, requiredAuthorization, gqlType, baseType)
        {

        }

        public SchemaType(ISchemaProvider schema, Type dotnetType, string name, string? description, RequiredAuthorization? requiredAuthorization, GqlTypes gqlType = GqlTypes.QueryObject, string? baseType = null)
            : base(schema, name, description, requiredAuthorization)
        {
            GqlType = gqlType;
            TypeDotnet = dotnetType;

            RequiredAuthorization = requiredAuthorization;

            if (gqlType != GqlTypes.Scalar)
            {
                if (gqlType == GqlTypes.Interface || gqlType == GqlTypes.Union)
                    // Because the type might actually be the type extending from the interface we need to look it up
                    AddField("__typename", t => schema.Type(t!.GetType().Name).Name, "Type name").IsNullable(false);
                else
                    // Simple and allows FieldExtensions that create new types that are not interfaces not have to worry about updating the typename expression
                    AddField("__typename", _ => Name, "Type name").IsNullable(false);
            }

            if (baseType != null)
            {
                BaseTypes.Add(schema.GetSchemaType(baseType, null));
            }

            ApplyAttributes(dotnetType.GetCustomAttributes());
        }

        /// <summary>
        /// Using reflection, add all the public Fields and Properties from the dotnet type as fields on the 
        /// schema type. Quick helper method to build out schemas
        /// </summary>
        /// <param name="options">Default SchemaBuilderOptions are used if null (default)</param>
        /// <returns>The schema type the fields were added to</returns>
        public override ISchemaType AddAllFields(SchemaBuilderOptions? options = null)
        {
            if (GqlType == GqlTypes.Enum)
            {
                foreach (var field in TypeDotnet.GetFields())
                {
                    if (field.Name == "value__")
                        continue;

                    var enumName = Enum.Parse(TypeDotnet, field.Name).ToString()!;
                    var description = (field.GetCustomAttribute(typeof(DescriptionAttribute)) as DescriptionAttribute)?.Description;
                    var nullability = field.GetNullabilityInfo();
                    var gqlTypeInfo = new GqlTypeInfo(() => Schema.GetSchemaType(TypeDotnet, null), TypeDotnet, nullability);
                    var schemaField = new Field(Schema, this, enumName, null, description, null, gqlTypeInfo, Schema.AuthorizationService.GetRequiredAuthFromMember(field));
                    var obsoleteAttribute = field.GetCustomAttribute<ObsoleteAttribute>();
                    schemaField.ApplyAttributes(field.GetCustomAttributes());

                    AddField(schemaField);
                }
            }
            else
            {
                options ??= new SchemaBuilderOptions();
                var fields = SchemaBuilder.GetFieldsFromObject(TypeDotnet, this, Schema, options, GqlType == GqlTypes.InputObject);
                AddFields(fields);
            }
            return this;
        }

        public Field AddField(Field field)
        {
            if (GqlType == GqlTypes.Union && field.Name != "__typename")
                throw new InvalidOperationException("Unions cannot contain fields");

            if (FieldsByName.ContainsKey(field.Name))
                throw new EntityQuerySchemaException($"Field {field.Name} already exists on type {Name}. Use ReplaceField() if this is intended.");

            if (field.Arguments.Any() && (GqlType == GqlTypes.InputObject || GqlType == GqlTypes.Enum))
                throw new EntityQuerySchemaException($"Field {field.Name} on type {Name} has arguments but is a GraphQL {GqlType} type and can not have arguments.");

            if (GqlType == GqlTypes.Scalar)
                throw new EntityQuerySchemaException($"Cannot add field {field.Name} to type {Name}, as {Name} is a scalar type and can not have fields.");

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

            var field = new Field(Schema, this, name, fieldSelection, description, null, SchemaBuilder.MakeGraphQlType(Schema, typeof(TReturn), null), requiredAuth);
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

            var field = new Field(Schema, this, name, fieldSelection, description, argTypes, SchemaBuilder.MakeGraphQlType(Schema, typeof(TReturn), null), requiredAuth);
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
            var field = new FieldToResolve<TBaseType>(Schema, this, name, description, null);
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
            var field = new FieldToResolveWithArgs<TBaseType, TParams>(Schema, this, name, description, argTypes);
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

            RemoveField(name);
            return AddField(new Field(Schema, this, name, fieldSelection, description, null, SchemaBuilder.MakeGraphQlType(Schema, typeof(TReturn), null), requiredAuth));
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

            RemoveField(name);
            return AddField(new Field(Schema, this, name, fieldSelection, description, argTypes, SchemaBuilder.MakeGraphQlType(Schema, typeof(TReturn), null), requiredAuth));
        }

        /// <summary>
        /// Replaces a field matching the name with this new field. If the field does not exist, it will be added.
        /// </summary>
        /// <param name="name">Name of the field in the schema. Is used as passed. Case sensitive</param>
        /// <param name="description">Description of the field for schema documentation</param>
        /// <returns>The field object to perform further configuration</returns>
        public FieldToResolve<TBaseType> ReplaceField(string name, string? description)
        {
            var field = new FieldToResolve<TBaseType>(Schema, this, name, description, null);
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
            var field = new FieldToResolveWithArgs<TBaseType, TParams>(Schema, this, name, description, argTypes);
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

        public override ISchemaType ImplementAllBaseTypes(bool addTypeIfNotInSchema = true, bool addAllFieldsOnAddedType = true)
        {
            if (TypeDotnet.BaseType != null)
            {
                Implements(TypeDotnet.BaseType, addTypeIfNotInSchema, addAllFieldsOnAddedType);
            }

            foreach (var i in TypeDotnet.GetInterfaces())
            {
                Implements(i, addTypeIfNotInSchema, addAllFieldsOnAddedType);
            }

            return this;
        }

        public override ISchemaType Implements<TClrType>(bool addTypeIfNotInSchema = true, bool addAllFieldsOnAddedType = true)
        {
            var type = typeof(TClrType);
            return Implements(type, addTypeIfNotInSchema, addAllFieldsOnAddedType);
        }

        private ISchemaType Implements(Type type, bool addTypeIfNotInSchema = true, bool addAllFieldsOnAddedType = true)
        {
            var hasInterface = Schema.HasType(type);
            ISchemaType? interfaceType = null;
            if (hasInterface)
            {
                interfaceType = Schema.GetSchemaType(type, null);

                if (!interfaceType.IsInterface)
                    throw new EntityGraphQLCompilerException($"Schema type {type.Name} can not be implemented as it is not an interface. You can only implement interfaces");
            }
            else if (!hasInterface && addTypeIfNotInSchema)
            {
                interfaceType = Schema.AddInterface(type, type.Name, null);

                if (addAllFieldsOnAddedType)
                    interfaceType.AddAllFields();
            }
            if (interfaceType == null)
                throw new EntityGraphQLCompilerException($"No schema interface found for dotnet type {type.Name}. Make sure you add the interface to the schema. Or use parameter addTypeIfNotInSchema = true");

            BaseTypes.Add(interfaceType);
            return this;
        }

        public override ISchemaType Implements(string typeName)
        {
            var interfaceType = Schema.GetSchemaType(typeName, null);
            if (!interfaceType.IsInterface)
                throw new EntityGraphQLCompilerException($"Schema type {typeName} can not be implemented as it is not an interface. You can only implement interfaces");

            BaseTypes.Add(interfaceType);
            return this;
        }

        public ISchemaType AddAllPossibleTypes(bool addTypeIfNotInSchema = true, bool addAllFieldsOnAddedType = true)
        {
            if (GqlType != GqlTypes.Union)
                throw new EntityGraphQLCompilerException($"Schema type {TypeDotnet} is not a union type");


            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => TypeDotnet.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

            foreach (var type in types)
            {
                AddPossibleType(type, addTypeIfNotInSchema, addAllFieldsOnAddedType);
            }

            return this;
        }

        public ISchemaType AddPossibleType<TClrType>(bool addTypeIfNotInSchema = true, bool addAllFieldsOnAddedType = true)
        {
            if (GqlType != GqlTypes.Union)
                throw new EntityGraphQLCompilerException($"Schema type {TypeDotnet} is not a union type");

            var type = typeof(TClrType);
            return AddPossibleType(type, addTypeIfNotInSchema, addAllFieldsOnAddedType);
        }

        private ISchemaType AddPossibleType(Type type, bool addTypeIfNotInSchema = true, bool addAllFieldsOnAddedType = true)
        {
            if (GqlType != GqlTypes.Union)
                throw new EntityGraphQLCompilerException($"Schema type {TypeDotnet} is not a union type");

            var hasType = Schema.HasType(type);
            ISchemaType? schemaType = null;

            if (!hasType && addTypeIfNotInSchema)
            {
                schemaType = Schema.AddType(type, type.Name, null);

                if (addAllFieldsOnAddedType)
                    schemaType.AddAllFields();
            }
            else if (hasType && schemaType == null)
            {
                schemaType = Schema.GetSchemaType(type, null);
            }
            if (schemaType == null)
                throw new EntityGraphQLCompilerException($"No schema type found for dotnet type {type.Name}. Make sure you add the type to the schema. Or use parameter addTypeIfNotInSchema = true");

            if (schemaType.GqlType != GqlTypes.QueryObject)
                throw new EntityGraphQLCompilerException($"The member types of a Union type must all be Object base types");

            PossibleTypes.Add(schemaType);

            return this;
        }
    }
}
