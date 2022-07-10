using EntityGraphQL.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EntityGraphQL.Schema.Validators
{
    public class DataAnnotationsValidator : IArgumentValidator
    {
        public Task ValidateAsync(ArgumentValidatorContext context)
        {
            ValidateObjectRecursive(context, context.Arguments);
            return Task.CompletedTask;
        }

        private void ValidateObjectRecursive(ArgumentValidatorContext context, object? obj)
        {
            if (obj == null)
                return;

            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(obj, new ValidationContext(obj), results, true))
            {
                results.ForEach(result =>
                {
                    if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                    {
                        context.AddError(result.ErrorMessage);
                    }
                });
            }

            var properties = obj!.GetType().GetProperties().Where(prop => prop.CanRead
                && prop.GetIndexParameters().Length == 0).ToList();

            foreach (var property in properties)
            {
                var value = property.GetValue(obj, null);

                if (property.PropertyType == typeof(string) || property.PropertyType.IsValueType)
                {
                    continue;
                }

                if (property.PropertyType.GetGenericArguments().Any() && property.PropertyType.GetGenericTypeDefinition() == typeof(RequiredField<>))
                {
                    if(value == null)
                    {
                        context.AddError($"missing required argument '{property.Name}'");
                    }
                    continue;
                }

                if (property.PropertyType.GetGenericArguments().Any() && property.PropertyType.GetGenericTypeDefinition() == typeof(EntityQueryType<>))
                {
                    continue;
                }

                if (value == null)
                {
                    continue;
                }

                if (value is IEnumerable asEnumerable)
                {
                    foreach (var enumObj in asEnumerable)
                    {
                        ValidateObjectRecursive(context, enumObj);
                    }
                }
                else
                {
                    ValidateObjectRecursive(context, value);
                }
            }
        }
    }
}
