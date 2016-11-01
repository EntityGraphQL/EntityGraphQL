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
    public static EqlResult Compile(string query) {
      return Compile(query, null, null);
    }

    public static EqlResult Compile(string query, ISchemaProvider schemaProvider) {
      return Compile(query, schemaProvider, new DefaultMethodProvider());
    }

    public static EqlResult Compile(string query, ISchemaProvider schemaProvider, IMethodProvider _methodProvider) {
      ParameterExpression contextParam = null;

      if (schemaProvider != null)
        contextParam = Expression.Parameter(schemaProvider.ContextType);

      AntlrInputStream stream = new AntlrInputStream(query);
      var lexer = new EqlGrammerLexer(stream);
      var tokens = new CommonTokenStream(lexer);
      var parser = new EqlGrammerParser(tokens);
      parser.BuildParseTree = true;
      var tree = parser.startRule();

      var visitor = new EqlGrammerVisitor(contextParam, schemaProvider, _methodProvider);
      var expression = visitor.Visit(tree);

      return new EqlResult(contextParam != null ? Expression.Lambda(expression, contextParam) : Expression.Lambda(expression));
    }

    public static EqlResult CompileWith(string query, Expression context, ISchemaProvider schemaProvider, IMethodProvider _methodProvider) {
      AntlrInputStream stream = new AntlrInputStream(query);
      var lexer = new EqlGrammerLexer(stream);
      var tokens = new CommonTokenStream(lexer);
      var parser = new EqlGrammerParser(tokens);
      parser.BuildParseTree = true;
      var tree = parser.startRule();

      var visitor = new EqlGrammerVisitor(context, schemaProvider, _methodProvider);
      var expression = visitor.Visit(tree);

      return new EqlResult(Expression.Lambda(expression));
    }
  }

  public class EqlResult {
    public LambdaExpression Expression { get; private set; }
    public EqlResult(LambdaExpression compiledEql) {
      Expression = compiledEql;
    }
    public object Execute(params object[] args) {
      return Expression.Compile().DynamicInvoke(args);
    }
    public TObject Execute<TObject>(params object[] args) {
      return (TObject)Expression.Compile().DynamicInvoke(args);
    }
  }

  public class EqlCompilerException : System.Exception {
    public EqlCompilerException(string message) : base(message) {
    }
  }
}
