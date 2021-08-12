using System.Linq.Expressions;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public interface IFieldExtension
    {
        void Configure(ISchemaProvider schema, Field field);
        Expression Invoke(Field field);
    }
}