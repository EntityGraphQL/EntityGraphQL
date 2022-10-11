using System;

namespace demo
{
    /// <summary>
    /// Very poor example of injecting a service into fields
    /// </summary>
    public class AgeService
    {
        public int Calc(DateTime dob)
        {
            return (int)((DateTime.Now - dob).TotalDays / 365);
        }
    }
}