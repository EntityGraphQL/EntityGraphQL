using System;
using System.Linq;
using System.Linq.Expressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using EntityQueryLanguage.DataApi.Util;
using EntityQueryLanguage.Extensions;
using EntityQueryLanguage.Grammer;

namespace EntityQueryLanguage.DataApi.Parsing
{
  public class DataApiCompiler
  {
    private ISchemaProvider _schemaProvider;
    private IMethodProvider _methodProvider;
    private IRelationHandler _relationHandler;
    public DataApiCompiler(ISchemaProvider schemaProvider, IMethodProvider methodProvider, IRelationHandler relationHandler = null)
    {
      _schemaProvider = schemaProvider;
      _methodProvider = methodProvider;
      _relationHandler = relationHandler;
    }

    /// Parses a GraphQL-like query syntax into a tree respresenting the requested object graph. E.g.
    /// {
    ///   entity/query {
    ///     field1,
    ///     field2,
    ///     relation { field }
    ///   },
    ///   ...
    /// }
    ///
    /// The returned DataQueryNode is a root node, it's Fields are the top level data queries
    public DataApiNode Compile(string query)
    {
      // Setup our Antlr parser
      var stream = new AntlrInputStream(query);
      var lexer = new EqlGrammerLexer(stream);
      var tokens = new CommonTokenStream(lexer);
      var parser = new EqlGrammerParser(tokens);
      parser.BuildParseTree = true;
      parser.ErrorHandler = new BailErrorStrategy();
      try
      {
        var tree = parser.dataQuery();
        var visitor = new DataApiVisitor(_schemaProvider, _methodProvider, _relationHandler);
        // visit each node. it will return a linq expression for each entity requested
        var node = visitor.Visit(tree);
        return node;
      }
      catch (ParseCanceledException pce)
      {
        if (pce.InnerException != null)
        {
          if (pce.InnerException is NoViableAltException)
          {
            var nve = (NoViableAltException)pce.InnerException;
            throw new EqlCompilerException($"Error: line {nve.OffendingToken.Line}:{nve.OffendingToken.Column} no viable alternative at input '{nve.OffendingToken.Text}'");
          }
          else if (pce.InnerException is InputMismatchException)
          {
            var ime = (InputMismatchException)pce.InnerException;
            var expecting = string.Join(", ", ime.GetExpectedTokens());
            throw new EqlCompilerException($"Error: line {ime.OffendingToken.Line}:{ime.OffendingToken.Column} extraneous input '{ime.OffendingToken.Text}' expecting {expecting}");
          }
          System.Console.WriteLine(pce.InnerException.GetType());
          throw new EqlCompilerException(pce.InnerException.Message);
        }
        throw new EqlCompilerException(pce.Message);
      }
    }

      /// Visits nodes of a DataQuery to build a list of linq expressions for each requested entity.
      /// We use EqlCompiler to compile the query and then build a Select() call for each field
    private class DataApiVisitor : EqlGrammerBaseVisitor<DataApiNode>
    {
      private ISchemaProvider _schemaProvider;
      private IMethodProvider _methodProvider;
      private IRelationHandler _relationHandler;
      // This is really just so we know what to use when visiting a field
      private Expression _selectContext;
      public DataApiVisitor(ISchemaProvider schemaProvider, IMethodProvider methodProvider, IRelationHandler relationHandler)
      {
        _schemaProvider = schemaProvider;
        _methodProvider = methodProvider;
        _relationHandler = relationHandler;
      }

      public override DataApiNode VisitField(EqlGrammerParser.FieldContext context)
      {
        var name = context.GetText();
        if (!_schemaProvider.TypeHasField(_selectContext.Type.Name, name))
            throw new EqlCompilerException($"Type {_selectContext.Type} does not have field or property {name}");
        var actualName = _schemaProvider.GetActualFieldName(_selectContext.Type.Name, name);
        var node = new DataApiNode(actualName, Expression.Property(_selectContext, actualName), null, null);
        return node;
      }
      public override DataApiNode VisitAliasExp(EqlGrammerParser.AliasExpContext context)
      {
        var name = context.name.GetText();
        var result = EqlCompiler.CompileWith(context.entity.GetText(), _selectContext, _schemaProvider, _methodProvider);
        var node = new DataApiNode(name, result.Expression, null, null);
        return node;
      }

