using System;
using System.Collections.Generic;
using System.Text;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public class SummarizeInput
    {
        public List<string>? GroupBy { get; set; } = new List<string>();
    }
}
