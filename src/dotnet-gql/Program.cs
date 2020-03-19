using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
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

        private async void OnExecute()
        {
            try
            {
                Console.WriteLine($"Building {Project}...");
                // make sure the project is built
                var buildProc = System.Diagnostics.Process.Start("dotnet", $"build {Project}");
                buildProc.WaitForExit();

                Console.WriteLine($"Loading class {ContextClass} from {Project}");
                var contextType = LoadContextClass();

                // We're calling ISchemaProvider schema = SchemaBuilder.FromObject<TContext>();
                // let's us do it with type safety
                Expression<Func<ISchemaProvider>> call = () => SchemaBuilder.FromObject<object, object>(true, true);
                var method = ((MethodCallExpression)call.Body).Method;
                method = method.GetGenericMethodDefinition().MakeGenericMethod(contextType);
                var schema = method.Invoke(null, new object[] {true}) as ISchemaProvider;

                Console.WriteLine($"Generating {Namespace}.{OutputClassName}, outputting to {OutputFilename}");

                // pass the schema to the template
                var engine = new RazorLightEngineBuilder()
                    .UseEmbeddedResourcesProject(typeof(Program))
                    .UseMemoryCachingProvider()
                    .Build();

                string result = await engine.CompileRenderAsync("template.cshtml", new {
                    Namespace = Namespace,
                    OutputClassName = OutputClassName,
                    ContextClass = ContextClass,
                    Schema = schema
                });
                File.WriteAllText(OutputFilename, result);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.ToString());
            }
        }

        private Type LoadContextClass()
        {
            // find project directory and proj file
            string projFile = null;
            string projPath = null;
            if (File.Exists(Project))
            {
                projFile = Project;
                projPath = Path.GetDirectoryName(Project);
            }
            else
            {
                // is directory
                projFile = Directory.GetFiles(Project, "*proj").FirstOrDefault();
                projPath = Project;
                if (projFile == null)
                {
                    throw new ArgumentException($"Could not find csproj file in path {Project}");
                }
            }

            var xml = new XmlDocument();
            xml.Load(new FileStream(projFile, FileMode.Open));
            var assemblyName = xml.GetElementsByTagName("AssemblyName").Count > 0 ? xml.GetElementsByTagName("AssemblyName").Item(0).InnerText : Path.GetFileNameWithoutExtension(projFile);
            var targetFramework = xml.GetElementsByTagName("TargetFramework").Item(0).InnerText;
            var assemblyPath = $"{Path.GetFullPath(projPath)}/bin/Debug/{targetFramework}/{assemblyName}.dll";

            if (!File.Exists(assemblyPath))
            {
                throw new ArgumentException($"Could not find assembly. Looking for {assemblyPath}");
            }
            Console.WriteLine($"Loading assembly from {assemblyPath}");

            var plugin = PluginLoader.CreateFromAssemblyFile(assemblyPath);
            var assembly = plugin.LoadAssembly(assemblyName);
            var type = assembly.GetType(ContextClass);
            return type;
        }
    }
}
