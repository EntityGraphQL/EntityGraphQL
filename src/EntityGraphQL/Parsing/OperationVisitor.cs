using System;
using System.Collections.Generic;
using EntityGraphQL.Grammer;

namespace EntityGraphQL.Parsing
{
    internal class OperationVisitor : EntityGraphQLBaseVisitor<GraphQLOperation>
    {
        private QueryVariables variables;
        private GraphQLOperation operation;

        public OperationVisitor(QueryVariables variables)
        {
            this.variables = variables;
            this.operation = new GraphQLOperation();
        }

        public override GraphQLOperation VisitOperationName(EntityGraphQLParser.OperationNameContext context)
        {
            this.operation.Name = context.operation.GetText();
            Visit(context.operationArgs);
            return this.operation;
        }

        public override GraphQLOperation VisitGqlTypeDef(EntityGraphQLParser.GqlTypeDefContext context)
        {
            var argName = context.gqlVar().GetText().TrimStart('$');
            var isArray = context.arrayType != null;
            var type = isArray ? context.arrayType.type.GetText() : context.type.GetText();
            var required = context.required != null;

            if (required && !variables.ContainsKey(argName))
            {
                throw new QueryException($"Missing required variable '{argName}' on query '{this.operation.Name}'");
            }

            this.operation.AddArgument(argName, type, isArray, required);

            return this.operation;
        }
    }

    internal class GraphQLOperation
    {
        private List<GraphQlOperationArgument> arguments;

        public GraphQLOperation()
        {
            arguments = new List<GraphQlOperationArgument>();
        }

        public string Name { get; internal set; }

        internal void AddArgument(string argName, object type, bool isArray, bool required)
        {
            arguments.Add(new GraphQlOperationArgument(argName, type, isArray, required));
        }
    }

    internal class GraphQlOperationArgument
    {
        public GraphQlOperationArgument(string argName, object type, bool isArray, bool required)
        {
            this.ArgName = argName;
            this.Type = type;
            this.IsArray = isArray;
            this.Required = required;
        }

        public string ArgName { get; private set; }
        public object Type { get; private set; }
        public bool IsArray { get; private set; }
        public bool Required { get; private set; }
    }
}