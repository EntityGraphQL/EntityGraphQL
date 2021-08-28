namespace EntityGraphQL.Schema.FieldExtensions
{
    public class FilterArgs<T>
    {
        public EntityQueryType<T> filter { get; set; } = ArgumentHelper.EntityQuery<T>();
    }
}