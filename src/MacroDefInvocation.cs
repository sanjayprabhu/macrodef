using System.Collections;
using System.Text;
using System.Xml;
using NAnt.Core;

namespace Macrodef
{
    internal class MacroDefInvocation
    {
        private readonly string _name;
        private readonly Task _task;
        private readonly XmlNode _invocationXml;
        private readonly ArrayList _attributeList;
        private readonly MacroDefSequential _sequential;
        private readonly ArrayList _elements;

        public MacroDefInvocation(string name, Task task, XmlNode invocationXml, ArrayList attributeList, MacroDefSequential sequential, ArrayList elements)
        {
            _name = name;
            _task = task;
            _invocationXml = invocationXml;
            _attributeList = attributeList;
            _sequential = sequential;
            _elements = elements;
        }

        public void Execute()
        {
            _task.Log(Level.Verbose, "Running '{0}'", _name);

            try
            {
                _task.Project.Indent();

                var oldPropertyValues = new PropertyDictionary(null);

                if (_attributeList != null)
                    SetUpProperties(_attributeList, _task, _invocationXml, oldPropertyValues);

                if (_sequential != null)
                {
                    var invocationTasks = CreateInvocationTasks();
                    ExecuteInvocationTasks(invocationTasks);
                }

                RestoreProperties(_attributeList, _task, oldPropertyValues);
            }
            finally
            {
                _task.Project.Unindent();
            }
        }

        private XmlNode CreateInvocationTasks()
        {
            var invocationTasks = _sequential.SequentialXml;
            
            if (_elements.Count > 0)
            {
                invocationTasks = invocationTasks.CloneNode(true);
            
                foreach (MacroElement element in _elements)
                {
                    ReplaceMacroElementsInInvocationXml(element.ElementName, invocationTasks);
                }

                Log(Level.Verbose, "Effective macro definition: " + invocationTasks.InnerXml);
            }

            return invocationTasks;
        }

        private void ReplaceMacroElementsInInvocationXml(string elementName, XmlNode invocationTasks)
        {
            var elementPlaceholders = invocationTasks.SelectNodes("nant:" + elementName, _task.NamespaceManager);
            
            Log(Level.Verbose, "Inserting {0} call(s) of '{1}' in {2}", elementPlaceholders.Count, elementName, invocationTasks.InnerXml);

            if (elementPlaceholders.Count > 0)
            {
                var invocationElementDefinition = GetInvocationElementDefinition(elementName);

                foreach (XmlElement elementPlaceholder in elementPlaceholders)
                {
                    ReplaceElementPlaceHolderWithInvocationContents(invocationElementDefinition, elementPlaceholder);
                }
            }
        }

        private XmlElement GetInvocationElementDefinition(string elementName)
        {
            var invocationElementDefinition = _invocationXml.SelectSingleNode("nant:" + elementName, _task.NamespaceManager) as XmlElement;

            if (invocationElementDefinition == null)
                throw new BuildException("Element '" + elementName + "' must be defined");
            
            return invocationElementDefinition;
        }

        private void ReplaceElementPlaceHolderWithInvocationContents(XmlElement invocationElementDefinition, XmlElement elementPlaceHolder)
        {
            var parentElement = elementPlaceHolder.ParentNode;

            Log(Level.Verbose, "Replacing element {0} in {1}", elementPlaceHolder.OuterXml, parentElement.OuterXml);
            
            foreach (XmlNode definitionStep in invocationElementDefinition.ChildNodes)
            {
                // needs to be imported because the context where it came from could be different (different xml file)
                var actualNodeToBeInserted = parentElement.OwnerDocument.ImportNode(definitionStep, true);

                parentElement.InsertBefore(actualNodeToBeInserted, elementPlaceHolder);
            }
            
            parentElement.RemoveChild(elementPlaceHolder);
        }

        private void Log(Level level, string s, params object[] args)
        {
            _task.Log(level, s, args);
        }

        private void ExecuteInvocationTasks(XmlNode invocationTasks)
        {
            foreach (XmlNode childNode in invocationTasks)
            {
                if (childNode.NodeType != XmlNodeType.Element || !childNode.NamespaceURI.Equals(_task.NamespaceManager.LookupNamespace("nant")))
                {
                    continue;
                }

                var childTask = CreateChildTask(childNode);
                
                if (childTask != null)
                {
                    childTask.Parent = this;
                    childTask.Execute();
                }
            }
        }

        protected virtual Task CreateChildTask(XmlNode node)
        {
            return _task.Project.CreateTask(node);
        }

        private static void RestoreProperties(ArrayList attributeList, Task task, PropertyDictionary oldValues)
        {
            var projectProperties = task.Project.Properties;
            
            foreach (MacroAttribute macroAttribute in attributeList)
            {
                var localPropertyName = macroAttribute.LocalPropertyName;
                var oldValue = oldValues[localPropertyName];

                if (projectProperties.Contains(localPropertyName))
                    projectProperties.Remove(localPropertyName);

                if (oldValue != null)
                    projectProperties.Add(localPropertyName, oldValue);
            }
        }

        private static void SetUpProperties(ArrayList attributeList, Task task, XmlNode xml, PropertyDictionary oldPropertyValues)
        {
            var projectProperties = task.Project.Properties;
            var logMessage = new StringBuilder();
            
            foreach (MacroAttribute macroAttribute in attributeList)
            {
                var attributeName = macroAttribute.AttributeName;
                var xmlAttribute = xml.Attributes[attributeName];
                string value = null;
                
                if (xmlAttribute != null)
                {
                    value = projectProperties.ExpandProperties(xmlAttribute.Value, null);
                }
                else if (macroAttribute.DefaultValue != null)
                {
                    value = macroAttribute.DefaultValue;
                }

                var localPropertyName = macroAttribute.LocalPropertyName;

                task.Log(Level.Debug, "Setting property {0} to {1}", localPropertyName, value);

                if (logMessage.Length > 0) logMessage.Append(", ");

                logMessage.AppendFormat("{0} = '{1}'", localPropertyName, value);

                if (projectProperties.Contains(localPropertyName))
                {
                    oldPropertyValues.Add(localPropertyName, projectProperties[localPropertyName]);
                    projectProperties.Remove(localPropertyName);
                }

                if (value != null)
                    projectProperties.Add(localPropertyName, value);
            }

            task.Log(Level.Info, logMessage.ToString());
        }
    }
}
