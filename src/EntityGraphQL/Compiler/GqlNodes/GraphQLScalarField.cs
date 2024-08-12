using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLScalarField : BaseGraphQLField
    {
        public GraphQLScalarField(
            ISchemaProvider schema,
            IField field,
            string name,
            Expression nextFieldContext,
            ParameterExpression? rootParameter,
            IGraphQLNode parentNode,
            IReadOnlyDictionary<string, object>? arguments
        )
            : base(schema, field, name, nextFieldContext, rootParameter, parentNode, arguments) { }

        public override bool HasServicesAtOrBelow(IEnumerable<GraphQLFragmentStatement> fragments)
        {
            return Field?.Services.Count > 0;
        }

        protected override Expression? GetFieldExpression(
            CompileContext compileContext,
            IServiceProvider? serviceProvider,
            List<GraphQLFragmentStatement> fragments,
            ParameterExpression? docParam,
            object? docVariables,
            ParameterExpression schemaContext,
            bool withoutServiceFields,
            Expression? replacementNextFieldContext,
            List<Type>? possibleNextContextTypes,
            bool contextChanged,
            ParameterReplacer replacer
        )
        {
            if (HasServices && withoutServiceFields)
                return null;

            var nextFieldContext = HandleBulkServiceResolver(compileContext, withoutServiceFields, NextFieldContext)!;

            // We need to swap the context first as GetExpression() below may change the expression if it uses the arguments
            // and the expressions will no longer match in ReplaceContext
            // example: field is (x => srv.Something(x.Name, args.input))
            // x.Name needs to be replaced before GetExpression() fixes up the execution args type
            // this is for service fields that have parameters that reference the context and the query args
            // See test InheritanceTestUsingResolveWithServiceUsingArgs
            if (contextChanged && replacementNextFieldContext != null)
            {
                nextFieldContext = ReplaceContext(replacementNextFieldContext!, replacer, nextFieldContext!, possibleNextContextTypes);
            }

            HandleBeforeRootFieldExpressionBuild(compileContext, GetOperationName(this), Name, contextChanged, IsRootField, ref nextFieldContext);

            (var result, _) = Field!.GetExpression(
                nextFieldContext,
                replacementNextFieldContext,
                ParentNode!,
                schemaContext,
                compileContext,
                Arguments,
                docParam,
                docVariables,
                Directives,
                contextChanged,
                replacer
            );

            if (result == null)
                return null;

            var newExpression = result;

            newExpression = ProcessScalarExpression(newExpression, replacer);

            if (HasServices)
                compileContext.AddServices(Field.Services);
            return newExpression;
        }
    }
}
