using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Class to hold required services ro constant parameters required to execute the compiled query
    /// </summary>
    public class CompileContext
    {
        private readonly HashSet<Type> servicesCollected = new();
        private readonly Dictionary<ParameterExpression, object> constantParameters = new();
        private readonly Dictionary<IField, ParameterExpression> constantParametersForField = new();

        public HashSet<Type> Services { get => servicesCollected; }
        public IReadOnlyDictionary<ParameterExpression, object> ConstantParameters { get => constantParameters; }

        public void AddServices(IEnumerable<Type> services)
        {
            foreach (var service in services)
            {
                servicesCollected.Add(service);
            }
        }

        public void AddConstant(IField? fromField, ParameterExpression parameterExpression, object value)
        {
            constantParameters[parameterExpression] = value;
            if (fromField != null)
                constantParametersForField[fromField] = parameterExpression;
        }

        public ParameterExpression? GetConstantParameterForField(IField field)
        {
            if (constantParametersForField.TryGetValue(field, out var param))
                return param;
            return null;
        }
    }
}