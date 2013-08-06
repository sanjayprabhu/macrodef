using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using NAnt.Core;
using NAnt.Core.Util;

namespace Macrodef
{
    // adapted from <script> task
    internal class SimpleCSharpCompiler
    {
        private readonly string _uniqueIdentifier;
        private readonly CodeDomProvider _provider;
        private string _runtimeVersion;

        public SimpleCSharpCompiler(string uniqueIdentifier)
        {
            _uniqueIdentifier = uniqueIdentifier;
            _provider = CreateCodeDomProvider("Microsoft.CSharp.CSharpCodeProvider", "System");
        }

        public string PreCompiledDllPath
        {
            get { return OutputDllPath(); }
        }

        public string GetSourceCode(CodeCompileUnit compileUnit)
        {
            var sw = new StringWriter(CultureInfo.InvariantCulture);

            _provider.GenerateCodeFromCompileUnit(compileUnit, sw, null);

            return sw.ToString();
        }

        private CompilerParameters CreateCompilerOptions()
        {
            var options = new CompilerParameters
                {
                    GenerateExecutable = false,
                    GenerateInMemory = false, // <script> task uses true - and hence doesn't work properly (second script that contains task defs fails)!
                    OutputAssembly = PreCompiledDllPath
                };

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!string.IsNullOrEmpty(asm.Location))
                    {
                        options.ReferencedAssemblies.Add(asm.Location);
                    }
                }
                catch (NotSupportedException)
                {
                    // Ignore - this error is sometimes thrown by asm.Location 
                    // for certain dynamic assemblies
                }
            }

            return options;
        }

        private string OutputDllPath()
        {
            var fileName = string.Format("{0}_{1}.dll", _uniqueIdentifier, _runtimeVersion);
            
            return Path.Combine(Path.GetTempPath(), fileName);
        }

        public Assembly CompileAssembly(CodeCompileUnit compileUnit)
        {
            var options = CreateCompilerOptions();
            var results = _provider.CompileAssemblyFromDom(options, compileUnit);

            if (results.Errors.Count > 0)
            {
                var errors = new StringBuilder();
                errors.AppendLine("Errors:");
                
                foreach (CompilerError err in results.Errors)
                {
                    errors.AppendLine(err.ToString());
                }

                errors.Append(GetSourceCode(compileUnit));

                throw new BuildException(errors.ToString());
            }

            return results.CompiledAssembly;
        }

        private CodeDomProvider CreateCodeDomProvider(string typeName, string assemblyName)
        {
            var providerAssembly = Assembly.Load(assemblyName);
            
            if (providerAssembly == null)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, ResourceUtils.GetString("NA2037"), assemblyName));
            }

            _runtimeVersion = providerAssembly.ImageRuntimeVersion.Replace('.', '_');

            var providerType = providerAssembly.GetType(typeName, true, true);
            var provider = Activator.CreateInstance(providerType);
            
            if (!(provider is CodeDomProvider))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, ResourceUtils.GetString("NA2038"), providerType.FullName));
            }

            return (CodeDomProvider) provider;
        }

        public bool PrecompiledDllExists()
        {
            return File.Exists(PreCompiledDllPath);
        }
    }
}
