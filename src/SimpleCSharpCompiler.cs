using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Globalization;
using System.IO;
using System.Reflection;
using NAnt.Core;
using NAnt.Core.Util;

namespace Macrodef
{
	// adapted from <script> task
	internal class SimpleCSharpCompiler
	{
	    private readonly string uniqueIdentifier;

	    public SimpleCSharpCompiler(string UniqueIdentifier)
		{
		    uniqueIdentifier = UniqueIdentifier;
		    Provider = CreateCodeDomProvider("Microsoft.CSharp.CSharpCodeProvider", "System");
			//Compiler = provider.CreateCompiler();
			//CodeGen = provider.CreateGenerator();
		}

	    //public readonly ICodeCompiler Compiler;
		//public readonly ICodeGenerator CodeGen;
		public readonly CodeDomProvider Provider;

	    public string PreCompiledDllPath
	    {
            get { return OuptutDllPath(uniqueIdentifier); }
	    }

		public string GetSourceCode(CodeCompileUnit compileUnit)
		{
			StringWriter sw = new StringWriter(CultureInfo.InvariantCulture);

			Provider.GenerateCodeFromCompileUnit(compileUnit, sw, null);
			return sw.ToString();
		}

		private static CompilerParameters CreateCompilerOptions(string uniqueIdentifier)
		{
			CompilerParameters options = new CompilerParameters();
			options.GenerateExecutable = false;
			// <script> task uses true - and hence doesn't work properly (second script that contains task defs fails)!
			options.GenerateInMemory = false;
		    options.OutputAssembly = OuptutDllPath(uniqueIdentifier);
			foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					if (!StringUtils.IsNullOrEmpty(asm.Location))
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

	    private static string OuptutDllPath(string uniqueIdentifier)
	    {
	        return Path.Combine(Path.GetTempPath(), uniqueIdentifier + ".dll");
	    }

		public Assembly CompileAssembly(CodeCompileUnit compileUnit)
		{
			CompilerParameters options = CreateCompilerOptions(uniqueIdentifier);

			CompilerResults results = Provider.CompileAssemblyFromDom(options, compileUnit);

			Assembly compiled;
			if (results.Errors.Count > 0)
			{
				string errors = "Errors:" + Environment.NewLine;
				foreach (CompilerError err in results.Errors)
				{
					errors += err.ToString() + Environment.NewLine;
				}
				errors += GetSourceCode(compileUnit);
				throw new BuildException(errors);
			}
			else
			{
				compiled = results.CompiledAssembly;
			}
			return compiled;
		}

		private static CodeDomProvider CreateCodeDomProvider(string typeName, string assemblyName)
		{
			Assembly providerAssembly = Assembly.Load(assemblyName);
			if (providerAssembly == null)
			{
				throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
				                                          ResourceUtils.GetString("NA2037"), assemblyName));
			}

			Type providerType = providerAssembly.GetType(typeName, true, true);

			object provider = Activator.CreateInstance(providerType);
			if (!(provider is CodeDomProvider))
			{
				throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
				                                          ResourceUtils.GetString("NA2038"), providerType.FullName));
			}
			return (CodeDomProvider) provider;
		}

	    public bool PrecompiledDllExists()
	    {
	        return File.Exists(OuptutDllPath(uniqueIdentifier));
	    }
	}
}
