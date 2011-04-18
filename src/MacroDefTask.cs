using System;
using System.CodeDom;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using NAnt.Core;
using NAnt.Core.Attributes;

namespace Macrodef
{
	/// <summary>
	/// Defines a new task.
	/// </summary>
	/// <remarks>
	/// Derived from <a href="http://ant.apache.org/manual/CoreTasks/macrodef.html">Ant's macrodef task</a>.
	/// Defines a new task, called <see cref="name"/>, which uses the
	/// <see cref="StuffToDo"/> element as a template.
	/// The new task can have xml <see cref="Attributes"/> and xml child <see cref="Elements"/>.
	/// </remarks>
	/// <example>
	///   <para>Simple Macro.</para>
	///   <code>
	///   <![CDATA[
	/// <macrodef name="mytask">
	///		<sequential>
	///			<echo messasge="mytask invoked!"/>
	///		</sequential>
	/// </macrodef>
	/// <mytask/>
	///   ]]>
	///   </code>
	/// </example>
	/// <example>
	///   <para>Receive Parameters.</para>
	///   <code>
	///   <![CDATA[
	/// <macrodef name="assert-equals">
	///   <attributes>
	///     <attribute name="name"/>
	///     <attribute name="expected"/>
	///     <attribute name="actual"/>
	///   </attributes>
	///	  <sequential>
	///     <fail if="${ expected != actual}" message="${name}: expected '${expected}' but was '${actual}'"/>
	///   </sequential>
	/// </macrodef>
	///   ]]>
	///   </code>
	/// </example>
	/// <example>
	///   <para>Receive Callable Elements.</para>
	///   <code>
	///   <![CDATA[<macrodef name="macro-with-elements">
	///		<elements>
	///			<element name="element1"/>
	///		</elements>
	///		<sequential>
	///			<echo message="before element1"/>
	///			<element1/>
	///			<echo message="after element1"/>
	///		</sequential>
	///	</macrodef>
	///	<macro-with-elements>
	///		<element1>
	///			<echo message="element1"/>
	///		</element1>
	///	</macro-with-elements>
	///   ]]>
	///   </code>
	/// </example>
	[TaskName("macrodef")]
	public class MacroDefTask : Task
	{
		private string _name;
		private MacroDefSequential _sequential;
		private ArrayList _attributes = new ArrayList();
		private ArrayList _elements = new ArrayList();

		private string typeName = "nant" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

		private static IDictionary macrodefs = new Hashtable();

		/// <summary>
		/// The tasks to execute when this macro is invoked.
		/// </summary>
		[BuildElement("sequential")]
		public MacroDefSequential StuffToDo
		{
			get { return _sequential; }
			set { _sequential = value; }
		}

		/// <summary>
		/// Attributes to the task - simple xml attributes on the macro invocation.
		/// </summary>
		[BuildElementCollection("attributes", "attribute", ElementType=typeof (MacroAttribute))]
		public ArrayList Attributes
		{
			get { return _attributes; }
		}

		/// <summary>
		/// Attributes to the task - xml child elements of the macro invocation.
		/// </summary>
		[BuildElementCollection("elements", "element", ElementType=typeof (MacroElement))]
		public ArrayList Elements
		{
			get { return _elements; }
		}

		/// <summary>
		/// The name of the macro.
		/// </summary>
		[TaskAttribute("name", Required = true)]
		public string name
		{
			get { return _name; }
			set { _name = value; }
		}

	    protected override void InitializeXml(XmlNode elementNode, PropertyDictionary properties, FrameworkInfo framework)
	    {
	        base.InitializeXml(elementNode, properties, framework);
	        macrodefNode = elementNode;
	    }

		public static void ExecuteTask(string name, XmlNode xml, Task task)
		{
			MacroDefTask macrodef = (MacroDefTask) macrodefs[name];
			macrodef.Invoke(xml, task);
		}

		private void Invoke(XmlNode xml, Task task)
		{
			MacroDefInvocation invocation = new MacroDefInvocation(name, task, xml, _attributes, _sequential, _elements);
			invocation.Execute();
		}

