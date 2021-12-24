using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Antlr4.Runtime;
using EntityGraphQL.Schema;
using EntityQL.Grammer;

namespace EntityGraphQL.Compiler.EntityQuery
{
    /// Simple language to write queries against an object schema.
    ///
    /// myEntity.where(field = 'value')
    ///
    ///   (primary_key) - e.g. myEntity(12)
    /// Binary Operators
    ///   =, !=, <, <=, >, >=, +, -, *, %, /, in
    /// Unary Operators
    ///   not(), !
    public static class EntityQueryCompiler
    {
        public static CompiledQueryResult Compile(string query, UserAuthInfo authInfo)
        {
            return Compile(query, null, authInfo, new DefaultMethodProvider());
        }

        public static CompiledQueryResult Compile(string query, ISchemaProvider schemaProvider, UserAuthInfo authInfo)
        {
            return Compile(query, schemaProvider, authInfo, new DefaultMethodProvider());
        }

        /// <summary>
        /// Compile a query.
        /// </summary>
        /// <param name="query">The query text</param>
        /// <param name="schemaProvider"></param>
        /// <param name="methodProvider"></param>
        /// <returns></returns>
        public static CompiledQueryResult Compile(string query, ISchemaProvider schemaProvider, UserAuthInfo authInfo, IMethodProvider methodProvider)
        {
            ParameterExpression contextParam = null;

            if (schemaProvider != null)
                contextParam = Expression.Parameter(schemaProvider.ContextType, $"cxt_{schemaProvider.ContextType.Name}");
            var expression = CompileQuery(query, contextParam, schemaProvider, authInfo, methodProvider);

            var contextParams = new List<ParameterExpression>();
            if (contextParam != null)
                contextParams.Add(contextParam);
            return new CompiledQueryResult(expression, contextParams);
        }

        public static CompiledQueryResult CompileWith(string query, Expression context, ISchemaProvider schemaProvider, UserAuthInfo authInfo, IMethodProvider methodProvider = null)
        {
            if (methodProvider == null)
            {
                methodProvider = new DefaultMethodProvider();
            }
            var expression = CompileQuery(query, context, schemaProvider, authInfo, methodProvider);

            var parameters = expression.Expression.NodeType == ExpressionType.Lambda ? ((LambdaExpression)expression.Expression).Parameters.ToList() : new List<ParameterExpression>();
            return new CompiledQueryResult(expression, parameters);
        }

        private static ExpressionResult CompileQuery(string query, Expression context, ISchemaProvider schemaProvider, UserAuthInfo authInfo, IMethodProvider methodProvider)
        {
            var stream = new AntlrInputStream(query);
            var lexer = new EntityQLLexer(stream);
            var tokens = new CommonTokenStream(lexer);
            var parser = new EntityQLParser(tokens)
            {
                BuildParseTree = true
            };
            var tree = parser.eqlStart();

            var visitor = new EntityQueryNodeVisitor(context, schemaProvider, methodProvider, authInfo);
            var expression = visitor.Visit(tree);
            return expression;
        }
    }
}
