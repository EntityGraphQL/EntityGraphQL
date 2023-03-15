using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace EntityGraphQL.Schema.Validators
{
    public class DataAnnotationsValidator : IArgumentValidator
    {
        public Task ValidateAsync(ArgumentValidatorContext context)
        {
            ValidateObjectRecursive(context, context.Arguments);
            ValidateMethodArguments(context);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Validate the arguments against the validation attributes in the specified method
        /// </summary>
        /// <param name="context"></param>
        private static void ValidateMethodArguments(ArgumentValidatorContext context)
        {
            if (context.Method != null)
            {
                if (context.Arguments is IEnumerable asEnumerable)
                {
                    var value = asEnumerable.GetEnumerator();
                    ParameterInfo[] parameters = context.Method.GetParameters();

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (!value.MoveNext())
                            break;

                        var customAttributes = parameters![i].GetCustomAttributes(typeof(ValidationAttribute), true).OfType<ValidationAttribute>();
                        if (customAttributes.Any())
                        {
                            var results = new List<ValidationResult>();
                            if (!Validator.TryValidateValue(value.Current, new ValidationContext(value.Current), results, customAttributes))
                            {
                                results.ForEach(result =>
                                {
                                    if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                                    {
                                        context.AddError(result.ErrorMessage);
                                    }
                                });
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Validate the values of the object recursively against the validation attributes on the object itself 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="obj"></param>
        private void ValidateObjectRecursive(ArgumentValidatorContext context, object? obj)
        {
            if (obj == null)
                return;

            var results = new List<ValidationResult>();

            if (obj is IEnumerable asEnumerable)
            {
                foreach (var enumObj in asEnumerable)
                {
                    ValidateObjectRecursive(context, enumObj);
                }
            }
            else
            {
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
                        if (value == null)
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


                    ValidateObjectRecursive(context, value);
                }
            }
        }
    }
}