		//Bad... does way too many things, should be moved to respective classes.
        protected override void ExecuteTask()
		{
            if(macrodefs.Contains(_name))
            {
                if (GetUniqueIdentifier() != ((MacroDefTask)macrodefs[_name]).GetUniqueIdentifier())
                    throw new BuildException("Different MacroDef with the name : " + _name + " already exists. Cannot redefine.", Location);
                Log(Level.Info, string.Format("macrodef \"{0}\" already included.", _name));
                return;
            }
		    macrodefs[_name] = this;

		    SimpleCSharpCompiler simpleCSharpCompiler = new SimpleCSharpCompiler(GetUniqueIdentifier());
		    
            if(simpleCSharpCompiler.PrecompiledDllExists())
            {
                TypeFactory.ScanAssembly(simpleCSharpCompiler.PreCompiledDllPath, this);
            }
            else
            {
                Log(Level.Info, string.Format("\"{0}\" New or Modified. Compiling.", _name));
                CodeCompileUnit compileUnit = GenerateCode();
                compiledAssembly = simpleCSharpCompiler.CompileAssembly(compileUnit);
                LogGeneratedCode(simpleCSharpCompiler, compileUnit);
                TypeFactory.ScanAssembly(compiledAssembly, this);
            }
		}

	    private string GetUniqueIdentifier()
	    {
            if (contentAsGuid == Guid.Empty)
                contentAsGuid = GenerateHash(macrodefNode);
            return _name + contentAsGuid; 
	    }

        //Create a 16 byte hash from the definition of the macrodef and return a guid constructed from that hash (guid so that we can use it in a filename)
        private Guid GenerateHash(XmlNode macrodefNode)
	    {
	        byte[] original = Encoding.Default.GetBytes(macrodefNode.InnerXml);
	        HashAlgorithm algorithm = MD5.Create();
	        byte[] hashed = algorithm.ComputeHash(original);
	        return new Guid(hashed);
	    }

		private void LogGeneratedCode(SimpleCSharpCompiler simpleCSharpCompiler, CodeCompileUnit compileUnit)
		{
			Log(Level.Verbose, simpleCSharpCompiler.GetSourceCode(compileUnit));
			Type compiledType = compiledAssembly.GetType(typeName);
			Log(Level.Verbose, "Created type " + compiledType + " in " + compiledAssembly.Location);
		}

		private static readonly string[] _defaultNamespaces = {
		                                                      	"System",
		                                                      	"System.Collections",
		                                                      	"System.Collections.Specialized",
		                                                      	"System.IO",
		                                                      	"System.Text",
		                                                      	"System.Text.RegularExpressions",
		                                                      	"NAnt.Core",
		                                                      	"NAnt.Core.Attributes"
		                                                      };

		private Assembly compiledAssembly;
	    private XmlNode macrodefNode;
	    private Guid contentAsGuid = Guid.Empty;

		public CodeCompileUnit GenerateCode()
		{
			CodeCompileUnit compileUnit = new CodeCompileUnit();

			CodeNamespace nspace = CreateNamespaceWithDefaultImports();
			compileUnit.Namespaces.Add(nspace);

			CodeTypeDeclaration taskClassDeclaration = CreateTaskClassDeclaration();
			nspace.Types.Add(taskClassDeclaration);

			AddGeneratedCodeToTaskClass(taskClassDeclaration);

			return compileUnit;
		}

		private void AddGeneratedCodeToTaskClass(CodeTypeDeclaration taskClassDeclaration)
		{
			string codeBody =
				@"
				private System.Xml.XmlNode _node;

				protected override void ExecuteTask()
				{
					Macrodef.MacroDefTask.ExecuteTask(""" +
				_name +
				@""", _node, this);
				}
				
				protected override void InitializeXml(System.Xml.XmlNode elementNode, PropertyDictionary properties, FrameworkInfo framework) 
				{
					_node = elementNode;
				}
			";
			taskClassDeclaration.Members.Add(new CodeSnippetTypeMember(codeBody));
		}

		private static CodeNamespace CreateNamespaceWithDefaultImports()
		{
			CodeNamespace nspace = new CodeNamespace();
			AddDefaultImports(nspace);
			return nspace;
		}

		private CodeTypeDeclaration CreateTaskClassDeclaration()
		{
			CodeTypeDeclaration typeDecl = new CodeTypeDeclaration(typeName);

			typeDecl.IsClass = true;
			typeDecl.TypeAttributes = TypeAttributes.Public;

			typeDecl.BaseTypes.Add(typeof (Task));

			CodeAttributeDeclaration attrDecl = new CodeAttributeDeclaration("TaskName");
			attrDecl.Arguments.Add(new CodeAttributeArgument(
			                       	new CodeVariableReferenceExpression("\"" + name + "\"")));

			typeDecl.CustomAttributes.Add(attrDecl);
			return typeDecl;
		}

		private static void AddDefaultImports(CodeNamespace nspace)
		{
			foreach (string nameSpace in _defaultNamespaces)
			{
				nspace.Imports.Add(new CodeNamespaceImport(nameSpace));
			}
		}
	}
}
