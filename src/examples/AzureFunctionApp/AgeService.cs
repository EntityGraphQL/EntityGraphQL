using System;

namespace demo
{
    /// <summary>
    /// Very poor example of injecting a service into fields
    /// </summary>
    public class AgeService
    {
        public int Calc(Person person)
        {
            return (int)((DateTime.Now - person.Dob).TotalDays / 365);
        }
    }
}