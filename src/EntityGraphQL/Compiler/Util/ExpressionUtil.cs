using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.Util
{
    public static class ExpressionUtil
    {
        public static Expression MakeCallOnQueryable(string methodName, Type[] genericTypes, params Expression[] parameters)
        {
            var type = typeof(IQueryable).IsAssignableFrom(parameters.First().Type) ? typeof(Queryable) : typeof(Enumerable);
            try
            {
                return Expression.Call(type, methodName, genericTypes, parameters);
            }
            catch (InvalidOperationException ex)
            {
                throw new EntityGraphQLCompilerException($"Could not find extension method {methodName} on types {type}", ex);
            }
        }

        public static Expression MakeCallOnEnumerable(string methodName, Type[] genericTypes, params Expression[] parameters)
        {
            try
            {
                return Expression.Call(typeof(Enumerable), methodName, genericTypes, parameters);
            }
            catch (InvalidOperationException ex)
            {
                throw new EntityGraphQLCompilerException($"Could not find extension method {methodName} on types {typeof(Enumerable)}", ex);
            }
        }

        public static MemberExpression CheckAndGetMemberExpression<TBaseType, TReturn>(Expression<Func<TBaseType, TReturn>> fieldSelection)
        {
            var exp = fieldSelection.Body;
            if (exp.NodeType == ExpressionType.Convert)
                exp = ((UnaryExpression)exp).Operand;

            if (exp.NodeType != ExpressionType.MemberAccess)
                throw new ArgumentException("fieldSelection should be a property or field accessor expression only. E.g (t) => t.MyField", nameof(fieldSelection));
            return (MemberExpression)exp;
        }

        public static object? ChangeType(object? value, Type toType, ISchemaProvider? schema)
        {
            if (value == null)
            {
                if (toType.IsConstructedGenericType && toType.GetGenericTypeDefinition() == typeof(EntityQueryType<>))
                {
                    // we don't want a null value. We want an empty EntityQueryType
                    var entityQuery = Activator.CreateInstance(toType);
                    return entityQuery;
                }

                return null;
            }

            var fromType = value.GetType();

            if (value == null || fromType == toType)
                return value;

            // Default JSON deserializer will deserialize child objects in QueryVariables as this JSON type
            if (typeof(JsonElement).IsAssignableFrom(fromType))
            {
                var jsonEle = (JsonElement)value;

                if (jsonEle.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }

                if (jsonEle.ValueKind == JsonValueKind.Object)
                {
                    value = Activator.CreateInstance(toType);
                    foreach (var item in jsonEle.EnumerateObject())
                    {
                        var prop = toType.GetProperties().FirstOrDefault(p => p.Name.ToLowerInvariant() == item.Name.ToLowerInvariant());
                        if (prop != null)
                            prop.SetValue(value, ChangeType(item.Value, prop.PropertyType, schema));
                        else
                        {
                            var field = toType.GetFields().FirstOrDefault(p => p.Name.ToLowerInvariant() == item.Name.ToLowerInvariant());
                            field?.SetValue(value, ChangeType(item.Value, field.FieldType, schema));
                        }
                    }
                    return value;
                }
                if (jsonEle.ValueKind == JsonValueKind.Array)
                {
                    var eleType = toType.GetEnumerableOrArrayType()!;
                    var list = (IList?)Activator.CreateInstance(typeof(List<>).MakeGenericType(eleType)) ?? throw new EntityGraphQLCompilerException($"Could not create list of type {eleType}");
                    foreach (var item in jsonEle.EnumerateArray())
                        list.Add(ChangeType(item, eleType, schema));
                    return list;
                }
                value = jsonEle.ToString();
                fromType = value.GetType();

                if (value == null)
                    return null;
            }

            // custom type converters after we unwind JSON elements
            if (schema?.TypeConverters.TryGetValue(fromType, out var converter) == true)
            {
                value = converter.ChangeType(value, toType, schema);
                fromType = value?.GetType()!;

                if (value == null || fromType == toType)
                    return value;
            }

            if (toType != typeof(string) && fromType == typeof(string))
            {
                if (toType == typeof(double) || toType == typeof(double?))
                    return double.Parse((string)value, CultureInfo.InvariantCulture);
                if (toType == typeof(decimal) || toType == typeof(decimal?))
                    return decimal.Parse((string)value, CultureInfo.InvariantCulture);
                if (toType == typeof(float) || toType == typeof(float?))
                    return float.Parse((string)value, CultureInfo.InvariantCulture);
                if (toType == typeof(int) || toType == typeof(int?))
                    return int.Parse((string)value, CultureInfo.InvariantCulture);
                if (toType == typeof(uint) || toType == typeof(uint?))
                    return uint.Parse((string)value, CultureInfo.InvariantCulture);
                if (toType == typeof(short) || toType == typeof(short?))
                    return short.Parse((string)value, CultureInfo.InvariantCulture);
                if (toType == typeof(DateTime) || toType == typeof(DateTime?))
                    return DateTime.Parse((string)value, CultureInfo.InvariantCulture);
                if (toType == typeof(DateTimeOffset) || toType == typeof(DateTimeOffset?))
                    return DateTimeOffset.Parse((string)value, CultureInfo.InvariantCulture);
            }
            else if (toType != typeof(long) && fromType == typeof(long))
            {
                if (toType == typeof(DateTime) || toType == typeof(DateTime?))
                    return new DateTime((long)value);
                if (toType == typeof(DateTimeOffset) || toType == typeof(DateTimeOffset?))
                    return new DateTimeOffset((long)value, TimeSpan.Zero);
            }

            var argumentNonNullType = toType.IsNullableType() ? Nullable.GetUnderlyingType(toType)! : toType;
            var valueNonNullType = fromType.IsNullableType() ? Nullable.GetUnderlyingType(fromType)! : fromType;
            if (argumentNonNullType.IsEnum)
            {
                return valueNonNullType == typeof(string) ? Enum.Parse(argumentNonNullType, (string)value) : Enum.ToObject(argumentNonNullType, value);
            }
            if (fromType.IsDictionary())
            {
                var interfaceType = fromType.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
                // handle dictionary of dictionary representing the objects
                if (interfaceType.GetGenericArguments()[0] != typeof(string))
                    throw new EntityGraphQLCompilerException($"Dictionary key type must be string. Got {fromType.GetGenericArguments()[0]}");

                var newValue = Activator.CreateInstance(toType);
                foreach (string key in ((IDictionary<string, object>)value).Keys)
                {
                    var toProp = toType.GetProperties().FirstOrDefault(p => p.Name.ToLowerInvariant() == key.ToLowerInvariant());
                    if (toProp != null)
                        toProp.SetValue(newValue, ChangeType(((IDictionary)value)[key], toProp.PropertyType, schema));
                    else
                    {
                        var toField = toType.GetFields().FirstOrDefault(p => p.Name.ToLowerInvariant() == key.ToLowerInvariant());
                        toField?.SetValue(newValue, ChangeType(((IDictionary)value)[key], toField.FieldType, schema));
                    }
                }
                return newValue;
            }
            if (toType.IsEnumerableOrArray())
            {
                var eleType = toType.GetEnumerableOrArrayType()!;
                var list = (IList?)Activator.CreateInstance(typeof(List<>).MakeGenericType(eleType)) ?? throw new EntityGraphQLCompilerException($"Could not create list of type {eleType}");
                foreach (var item in (IEnumerable)value)
                    list.Add(ChangeType(item, eleType, schema));
                if (toType.IsArray)
                {
                    // if toType is array [] we can't use a List<>
                    var result = Expression.Lambda(Expression.Call(typeof(Enumerable), "ToArray", new[] { eleType }, Expression.Constant(list))).Compile().DynamicInvoke();
                    return result;
                }
                return list;
            }
            if ((argumentNonNullType == typeof(Guid) || argumentNonNullType == typeof(Guid?) ||
                argumentNonNullType == typeof(RequiredField<Guid>) || argumentNonNullType == typeof(RequiredField<Guid?>)) &&
                fromType == typeof(string) && QueryWalkerHelper.GuidRegex.IsMatch(value.ToString()!))
            {
                return Guid.Parse(value!.ToString()!);
            }
            if (toType.IsGenericType && toType.GetGenericTypeDefinition() == typeof(RequiredField<>))
            {
                if (fromType == toType.GetGenericArguments()[0])
                    return Activator.CreateInstance(toType, value);
                else if (toType.IsGenericType && toType.GetGenericTypeDefinition() == typeof(RequiredField<>))
                    return Activator.CreateInstance(toType, ChangeType(value, toType.GetGenericArguments()[0], schema));
            }
            if (argumentNonNullType.IsClass && typeof(string) != argumentNonNullType && !fromType.IsEnumerableOrArray())
            {
                return ConvertObjectType(schema, value, toType, fromType);
            }
            if (argumentNonNullType != valueNonNullType)
            {
                var implicitCastOperator = argumentNonNullType.GetMethod("op_Implicit", new[] { valueNonNullType });
                if (implicitCastOperator != null)
                {
                    return implicitCastOperator.Invoke(null, new[] { value });
                }

                var newVal = Convert.ChangeType(value, argumentNonNullType, CultureInfo.InvariantCulture);
                return newVal;
            }

            return value;
        }

        private static object? ConvertObjectType(ISchemaProvider? schema, object? value, Type toType, Type valueObjType)
        {
            var newValue = Activator.CreateInstance(toType);
            foreach (var toField in toType.GetFields())
            {
                var fromProp = valueObjType.GetProperties().FirstOrDefault(p => p.Name.ToLowerInvariant() == toField.Name.ToLowerInvariant());
                if (fromProp != null)
                    toField.SetValue(newValue, ChangeType(fromProp.GetValue(value), toField.FieldType, schema));
                else
                {
                    var fromField = valueObjType.GetFields().FirstOrDefault(p => p.Name.ToLowerInvariant() == toField.Name.ToLowerInvariant());
                    if (fromField != null)
                        toField.SetValue(newValue, ChangeType(fromField.GetValue(value), toField.FieldType, schema));
                }
            }
            foreach (var toProperty in toType.GetProperties())
            {
                var fromProp = valueObjType.GetProperties().FirstOrDefault(p => p.Name.ToLowerInvariant() == toProperty.Name.ToLowerInvariant());
                if (fromProp != null)
                    toProperty.SetValue(newValue, ChangeType(fromProp.GetValue(value), toProperty.PropertyType, schema));
                else
                {
                    var fromField = valueObjType.GetFields().FirstOrDefault(p => p.Name.ToLowerInvariant() == toProperty.Name.ToLowerInvariant());
                    if (fromField != null)
                        toProperty.SetValue(newValue, ChangeType(fromField.GetValue(value), toProperty.PropertyType, schema));
                }
            }

            // Handle converting a string to EntityQueryType
            if (schema != null && toType.IsConstructedGenericType && toType.GetGenericTypeDefinition() == typeof(EntityQueryType<>))
            {
                if (value != null && !string.IsNullOrWhiteSpace((string)value))
                {
                    var expression = BuildEntityQueryExpression(schema, toType.GetGenericArguments()[0], (string)value);
                    var genericProp = toType.GetProperty("Query")!;
                    genericProp.SetValue(newValue, expression);
                }
            }
            return newValue;
        }

        public static Dictionary<string, ArgType> ObjectToDictionaryArgs(ISchemaProvider schema, object argTypes)
        {
            var args = argTypes.GetType().GetProperties().Where(p => !GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(p)).ToDictionary(k => schema.SchemaFieldNamer(k.Name), p => ArgType.FromProperty(schema, p, p.GetValue(argTypes)));
            argTypes.GetType().GetFields().Where(p => !GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(p)).ToList().ForEach(p => args.Add(schema.SchemaFieldNamer(p.Name), ArgType.FromField(schema, p, p.GetValue(argTypes))));
            return args;
        }

        public static Type MergeTypes(Type? type1, Type type2)
        {
            if (type1 == null)
                return type2;

            if (type2 == null)
                throw new ArgumentNullException(nameof(type2));

            var fields = type1.GetFields().ToDictionary(f => f.Name, f => f.FieldType);
            type1.GetProperties().ToList().ForEach(f => fields.Add(f.Name, f.PropertyType));
            type2.GetFields().ToList().ForEach(f => fields.Add(f.Name, f.FieldType));
            type2.GetProperties().ToList().ForEach(f => fields.Add(f.Name, f.PropertyType));

            var newType = LinqRuntimeTypeBuilder.GetDynamicType(fields, "mergedArgs");
            return newType;
        }

        /// <summary>
        /// If the combineExpression is a First()/etc. with a filter, pull the filter back to a Where() in the collection field expression
        /// </summary>
        /// <param name="collectionSelectionNode"></param>
        /// <param name="combineExpression"></param>
        public static string? UpdateCollectionNodeFieldExpression(GraphQLListSelectionField collectionSelectionNode, Expression combineExpression)
        {
            string? capMethod = null;
            if (combineExpression.NodeType == ExpressionType.Call)
            {
                // In the case of a First() we need to insert that select before the first
                // This is all to have 1 nice expression that can work with ORMs (like EF)
                // E.g  we want db => db.Entity.Select(e => new {name = e.Name, ...}).First(filter)
                // we dot not want db => new {name = db.Entity.First(filter).Name, ...})

                var call = (MethodCallExpression)combineExpression;
                if (call.Method.Name == "First" || call.Method.Name == "FirstOrDefault" ||
                    call.Method.Name == "Last" || call.Method.Name == "LastOrDefault" ||
                    call.Method.Name == "Single" || call.Method.Name == "SingleOrDefault")
                {
                    // Get the expression that we can add the Select() too
                    var contextExpression = collectionSelectionNode.ListExpression;
                    if (contextExpression != null && call.Arguments.Count == 2)
                    {
                        // this is a ctx.Something.First(f => ...)
                        // move the filter to a Where call so we can use .Select() to get the fields requested
                        var filter = call.Arguments.ElementAt(1);
                        var isQueryable = typeof(IQueryable).IsAssignableFrom(contextExpression.Type);
                        contextExpression = isQueryable ?
                            MakeCallOnQueryable("Where", new Type[] { combineExpression.Type }, contextExpression, filter) :
                            MakeCallOnEnumerable("Where", new Type[] { combineExpression.Type }, contextExpression, filter);
                        // we can first call ToList() as the data is filtered so risk of over fetching is low
                        capMethod = call.Method.Name;
                        collectionSelectionNode.ListExpression = contextExpression;
                    }
                }
            }
            return capMethod;
        }

        /// <summary>
        /// Tries to take 2 expressions returned from FindIEnumerable and join them together. I.e. If we Split list.First() with FindIEnumerable, we can join it back together with newList.First()
        /// </summary>
        /// <param name="baseExp"></param>
        /// <param name="nextExp"></param>
        /// <returns></returns>
        public static Expression CombineExpressions(Expression baseExp, Expression nextExp, ParameterReplacer replacer)
        {
            switch (nextExp.NodeType)
            {
                case ExpressionType.Call:
                    {
                        var mc = (MethodCallExpression)nextExp;
                        if (mc.Object == null)
                        {
                            var args = new List<Expression> { baseExp };
                            var type = baseExp.Type.GetGenericArguments().First();
                            var newParam = Expression.Parameter(type, $"p_{type.Name}");
                            foreach (var item in mc.Arguments.Skip(1))
                            {
                                var exp = item;
                                if (exp.NodeType == ExpressionType.Quote)
                                {
                                    exp = ((UnaryExpression)item).Operand;
                                }
                                var lambda = (LambdaExpression)exp;
                                exp = replacer.Replace(lambda, lambda.Parameters.First(), newParam);
                                args.Add(exp);
                            }
                            var call = MakeCallOnQueryable(mc.Method.Name, baseExp.Type.GetGenericArguments().ToArray(), args.ToArray());
                            return call;
                        }
                        return Expression.Call(baseExp, mc.Method, mc.Arguments);
                    }
                default: throw new EntityGraphQLCompilerException($"Could not join expressions '{baseExp.NodeType} and '{nextExp.NodeType}'");
            }
        }

        /// <summary>
        /// Navigates back through an expression to see if there was a point where we had a IEnumerable object so we can "edit" it (so a .Select() etc.)
        /// </summary>
        /// <param name="baseExpression"></param>
        /// <returns></returns>
        public static Tuple<Expression?, Expression?> FindEnumerable(Expression baseExpression)
        {
            var exp = baseExpression;
            Expression? endExpression = null;
            while (exp != null && !exp.Type.IsEnumerableOrArray())
            {
                switch (exp.NodeType)
                {
                    case ExpressionType.Call:
                        {
                            endExpression = exp;
                            var mc = (MethodCallExpression)exp;
                            exp = mc.Object ?? mc.Arguments.First();
                            if (exp.Type != baseExpression.Type && exp.Type.IsEnumerableOrArray() && exp.Type.GetEnumerableOrArrayType() != baseExpression.Type)
                                exp = null;
                            break;
                        }
                    default:
                        exp = null;
                        break;
                }
            }
            return Tuple.Create(exp, endExpression);
        }

        class ConversionVisitor : ExpressionVisitor
        {
            private readonly Expression parameter;
            private readonly Type convertTo;

            public ConversionVisitor(Expression parameter, Type convertTo)
            {
                this.parameter = parameter;
                this.convertTo = convertTo;
            }
            protected override Expression VisitMember(MemberExpression node)
            {
                if (!node.Expression?.Type.IsAssignableFrom(convertTo) == true)
                {
                    if (node.Expression is MemberExpression)
                    {
                        return Expression.PropertyOrField(Visit(node.Expression), node.Member.Name);
                    }
                    return node;
                }

                return Expression.PropertyOrField(Expression.Convert(parameter, convertTo), node.Member.Name);
            }
        }

        private static Type? RootType(Expression expression)
        {
            return expression switch
            {
                MemberExpression me => me.Expression?.Type,
                ConditionalExpression ce => RootType(ce.Test),
                BinaryExpression be => RootType(be.Left),
                _ => null
            };
        }

        /// <summary>
        /// Makes a selection from a IEnumerable context
        /// </summary>
        public static Expression MakeSelectWithDynamicType(GraphQLListSelectionField field, ParameterExpression currentContextParam, Expression baseExp, IDictionary<IFieldKey, CompiledField> fieldExpressions, bool nullCheck)
        {
            if (!fieldExpressions.Any())
                return baseExp;

            // fallback to parent return type for mutations
            var gqlType = field.Field?.ReturnType.SchemaType.GqlType ?? field.ParentNode?.Field?.ReturnType.SchemaType.GqlType;
            if (gqlType == GqlTypes.Union || gqlType == GqlTypes.Interface)
            {
                // get a list of distinct types asked for in the query (fragments for interfaces)
                var validTypes = fieldExpressions.Values
                    .Select(i => RootType(i.Expression))
                    .Where(i => i != null && currentContextParam.Type.IsAssignableFrom(i))
                    .Distinct();

                var fieldsOnBaseType = fieldExpressions.Values
                       .Where(i => RootType(i.Expression) == null || RootType(i.Expression)! == currentContextParam.Type || typeof(ISchemaType).IsAssignableFrom(RootType(i.Expression)))
                       .ToLookup(i => i.Field.Name, i => i.Expression)
                       .ToDictionary(i => i.Key, i => i.Last());

                // make a query that checks type of object and returns the valid properties for that specific type
                var baseDynamicType = LinqRuntimeTypeBuilder.GetDynamicType(
                    fieldsOnBaseType.ToDictionary(x => x.Key, x => x.Value.Type),
                    field.Name + "baseDynamicType"
                ) ?? throw new EntityGraphQLCompilerException("Could not create dynamic type");

                //create a cascading TypeIs X query for each type in query
                Expression? previous = CreateNewExpression(fieldsOnBaseType, baseDynamicType) ?? Expression.Constant(null, baseDynamicType);

                foreach (var type in validTypes)
                {
                    if (type == currentContextParam.Type)
                        continue;

                    var fieldsOnType = fieldExpressions.Values
                       .Where(i => RootType(i.Expression) == null || RootType(i.Expression)!.IsAssignableFrom(type) || typeof(ISchemaType).IsAssignableFrom(RootType(i.Expression)))
                       .ToLookup(i => i.Field.Name, i => i.Expression)
                       .ToDictionary(i => i.Key, i => i.Last());

                    var memberInit = CreateNewExpression(field.Name, fieldsOnType, out Type dynamicType, parentType: baseDynamicType);
                    if (memberInit == null)
                        continue;

                    var conversionVisitor = new ConversionVisitor(currentContextParam, type!);
                    var convertedExpression = conversionVisitor.Visit(memberInit);

                    previous = Expression.Condition(
                          test: Expression.TypeIs(currentContextParam, type!),
                          ifTrue: convertedExpression!,
                          ifFalse: previous,
                          type: baseDynamicType
                    );
                }

                var selector = Expression.Lambda(previous!, currentContextParam);
                var isQueryable = typeof(IQueryable).IsAssignableFrom(baseExp.Type);

                Expression call;
                if (nullCheck)
                    call = Expression.Call(typeof(EnumerableExtensions), nameof(EnumerableExtensions.SelectWithNullCheck), new Type[] { currentContextParam.Type, baseDynamicType }, baseExp, selector);
                else
                    call = isQueryable ? MakeCallOnQueryable(nameof(Enumerable.Select), new Type[] { currentContextParam.Type, baseDynamicType }, baseExp, selector) :
                        MakeCallOnEnumerable(nameof(Queryable.Select), new Type[] { currentContextParam.Type, baseDynamicType }, baseExp, selector);
                return call;
            }
            else
            {
                var memberExpressions = fieldExpressions.ToDictionary(i => i.Key.Name, i => i.Value.Expression);
                var memberInit = CreateNewExpression(field.Name, memberExpressions, out Type dynamicType);
                if (memberInit == null || dynamicType == null) // nothing to select
                    return baseExp;
                var selector = Expression.Lambda(memberInit, currentContextParam);
                var isQueryable = typeof(IQueryable).IsAssignableFrom(baseExp.Type);
                Expression call;
                if (nullCheck)
                    call = Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), isQueryable ? nameof(QueryableExtensions.SelectWithNullCheck) : nameof(EnumerableExtensions.SelectWithNullCheck), new Type[] { currentContextParam.Type, dynamicType }, baseExp, selector);
                else
                    call = isQueryable ? MakeCallOnQueryable(nameof(Enumerable.Select), new Type[] { currentContextParam.Type, dynamicType }, baseExp, selector) :
                        MakeCallOnEnumerable(nameof(Queryable.Select), new Type[] { currentContextParam.Type, dynamicType }, baseExp, selector);
                return call;
            }
        }


        public static Expression? CreateNewExpression(IDictionary<string, Expression> fieldExpressions, Type type, bool includeProperties = false)
        {
            var fieldExpressionsByName = new Dictionary<string, Expression>();

            foreach (var item in fieldExpressions)
            {
                // if there are duplicate fields (looking at you ApolloClient when using fragments) they override
                if (item.Value != null)
                    fieldExpressionsByName[item.Key] = item.Value;
            }

            var bindings = type.GetFields().Select(p => Expression.Bind(p, fieldExpressionsByName[p.Name])).OfType<MemberBinding>().ToList();
            if (includeProperties)
                bindings.AddRange(type.GetProperties().Select(p => Expression.Bind(p, fieldExpressionsByName[p.Name])).OfType<MemberBinding>());
            var constructor = type.GetConstructor(Type.EmptyTypes) ?? throw new EntityGraphQLCompilerException("Could not create dynamic type");
            var newExp = Expression.New(constructor);
            var mi = Expression.MemberInit(newExp, bindings);
            return mi;
        }

        public static Expression? CreateNewExpression(string fieldDescription, IDictionary<string, Expression> fieldExpressions, out Type dynamicType, Type? parentType = null)
        {
            var fieldExpressionsByName = new Dictionary<string, Expression>();

            foreach (var item in fieldExpressions)
            {
                // if there are duplicate fields (looking at you ApolloClient when using fragments) they override
                if (item.Value != null)
                    fieldExpressionsByName[item.Key] = item.Value;
            }

            dynamicType = typeof(object);
            if (!fieldExpressionsByName.Any())
                return null;

            dynamicType = LinqRuntimeTypeBuilder.GetDynamicType(fieldExpressionsByName.ToDictionary(f => f.Key, f => f.Value.Type), fieldDescription, parentType: parentType);
            if (dynamicType == null)
                throw new EntityGraphQLCompilerException("Could not create dynamic type");

            var bindings = dynamicType.GetFields().Select(p => Expression.Bind(p, fieldExpressionsByName[p.Name])).OfType<MemberBinding>();
            var constructor = dynamicType.GetConstructor(Type.EmptyTypes) ?? throw new EntityGraphQLCompilerException("Could not create dynamic type");
            var newExp = Expression.New(constructor);
            var mi = Expression.MemberInit(newExp, bindings);
            return mi;
        }

        private static Expression CreateNewExpression(string fieldDescription, Dictionary<string, Expression> fieldExpressions)
        {
            var fieldExpressionsByName = new Dictionary<string, Expression>();
            foreach (var item in fieldExpressions)
            {
                // if there are duplicate fields (looking at you ApolloClient when using fragments) they override
                fieldExpressionsByName[item.Key] = item.Value;
            }
            var dynamicType = LinqRuntimeTypeBuilder.GetDynamicType(fieldExpressionsByName.ToDictionary(f => f.Key, f => f.Value.Type), fieldDescription);

            var bindings = dynamicType.GetFields().Select(p => Expression.Bind(p, fieldExpressionsByName[p.Name])).OfType<MemberBinding>();
            var constructor = dynamicType.GetConstructor(Type.EmptyTypes) ?? throw new EntityGraphQLCompilerException("Could not create dynamic type");
            var newExp = Expression.New(constructor);
            var mi = Expression.MemberInit(newExp, bindings);
            return mi;
        }

        /// <summary>
        /// Wrap a field expression in a method that does a null check for us and avoid calling the field multiple times.
        /// E.g. if the field is (item) => CallSomeService(item) and the result is an object (not IEnumerable) we do not want to generate
        ///     CallSomeService(item) == null ? null : new {
        ///         field1 = CallSomeService(item).field1,
        ///         field2 = CallSomeService(item).field2
        //      }
        /// As that will call the function 3 times (or 1 + number of fields selected)
        ///
        /// This wraps the field expression that does the call once
        /// </summary>
        internal static Expression WrapObjectProjectionFieldForNullCheck(string fieldDescription, Expression nullCheckExpression, IEnumerable<ParameterExpression> paramsForFieldExpressions, Dictionary<string, Expression> fieldExpressions, IEnumerable<object?> fieldSelectParamValues, ParameterExpression nullWrapParam, Expression schemaContext)
        {
            var arguments = new Expression[] {
                Expression.Constant(fieldDescription),
                nullCheckExpression,
                Expression.Constant(nullWrapParam, typeof(ParameterExpression)),
                Expression.Constant(paramsForFieldExpressions.ToList()),
                Expression.Constant(fieldExpressions),
                Expression.Constant(fieldSelectParamValues),
                schemaContext == null ? Expression.Constant(null, typeof(ParameterExpression)) : Expression.Constant(schemaContext),
                schemaContext ?? Expression.Constant(null),
            };
            var call = Expression.Call(typeof(ExpressionUtil), nameof(WrapObjectProjectionFieldForNullCheckExec), null, arguments);
            return call;
        }

        /// <summary>
        /// DO NOT REMOVE. Used at runtuime.
        /// Actually implements the null check code. This is executed at execution time of the whole query not at compile time
        /// </summary>
        /// <param name="nullCheck">Object that we build the select on. Check if it is null first</param>
        /// <param name="nullWrapParam">The ParameterExpression for the null check</param>
        /// <param name="paramsForFieldExpressions">Parameters needed for the expression</param>
        /// <param name="fieldExpressions">Selection fields</param>
        /// <param name="fieldSelectParamValues">Values (arguments) for the paramsForFieldExpressions</param>
        /// <param name="schemaContextParam"></param>
        /// <param name="schemaContextValue"></param>
        /// <returns></returns>
        public static object? WrapObjectProjectionFieldForNullCheckExec(string fieldDescription, object? nullCheck, ParameterExpression nullWrapParam, List<ParameterExpression> paramsForFieldExpressions, Dictionary<string, Expression> fieldExpressions, IEnumerable<object?> fieldSelectParamValues, ParameterExpression schemaContextParam, object schemaContextValue)
        {
            if (nullCheck == null)
                return null;

            var newExp = CreateNewExpression(fieldDescription, fieldExpressions);
            var args = new List<object?>();
            args.AddRange(fieldSelectParamValues);
            if (schemaContextParam != null)
            {
                args.Add(schemaContextValue);
                if (!paramsForFieldExpressions.Contains(schemaContextParam))
                    paramsForFieldExpressions.Add(schemaContextParam);
            }
            if (nullWrapParam != null)
            {
                if (!paramsForFieldExpressions.Contains(nullWrapParam))
                    paramsForFieldExpressions.Add(nullWrapParam);
                args.Add(nullCheck);
            }
            var result = Expression.Lambda(newExp, paramsForFieldExpressions).Compile().DynamicInvoke(args.ToArray());
            return result;
        }

        public static Expression BuildEntityQueryExpression(ISchemaProvider schemaProvider, Type queryType, string query)
        {
            var contextParam = Expression.Parameter(queryType, $"q_{queryType.Name}");
            Expression expression = EntityQueryCompiler.CompileWith(query, contextParam, schemaProvider, new QueryRequestContext(null, null)).ExpressionResult;
            expression = Expression.Lambda(expression, contextParam);
            return expression;
        }
    }
}