      /// We compile each entityQuery with EqlCompiler and build a Select call from the fields
      public override DataApiNode VisitEntityQuery(EqlGrammerParser.EntityQueryContext context)
      {
          string name;
          string query;
          if (context.alias != null)
          {
            name = context.alias.name.GetText();
            query = context.alias.entity.GetText();
          }
          else
          {
            query = context.entity.GetText();
            name = query;
            if (name.IndexOf(".") > -1)
              name = name.Substring(0, name.IndexOf("."));
          }

          try
          {
            if (_selectContext == null)
            {
              // top level are queries on the context
              var exp = EqlCompiler.Compile(query, _schemaProvider, _methodProvider).Expression;
              var topLevelSelect = BuildDynamicSelectOnCollection(exp, name, context);
              // if (_relationHandler != null)
              // {
              // }
              return topLevelSelect;
            }
            // other levels are object selection. e.g. from the top level people query I am selecting all their children { field1, etc. }
            return BuildDynamicSelectForObjectGraph(query, name, context);
          }
          catch (EqlCompilerException ex)
          {
            //return DataApiNode.MakeError(name, $"Error compiling field or query '{query}'. {ex.Message}");
            throw DataApiException.MakeFieldCompileError(query, ex.Message);
          }
      }

      /// Given a syntax of someCollection { fields, to, selection, from, object }
      /// it will build a select assuming 'someCollection' is an IEnumerable
      private DataApiNode BuildDynamicSelectOnCollection(LambdaExpression exp, string name, EqlGrammerParser.EntityQueryContext context)
      {
        var elementType = exp.Body.Type.GetEnumerableType();
        var contextParameter = Expression.Parameter(elementType);

        var oldContext = _selectContext;
        _selectContext = contextParameter;
        // visit child fields. Will be field or entityQueries again
        var fieldExpressions = context.fields.children.Select(c => Visit(c)).Where(n => n != null).ToList();
        if (_relationHandler != null)
        {
          // Likely the EF handler to build .Include()s
          var node = _relationHandler.BuildNode(fieldExpressions, contextParameter, exp, name, _schemaProvider);
          _selectContext = oldContext;

          return node;
        }
        else {
          // Default we select out sub objects/relations. So Select(d => new {Field = d.Field, Relation = new { d.Relation.Field }})
          var selectExpression = DataApiExpressionUtil.SelectDynamic(contextParameter, exp.Body, fieldExpressions, _schemaProvider);
          var node = new DataApiNode(name, selectExpression, exp.Parameters.Any() ? exp.Parameters.First() : null, exp.Body);
          _selectContext = oldContext;
          return node;
        }
      }

      /// Given a syntax of someField { fields, to, selection, from, object }
      /// it will figure out if 'someField' is an IEnumerable or an istance of the object (not a collection) and build the correct select statement
      private DataApiNode BuildDynamicSelectForObjectGraph(string query, string name, EqlGrammerParser.EntityQueryContext context)
      {
        if (!_schemaProvider.TypeHasField(_selectContext.Type.Name, name))
          throw new EqlCompilerException($"Type {_selectContext.Type} does not have field or property {name}");
        name = _schemaProvider.GetActualFieldName(_selectContext.Type.Name, name);

        // Don't really like any of this, but...
        try
        {
          var result = EqlCompiler.CompileWith(query, _selectContext, _schemaProvider, _methodProvider);
          var exp = result.Expression;
          if (exp.Body.Type.IsEnumerable())
          {
            return BuildDynamicSelectOnCollection(exp, name, context);
          }

          var oldContext = _selectContext;
          _selectContext = exp.Body;
          // visit child fields. Will be field or entityQueries again
          var fieldExpressions = context.fields.children.Select(c => Visit(c)).Where(n => n != null).ToList();

          var newExp = DataApiExpressionUtil.CreateNewExpression(_selectContext, fieldExpressions, _schemaProvider);
          _selectContext = oldContext;
          return new DataApiNode(_schemaProvider.GetActualFieldName(_selectContext.Type.Name, name), newExp, exp.Parameters.Any() ? exp.Parameters.First() : null, exp.Body);
        }
        catch (EqlCompilerException ex)
        {
          throw DataApiException.MakeFieldCompileError(query, ex.Message);
        }
      }

      /// This is our top level node.
      /// {
      ///   entityQuery { fields [, field] },
      ///   entityQuery { fields [, field] },
      ///   ...
      /// }
      public override DataApiNode VisitDataQuery(EqlGrammerParser.DataQueryContext context)
      {
        var root = new DataApiNode("root", null, null, null);
        // Just visit each child node. All top level will be entityQueries
        var entities = context.children.Select(c => Visit(c)).ToList();
        root.Fields.AddRange(entities.Where(n => n != null));
        return root;
      }
    }
  }

  public class DataApiException : Exception
  {
    public DataApiException(string message) : base(message) { }
    public static DataApiException MakeFieldCompileError(string query, string message)
    {
      return new DataApiException($"Error compiling field or query '{query}'. {message}");
    }
  }
}
