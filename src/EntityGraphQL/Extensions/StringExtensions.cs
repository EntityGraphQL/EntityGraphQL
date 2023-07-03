using System;
using System.Globalization;
using System.Linq;

namespace EntityGraphQL.Extensions
{
    public static class StringExtensions
    {
        public static string FirstCharToUpper(this string input)
        {
            return input switch
            {
                null => throw new ArgumentNullException(nameof(input)),
                "" => throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input)),
                _ => input.First().ToString().ToUpper(CultureInfo.InvariantCulture) + input[1..]
            };
        }

        public static string FirstCharToLower(this string input)
        {
            return input switch
            {
                null => throw new ArgumentNullException(nameof(input)),
                "" => throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input)),
                _ => input.First().ToString().ToLower(CultureInfo.InvariantCulture) + input[1..]
            };
        }
    }
}
