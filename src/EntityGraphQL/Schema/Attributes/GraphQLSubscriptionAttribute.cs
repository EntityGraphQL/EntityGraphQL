using System;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Marks the method in the class as a Subscription for EntityGraphQL to include in the Subscription Type.
    /// You need to add the subscription class (containing the method) to the schema using <code>schema.AddSubscriptionFrom<MyClass>();</code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class GraphQLSubscriptionAttribute : Attribute
    {
        public GraphQLSubscriptionAttribute(string description = "")
        {
            this.Description = description;
        }

        public string Description { get; set; }
    }
}