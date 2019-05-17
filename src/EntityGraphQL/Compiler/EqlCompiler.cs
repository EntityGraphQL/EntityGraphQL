using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Antlr4.Runtime;
using EntityGraphQL.Compiler;
using EntityGraphQL.Grammer;
using EntityGraphQL.LinqQuery;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    /// Simple language to write queries against an object schema.
    ///
    /// myEntity.where(field = 'value') { my, field, selection, orRelation { field1 } }
    ///
    ///   (primary_key) - e.g. myEntity(12)
    /// Binary Operators
    ///   =, !=, <, <=, >, >=, +, -, *, %, /, in
    /// Urnary Operators
    ///   not(), !
    public static class EqlCompiler
    {
        public static CompiledQueryResult Compile(string query)
        {
            return Compile(query, null, new DefaultMethodProvider(), null);
        }

        public static CompiledQueryResult Compile(string query, ISchemaProvider schemaProvider)
        {
            return Compile(query, schemaProvider, new DefaultMethodProvider(), null);
        }

        /// <summary>
        /// Compile a query.
        /// </summary>
        /// <param name="query">The query text</param>
        /// <param name="schemaProvider"></param>
        /// <param name="methodProvider"></param>
        /// <returns></returns>
        public static CompiledQueryResult Compile(string query, ISchemaProvider schemaProvider, IMethodProvider methodProvider, QueryVariables variables)
        {
            ParameterExpression contextParam = null;

            if (schemaProvider != null)
                contextParam = Expression.Parameter(schemaProvider.ContextType);
            var expression = CompileQuery(query, contextParam, schemaProvider, methodProvider, variables);

            var contextParams = new List<ParameterExpression>();
            if (contextParam != null)
                contextParams.Add(contextParam);
            return new CompiledQueryResult(expression, contextParams);
        }

        public static CompiledQueryResult CompileWith(string query, Expression context, ISchemaProvider schemaProvider, IMethodProvider methodProvider = null, QueryVariables variables = null)
        {
            if (methodProvider == null)
            {
                methodProvider = new DefaultMethodProvider();
            }
            if (variables == null)
            {
                variables = new QueryVariables();
            }
            var expression = CompileQuery(query, context, schemaProvider, methodProvider, variables);

            var parameters = expression.Expression.NodeType == ExpressionType.Lambda ? ((LambdaExpression)expression.Expression).Parameters.ToList() : new List<ParameterExpression>();
            return new CompiledQueryResult(expression, parameters);
        }

        private static ExpressionResult CompileQuery(string query, Expression context, ISchemaProvider schemaProvider, IMethodProvider methodProvider, QueryVariables variables)
        {
            AntlrInputStream stream = new AntlrInputStream(query);
            var lexer = new EntityGraphQLLexer(stream);
            var tokens = new CommonTokenStream(lexer);
            var parser = new EntityGraphQLParser(tokens);
            parser.BuildParseTree = true;
            var tree = parser.startRule();

            var visitor = new QueryGrammerNodeVisitor(context, schemaProvider, methodProvider, variables);
            var expression = visitor.Visit(tree);
            return expression;
        }
    }
}
