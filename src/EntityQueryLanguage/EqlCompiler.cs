using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Antlr4.Runtime;
using EntityQueryLanguage.Compiler;
using EntityQueryLanguage.Grammer;
using EntityQueryLanguage.Schema;

namespace EntityQueryLanguage
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
        public static QueryResult Compile(string query)
        {
            return Compile(query, null, new DefaultMethodProvider());
        }

        public static QueryResult Compile(string query, ISchemaProvider schemaProvider)
        {
            return Compile(query, schemaProvider, new DefaultMethodProvider());
        }

        /// <summary>
        /// Compile a query.
        /// </summary>
        /// <param name="query">The query text</param>
        /// <param name="schemaProvider"></param>
        /// <param name="methodProvider"></param>
        /// <returns></returns>
        public static QueryResult Compile(string query, ISchemaProvider schemaProvider, IMethodProvider methodProvider)
        {
            ParameterExpression contextParam = null;

            if (schemaProvider != null)
                contextParam = Expression.Parameter(schemaProvider.ContextType);
            var expression = CompileQuery(query, contextParam, schemaProvider, methodProvider);

            var contextParams = new List<ParameterExpression>();
            if (contextParam != null)
                contextParams.Add(contextParam);
            if (expression.ConstantParameters.Any())
                contextParams.AddRange(expression.ConstantParameters.Keys);
            var lambda = Expression.Lambda(expression, contextParams.ToArray());
            return new QueryResult(lambda, expression.ConstantParameters.Values);
        }

        public static QueryResult CompileWith(string query, Expression context, ISchemaProvider schemaProvider, IMethodProvider methodProvider)
        {
            var expression = CompileQuery(query, context, schemaProvider, methodProvider);

            var parameters = expression.Expression.NodeType == ExpressionType.Lambda ? ((LambdaExpression)expression.Expression).Parameters.ToList() : new List<ParameterExpression>();
            if (expression.ConstantParameters != null)
            {
                parameters.AddRange(expression.ConstantParameters.Keys);
            }
            return new QueryResult(Expression.Lambda(expression, parameters.ToArray()), expression.ConstantParameters?.Values);
        }

        private static ExpressionResult CompileQuery(string query, Expression context, ISchemaProvider schemaProvider, IMethodProvider methodProvider)
        {
            AntlrInputStream stream = new AntlrInputStream(query);
            var lexer = new EqlGrammerLexer(stream);
            var tokens = new CommonTokenStream(lexer);
            var parser = new EqlGrammerParser(tokens);
            parser.BuildParseTree = true;
            var tree = parser.startRule();

            var visitor = new QueryGrammerNodeVisitor(context, schemaProvider, methodProvider);
            var expression = visitor.Visit(tree);
            return expression;
        }
    }

    public class EqlCompilerException : System.Exception
    {
        public EqlCompilerException(string message) : base(message)
        {
        }
    }
}
