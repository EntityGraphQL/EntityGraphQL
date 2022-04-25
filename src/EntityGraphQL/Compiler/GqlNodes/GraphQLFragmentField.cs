using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLFragmentField : BaseGraphQLField
    {
        private readonly GraphQLDocument document;

        public GraphQLFragmentField(ISchemaProvider schema, string name, Expression? nodeExpression, ParameterExpression rootParameter, IGraphQLNode parentNode, GraphQLDocument document)
            : base(schema, null, name, nodeExpression, rootParameter, parentNode, null)
        {
            this.document = document;
        }

        public override bool HasAnyServices(IEnumerable<GraphQLFragmentStatement> fragments)
        {
            return fragments.FirstOrDefault(f => f.Name == Name).QueryFields.Any(f => f.HasAnyServices(fragments));
        }

        public override IEnumerable<BaseGraphQLField> Expand(List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, Expression fieldContext, ParameterExpression? docParam, object? docVariables)
        {
            var result = ProcessFieldDirectives(this, docParam, docVariables);
            if (result == null)
                return new List<BaseGraphQLField>();

            var fragment = fragments.FirstOrDefault(f => f.Name == Name) ?? throw new EntityGraphQLCompilerException($"Fragment {Name} not found in query document");
            var fields = fragment.QueryFields.SelectMany(f => f.Expand(fragments, withoutServiceFields, fieldContext, docParam, docVariables));
            // the current op did not know about services in the fragment as the fragment definition may be after the operation in the query
            // we now know  if there are services we need to know about for executing
            if (!withoutServiceFields)
            {
                if (document.AddServiceToCurrentOperation == null)
                    throw new EntityGraphQLCompilerException("AddServiceToCurrentOperation is null");
                var services = new HashSet<Type>();
                foreach (var field in fields)
                {
                    GetServices(services, field);
                }

                document.AddServiceToCurrentOperation(services);
            }
            return fields;
        }

        private void GetServices(HashSet<Type> services, BaseGraphQLField gqlField)
        {
            if (gqlField.Field != null && gqlField.Field.Services.Any() == true)
            {
                foreach (var service in gqlField.Field.Services)
                {
                    services.Add(service);
                }
            }
            foreach (var subField in gqlField.QueryFields)
            {
                GetServices(services, subField);
            }
        }

        public override Expression? GetNodeExpression(IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext, bool isRoot, bool contextChanged, ParameterReplacer replacer)
        {
            throw new EntityGraphQLCompilerException($"Fragment should have expanded out into non fragment fields");
        }
    }
}