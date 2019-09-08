using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Xml;
using EntityGraphQL.Schema;
using McMaster.Extensions.CommandLineUtils;
using McMaster.NETCore.Plugins;
using RazorLight;

namespace dotnet_gql
{
    public class Program
    {
        [Argument(0, Description = "The full namespace of the context class to generate the schema from")]
        [Required]
        public string ContextClass { get; }

        [Option(ShortName = "n", Description = "Namespace of the generated code")]
        public string Namespace { get; } = "GraphqlSchema";

        [Option(ShortName = "p", Description = "Path to the project containing the context class. Defaults to current directory")]
        public string Project { get; } = ".";

        [Option(LongName = "classname", ShortName = "c", Description = "Generated class name")]
        public string OutputClassName { get; } = "GraphQLSchemaBuilder";
        
        [Option(LongName = "output", ShortName = "o", Description = "Output filename")]
        public string OutputFilename { get; } = "GraphQlSchema.cs";

        public static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);


    }
}
