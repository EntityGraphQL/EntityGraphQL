namespace EntityGraphQL
{
    /// <summary>
    /// Exceptions implementing this interface will have their message displayed in the 'errors' field even while running outside of Development
    /// </summary>
#pragma warning disable CA1711
    public interface IExposableException
#pragma warning restore CA1711
    {
    }
}
