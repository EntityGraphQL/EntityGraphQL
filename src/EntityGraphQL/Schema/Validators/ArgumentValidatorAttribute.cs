using System;

namespace EntityGraphQL.Schema;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class ArgumentValidatorAttribute : Attribute
{
    public IArgumentValidator Validator { get; }
    public ArgumentValidatorAttribute(Type validatorType)
    {
        if (!typeof(IArgumentValidator).IsAssignableFrom(validatorType))
            throw new ArgumentException($"{validatorType.Name} must implement {typeof(IArgumentValidator).Name}");
        Validator = (IArgumentValidator)Activator.CreateInstance(validatorType)!;
    }
}