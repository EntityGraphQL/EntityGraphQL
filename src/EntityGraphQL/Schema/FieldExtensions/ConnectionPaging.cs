using System;
using System.Linq.Expressions;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.Connections;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public static class UseConnectionPagingExtension
    {
        public static Field UseConnectionPaging(this Field field)
        {
            field.AddExtension(new ConnectionPagingExtension());
            return field;
        }
    }

    public class ConnectionPagingExtension : IFieldExtension
    {
        /// <summary>
        /// Configure the field for a connection style paging field
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="field"></param>
        public void Configure(ISchemaProvider schema, Field field)
        {
            if (!field.Resolve.Type.IsEnumerableOrArray())
                throw new ArgumentException($"Expression for field {field.Name} must be a collection to use ConnectionPagingExtension. Found type {field.ReturnType.TypeDotnet}");

            // Make sure required types are in the schema
            if (!schema.HasType("PageInfo"))
                schema.AddType(typeof(ConnectionPageInfo), "PageInfo", "Metadata about a page of data").AddAllFields();
            var edgeName = $"{field.ReturnType.SchemaType.Name}Edge";

            if (!schema.HasType(edgeName))
            {
                var edgeType = typeof(ConnectionEdge<>)
                    .MakeGenericType(field.ReturnType.TypeDotnet.GetEnumerableOrArrayType());
                schema.AddType(edgeType, edgeName, "Metadata about an edge of page result").AddAllFields();
            }

            Type returnType;
            var connectionName = $"{field.ReturnType.SchemaType.Name}Connection";
            if (!schema.HasType(connectionName))
            {
                returnType = typeof(Connection<>)
                    .MakeGenericType(field.ReturnType.TypeDotnet.GetEnumerableOrArrayType());
                schema.AddType(returnType, connectionName, "Metadata about a person connection (paging over people)").AddAllFields();
            }
            else
            {
                returnType = schema.Type(connectionName).ContextType;
            }

            field.UpdateReturnType(SchemaBuilder.MakeGraphQlType(schema, returnType, connectionName));

            // Update field arguments
            field.AddArguments(new ConnectionArgs());

            // Rebuild expression ready for completion at runtime (we need to know the selection fields to complete)
            // var newExp = ;
            // field.UpdateExpression(newExp);
        }

        public Expression Invoke(Field field)
        {
            return field.Resolve;
        }
    }
}