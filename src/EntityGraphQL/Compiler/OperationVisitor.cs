using System.Collections.Generic;
using System.Security.Claims;
using EntityGraphQL.Grammer;

namespace EntityGraphQL.Compiler
{
    internal class OperationVisitor : EntityGraphQLBaseVisitor<GraphQLOperation>
    {
        private readonly ClaimsIdentity claims;
        private readonly QueryVariables variables;
        private readonly GraphQLOperation operation;
        private readonly ConstantVisitor constantVisitor;

        public OperationVisitor(QueryVariables variables, Schema.ISchemaProvider schemaProvider, ClaimsIdentity claims)
        {
            this.claims = claims;
            this.variables = variables;
            this.operation = new GraphQLOperation();
            this.constantVisitor = new ConstantVisitor(schemaProvider);
        }

        public override GraphQLOperation VisitOperationName(EntityGraphQLParser.OperationNameContext context)
        {
            this.operation.Name = context.operation.GetText();
            if (context.operationArgs != null)
            {
                Visit(context.operationArgs);
            }
            return this.operation;
        }

        public override GraphQLOperation VisitGqlTypeDef(EntityGraphQLParser.GqlTypeDefContext context)
        {
            var argName = context.gqlVar().GetText().TrimStart('$');
            var isArray = context.arrayType != null;
            var type = isArray ? context.arrayType.type.GetText() : context.type.GetText();
            var required = context.required != null;
            ExpressionResult defaultValue = null;
            if (context.defaultValue != null)
            {
                defaultValue = constantVisitor.Visit(context.defaultValue);
            }

            if (required && !variables.ContainsKey(argName) && defaultValue == null)
            {
                throw new QueryException($"Missing required variable '{argName}' on query '{this.operation.Name}'");
            }

            this.operation.AddArgument(argName, type, isArray, required, defaultValue != null ? defaultValue : null);

            return this.operation;
        }
    }

    internal class GraphQLOperation
    {
        public List<GraphQlOperationArgument> Arguments { get; set; }

        public GraphQLOperation()
        {
            Arguments = new List<GraphQlOperationArgument>();
        }

        public string Name { get; internal set; }

        internal void AddArgument(string argName, object type, bool isArray, bool required, ExpressionResult defaultValue)
        {
            Arguments.Add(new GraphQlOperationArgument(argName, type, isArray, required, defaultValue));
        }
    }

    internal class GraphQlOperationArgument
    {
        public GraphQlOperationArgument(string argName, object type, bool isArray, bool required, ExpressionResult defaultValue)
        {
            this.ArgName = argName;
            this.Type = type;
            this.IsArray = isArray;
            this.Required = required;
            DefaultValue = defaultValue;
        }

        public string ArgName { get; }
        public object Type { get; }
        public bool IsArray { get; }
        public bool Required { get; }
        public ExpressionResult DefaultValue { get; }
    }
}