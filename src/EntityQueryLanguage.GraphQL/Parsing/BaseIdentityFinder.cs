using EntityQueryLanguage.Grammer;

namespace EntityQueryLanguage.GraphQL.Parsing
{
    internal class BaseIdentityFinder : EqlGrammerBaseVisitor<string>
    {
        public override string VisitIdentity(EqlGrammerParser.IdentityContext context)
        {
            return context.GetText();
        }

        public override string VisitGqlcall(EqlGrammerParser.GqlcallContext context)
        {
            return context.method.GetText();
        }
    }
}