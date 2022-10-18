namespace EntityGraphQL.Schema.FieldExtensions
{
    public class FilterArgs<T>
    {
        public EntityQueryType<T>? Filter { get; set; } = ArgumentHelper.EntityQuery<T>();
    }
}