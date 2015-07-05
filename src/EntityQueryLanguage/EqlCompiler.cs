using System;
using System.Linq;
using System.Linq.Expressions;
using Antlr4.Runtime;
using EntityQueryLanguage.Extensions;
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
  public class EqlCompiler
  {
    public EqlCompiler() {
      
    }

    public EqlResult Compile(string query) {
      return Compile(query, null, null);
    }
    public EqlResult Compile(string query, ISchemaProvider schemaProvider, IMethodProvider _methodProvider) {
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
    public EqlResult CompileWith(string query, Expression context, ISchemaProvider schemaProvider, IMethodProvider _methodProvider) {
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
    
    private class EqlGrammerVisitor : EqlGrammerBaseVisitor<Expression> {
      private Expression _currentContext;
      private ISchemaProvider _schemaProvider;
      private IMethodProvider _methodProvider;
      
      public EqlGrammerVisitor(Expression expression, ISchemaProvider schemaProvider, IMethodProvider methodProvider) {
        _currentContext = expression;
        _schemaProvider = schemaProvider;
        _methodProvider = methodProvider;
      }
      
    	public override Expression VisitBinary(EqlGrammerParser.BinaryContext context) {
        var left = Visit(context.left);
        var right = Visit(context.right);
        
        // we may need to do some converting here
        if (left.Type != right.Type) {
          ConvertLeftOrRight(left, right, out left, out right);
        }
        
        var bin = Expression.MakeBinary(MakeOperator(context.op.GetText()), left, right);
        return bin;
      }
    	public override Expression VisitExpr(EqlGrammerParser.ExprContext context) {
        var r = Visit(context.body);
        return r;
      }
    	public override Expression VisitIdentity(EqlGrammerParser.IdentityContext context) {
        // check that the schema has the property for the context
        var field = context.GetText();
        if (!_schemaProvider.EntityTypeHasField(_currentContext.Type, field)) {
          throw new EqlCompilerException($"Field or property '{field}' not found on current context '{_currentContext.Type.Name}'");
        }
        _currentContext = Expression.PropertyOrField(_currentContext, field);
        return _currentContext;
      }
    	public override Expression VisitInt(EqlGrammerParser.IntContext context) {
        return Expression.Constant(Int32.Parse(context.GetText()));
      }
    	public override Expression VisitDecimal(EqlGrammerParser.DecimalContext context) {
        return Expression.Constant(Decimal.Parse(context.GetText()));
      }
    	public override Expression VisitString(EqlGrammerParser.StringContext context) {
        return Expression.Constant(context.GetText().Trim('\''));
      }
      public override Expression VisitIfThenElse(EqlGrammerParser.IfThenElseContext context) {
        return Expression.Condition(CheckConditionalTest(Visit(context.test)), Visit(context.ifTrue), Visit(context.ifFalse));
      }
      public override Expression VisitIfThenElseInline(EqlGrammerParser.IfThenElseInlineContext context) {
        return Expression.Condition(CheckConditionalTest(Visit(context.test)), Visit(context.ifTrue), Visit(context.ifFalse));
      }
    	public override Expression VisitCall(EqlGrammerParser.CallContext context) {
        var method = context.method.GetText();
        if (!_methodProvider.EntityTypeHasMethod(_currentContext.Type, method))
          throw new EqlCompilerException($"Method '{method}' not found on current context '{_currentContext.Type.Name}'");
        // Keep the current context
        var outerContext = _currentContext;
        // some methods might have a different inner context (IEnumerable etc)
        var methodArgContext = _methodProvider.GetMethodContext(_currentContext, method);
        _currentContext = methodArgContext;
        // Compile the arguments with the new context          
        var args = context.arguments?.children.Select(c => Visit(c)).ToList();
        // build our method call
        var call = _methodProvider.MakeCall(outerContext, methodArgContext, method, args);
        _currentContext = call;
        return call;
      }
    	public override Expression VisitArgs(EqlGrammerParser.ArgsContext context) {
        return VisitChildren(context);
      }
    	//  public override Expression VisitCallOrId(EqlGrammerParser.CallOrIdContext context) {
      //    return VisitChildren(context);
      //  }
    	//  public override Expression VisitCallPath(EqlGrammerParser.CallPathContext context) {
      //    return VisitChildren(context);
      //  }
    	//  public override Expression VisitConstant(EqlGrammerParser.ConstantContext context) {
      //    return VisitChildren(context);
      //  }
    	//  public override Expression VisitOperator(EqlGrammerParser.OperatorContext context) {
      //    return VisitChildren(context);
      //  }
    	//  public override Expression VisitExpression(EqlGrammerParser.ExpressionContext context) {
      //    return VisitChildren(context);
      //  }
    	//  public override Expression VisitStartRule(EqlGrammerParser.StartRuleContext context) {
      //    Console.WriteLine($"----VisitStartRule {context.GetText()}");
      //    return VisitChildren(context);
      //  }
      
      /// Implements rules about comparing non-matching types.
      /// Nullable vs. non-nullable - the non-nullable gets converted to nullable
      /// int vs. uint - the uint gets down cast to int
      /// more to come...
      private void ConvertLeftOrRight(Expression leftIn, Expression rightIn, out Expression left, out Expression right) {
        left = leftIn;
        right = rightIn;
        if (left.Type.IsNullableType() && !right.Type.IsNullableType())
          right = Expression.Convert(right, left.Type);
        else if (right.Type.IsNullableType() && !left.Type.IsNullableType())
          left = Expression.Convert(left, right.Type);
          
        else if (left.Type == typeof(int) && right.Type == typeof(uint))
          right = Expression.Convert(right, left.Type);
        else if (left.Type == typeof(uint) && right.Type == typeof(int))
          left = Expression.Convert(left, right.Type);
      }
      
      private Expression CheckConditionalTest(Expression test) {
        if (test.Type != typeof(bool))
          throw new EqlCompilerException($"Expected boolean value in conditional test but found '{test}'");
        return test;
      }
      
      private ExpressionType MakeOperator(string op) {
        switch (op) {
          case "=": return ExpressionType.Equal;
          case "+": return ExpressionType.Add;
          case "-": return ExpressionType.Subtract;
          case "%": return ExpressionType.Modulo;
          case "^": return ExpressionType.Power;
          case "and": return ExpressionType.AndAlso;
          case "*": return ExpressionType.Multiply;
          case "or": return ExpressionType.OrElse;
          case "<=": return ExpressionType.LessThanOrEqual;
          case ">=": return ExpressionType.GreaterThanOrEqual;
          case "<": return ExpressionType.LessThan;
          case ">": return ExpressionType.GreaterThan;
          default: throw new EqlCompilerException($"Unsupported binary operator '{op}'");
        }
      }
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
