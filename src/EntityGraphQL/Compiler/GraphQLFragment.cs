namespace EntityGraphQL.Compiler
{
    public class GraphQLFragment
    {
        private string name;
        private string typeName;

        public GraphQLFragment(string name, string typeName)
        {
            this.name = name;
            this.typeName = typeName;
        }
    }
}