using System;
using System.Collections.Generic;
using System.Text;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public class Summary<T>
    {
        public int? Count { get; set; }
        public T? Min { get; set; }
        public T? Max { get; set; }
        public T? Sum { get; set; }
        public T? Avg { get; set; }
    }
}
