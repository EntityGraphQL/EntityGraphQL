using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    public class CompileContext
    {
        private readonly HashSet<Type> servicesCollected = new();
        private readonly Dictionary<ParameterExpression, object> constantParameters = new();

        public HashSet<Type> Services { get => servicesCollected; }
        public IReadOnlyDictionary<ParameterExpression, object> ConstantParameters { get => constantParameters; }

        public void AddServices(IEnumerable<Type> services)
        {
            foreach (var service in services)
            {
                servicesCollected.Add(service);
            }
        }

        public void AddConstant(ParameterExpression parameterExpression, object value)
        {
            constantParameters[parameterExpression] = value;
        }
    }
}