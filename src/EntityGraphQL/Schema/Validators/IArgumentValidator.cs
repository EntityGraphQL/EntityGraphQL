using System.Threading.Tasks;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// An argument validator is invoked after the field expression and arguments are built but before execution of the query.
    /// Use it to perform validation before execution. To stop execution add any errors to the context.
    /// </summary>
    public interface IArgumentValidator
    {
        Task ValidateAsync(ArgumentValidatorContext context);
    }
}