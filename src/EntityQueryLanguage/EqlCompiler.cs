using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Antlr4.Runtime;
using EntityQueryLanguage.Grammer;

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
        public static EqlResult Compile(string query)
        {
            return Compile(query, null, new DefaultMethodProvider());
        }

        public static EqlResult Compile(string query, ISchemaProvider schemaProvider)
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
        public static EqlResult Compile(string query, ISchemaProvider schemaProvider, IMethodProvider methodProvider)
        {
            ParameterExpression contextParam = null;

            if (schemaProvider != null)
                contextParam = Expression.Parameter(schemaProvider.ContextType);
            var expression = CompileQuery(query, contextParam, schemaProvider, methodProvider);

            var contextParams = new List<ParameterExpression>();
            if (contextParam != null)
                contextParams.Add(contextParam);
            if (expression.Parameters.Any())
                contextParams.AddRange(expression.Parameters.Keys);
            var lambda = Expression.Lambda(expression, contextParams.ToArray());
            return new EqlResult(lambda, expression.Parameters.Values);
        }

        public static EqlResult CompileWith(string query, Expression context, ISchemaProvider schemaProvider, IMethodProvider methodProvider)
        {
            var expression = CompileQuery(query, context, schemaProvider, methodProvider);

            return new EqlResult(Expression.Lambda(expression), null);
        }

        private static ExpressionResult CompileQuery(string query, Expression context, ISchemaProvider schemaProvider, IMethodProvider methodProvider)
        {
            AntlrInputStream stream = new AntlrInputStream(query);
            var lexer = new EqlGrammerLexer(stream);
            var tokens = new CommonTokenStream(lexer);
            var parser = new EqlGrammerParser(tokens);
            parser.BuildParseTree = true;
            var tree = parser.startRule();

            var visitor = new EqlGrammerVisitor(context, schemaProvider, methodProvider);
            var expression = visitor.Visit(tree);
            return expression;
        }
    }

    public class EqlResult
    {
        private readonly IEnumerable<object> parameterValues;

        public LambdaExpression Expression { get; private set; }
        public EqlResult(LambdaExpression compiledEql, IEnumerable<object> parameterValues)
        {
            Expression = compiledEql;
            this.parameterValues = parameterValues;
        }
        public object Execute(params object[] args)
        {
            var allArgs = new List<object>(args);
            if (parameterValues != null)
                allArgs.AddRange(parameterValues);
            return Expression.Compile().DynamicInvoke(allArgs.ToArray());
        }
        public TObject Execute<TObject>(params object[] args)
        {
            return (TObject)Execute(args);
        }
    }

    public class EqlCompilerException : System.Exception
    {
        public EqlCompilerException(string message) : base(message)
        {
        }
    }
}
