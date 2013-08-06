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
    /// Defines a new task, called <see cref="TaskName"/>, which uses the
    /// <see cref="TasksToExecute"/> element as a template.
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
        private static readonly IDictionary Macrodefs = new Hashtable();

        private static readonly string[] DefaultNamespaces = {
            "System",
            "System.Collections",
            "System.Collections.Specialized",
            "System.IO",
            "System.Text",
            "System.Text.RegularExpressions",
            "NAnt.Core",
            "NAnt.Core.Attributes"
        };

        private readonly string _typeName = "nant" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        private Assembly _compiledAssembly;
        private XmlNode _macrodefNode;
        private string _contentHash = string.Empty;

        public MacroDefTask()
        {
            Elements = new ArrayList();
            Attributes = new ArrayList();
        }

        /// <summary>
        /// The tasks to execute when this macro is invoked.
        /// </summary>
        [BuildElement("sequential")]
        public MacroDefSequential TasksToExecute { get; set; }

        /// <summary>
        /// Attributes to the task - simple xml attributes on the macro invocation.
        /// </summary>
        [BuildElementCollection("attributes", "attribute", ElementType = typeof(MacroAttribute))]
        public ArrayList Attributes { get; private set; }

        /// <summary>
        /// Attributes to the task - xml child elements of the macro invocation.
        /// </summary>
        [BuildElementCollection("elements", "element", ElementType = typeof(MacroElement))]
        public ArrayList Elements { get; private set; }

        /// <summary>
        /// The name of the macro.
        /// </summary>
        [TaskAttribute("name", Required = true)]
        public string TaskName { get; set; }

        protected override void InitializeXml(XmlNode elementNode, PropertyDictionary properties, FrameworkInfo framework)
        {
            base.InitializeXml(elementNode, properties, framework);
            _macrodefNode = elementNode;
        }

        public static void ExecuteTask(string name, XmlNode xml, Task task)
        {
            var macrodef = (MacroDefTask)Macrodefs[name];
            macrodef.Invoke(xml, task);
        }

        private void Invoke(XmlNode xml, Task task)
        {
            var invocation = new MacroDefInvocation(TaskName, task, xml, Attributes, TasksToExecute, Elements);
            invocation.Execute();
        }

        // Bad... does way too many things, should be moved to respective classes.
        protected override void ExecuteTask()
        {
            if (Macrodefs.Contains(TaskName))
            {
                if (GetUniqueIdentifier() != ((MacroDefTask)Macrodefs[TaskName]).GetUniqueIdentifier())
                    throw new BuildException("Different MacroDef with the name : " + TaskName + " already exists. Cannot redefine.", Location);

                Log(Level.Info, "macrodef \"{0}\" already included.", TaskName);

                return;
            }

            Macrodefs[TaskName] = this;

            var simpleCSharpCompiler = new SimpleCSharpCompiler(GetUniqueIdentifier());

            if (simpleCSharpCompiler.PrecompiledDllExists())
            {
                TypeFactory.ScanAssembly(simpleCSharpCompiler.PreCompiledDllPath, this);
            }
            else
            {
                Log(Level.Info, "\"{0}\" New or Modified. Compiling.", TaskName);

                var compileUnit = GenerateCode();
                _compiledAssembly = simpleCSharpCompiler.CompileAssembly(compileUnit);
                LogGeneratedCode(simpleCSharpCompiler, compileUnit);
                TypeFactory.ScanAssembly(_compiledAssembly, this);
            }
        }

        private string GetUniqueIdentifier()
        {
            if (string.IsNullOrEmpty(_contentHash))
                _contentHash = GenerateHash(_macrodefNode);

            return string.Format("mdef_{0}_{1}", TaskName, _contentHash);
        }

        // Create a hash from the definition of the macrodef and return it
        private string GenerateHash(XmlNode xml)
        {
            var original = Encoding.UTF8.GetBytes(xml.InnerXml);
            var algorithm = SHA256.Create();
            var hashed = algorithm.ComputeHash(original);
            var hashstring = new StringBuilder();

            foreach (var @byte in hashed)
                hashstring.AppendFormat("{0:x2}", @byte);   //convert to hex string

            return hashstring.ToString();
        }

        private void LogGeneratedCode(SimpleCSharpCompiler simpleCSharpCompiler, CodeCompileUnit compileUnit)
        {
            Log(Level.Verbose, simpleCSharpCompiler.GetSourceCode(compileUnit));

            var compiledType = _compiledAssembly.GetType(_typeName);

            Log(Level.Verbose, "Created type {0} in {1}", compiledType, _compiledAssembly.Location);
        }

        public CodeCompileUnit GenerateCode()
        {
            var compileUnit = new CodeCompileUnit();

            var nspace = CreateNamespaceWithDefaultImports();
            compileUnit.Namespaces.Add(nspace);

            var taskClassDeclaration = CreateTaskClassDeclaration();
            nspace.Types.Add(taskClassDeclaration);

            AddGeneratedCodeToTaskClass(taskClassDeclaration);

            return compileUnit;
        }

        private void AddGeneratedCodeToTaskClass(CodeTypeDeclaration taskClassDeclaration)
        {
            var codeBody = @"
                private System.Xml.XmlNode _node;

                protected override void ExecuteTask()
                {
                    Macrodef.MacroDefTask.ExecuteTask(""" + TaskName + @""", _node, this);
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
            var nspace = new CodeNamespace();
            AddDefaultImports(nspace);

            return nspace;
        }

        private CodeTypeDeclaration CreateTaskClassDeclaration()
        {
            var typeDeclaration = new CodeTypeDeclaration(_typeName) { IsClass = true, TypeAttributes = TypeAttributes.Public };

            typeDeclaration.BaseTypes.Add(typeof(Task));

            var attributeDeclaration = new CodeAttributeDeclaration("TaskName");
            attributeDeclaration.Arguments.Add(new CodeAttributeArgument(new CodeVariableReferenceExpression("\"" + TaskName + "\"")));
            typeDeclaration.CustomAttributes.Add(attributeDeclaration);

            return typeDeclaration;
        }

        private static void AddDefaultImports(CodeNamespace nspace)
        {
            foreach (var nameSpace in DefaultNamespaces)
            {
                nspace.Imports.Add(new CodeNamespaceImport(nameSpace));
            }
        }
    }
}
