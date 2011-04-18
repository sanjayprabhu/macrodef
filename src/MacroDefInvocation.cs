using System.Collections;
using System.Text;
using System.Xml;
using NAnt.Core;

namespace Macrodef
{
	internal class MacroDefInvocation
	{
		private readonly string name;
		private readonly Task task;
		private readonly XmlNode invocationXml;
		private readonly ArrayList attributeList;
		private readonly MacroDefSequential sequential;
		private readonly ArrayList elements;

		public MacroDefInvocation(string name, Task task,
		                          XmlNode invocationXml,
		                          ArrayList attributeList,
		                          MacroDefSequential sequential,
		                          ArrayList elements)
		{
			this.name = name;
			this.task = task;
			this.invocationXml = invocationXml;
			this.attributeList = attributeList;
			this.sequential = sequential;
			this.elements = elements;
		}

		public void Execute()
		{
			task.Log(Level.Verbose, "Running '" + name + "'");
			task.Project.Indent();
			try
			{
				PropertyDictionary oldPropertyValues = new PropertyDictionary(null);

				if (attributeList != null)
					SetUpProperties(attributeList, task, invocationXml, oldPropertyValues);

				if (sequential != null)
				{
					XmlNode invocationTasks = CreateInvocationTasks();
					ExecuteInvocationTasks(invocationTasks);
				}

				RestoreProperties(attributeList, task, oldPropertyValues);
			}
			finally
			{
				task.Project.Unindent();
			}
		}

		private XmlNode CreateInvocationTasks()
		{
			XmlNode invocationTasks = sequential.SequentialXml;
			if (elements.Count > 0)
			{
				invocationTasks = invocationTasks.CloneNode(true);
				foreach (MacroElement element in elements)
				{
					ReplaceMacroElementsInInvocationXml(element.name, invocationTasks);
				}

				Log(Level.Verbose, "Effective macro definition: " + invocationTasks.InnerXml);
			}
			return invocationTasks;
		}

		private void ReplaceMacroElementsInInvocationXml(string elementName, XmlNode invocationTasks)
		{
			XmlNodeList elementPlaceholders = invocationTasks.SelectNodes("nant:" + elementName, task.NamespaceManager);
			Log(Level.Verbose,
			    "Inserting " + elementPlaceholders.Count + " call(s) of '" + elementName + "' in " + invocationTasks.InnerXml);

			if (elementPlaceholders.Count > 0)
			{
				XmlElement invocationElementDefinition = GetInvocationElementDefinition(elementName);

				foreach (XmlElement elementPlaceholder in elementPlaceholders)
				{
					ReplaceElementPlaceHolderWithInvocationContents(invocationElementDefinition, elementPlaceholder);
				}
			}
		}

		private XmlElement GetInvocationElementDefinition(string elementName)
		{
			XmlElement invocationElementDefinition =
				invocationXml.SelectSingleNode("nant:" + elementName, task.NamespaceManager) as XmlElement;
			if (invocationElementDefinition == null)
				throw new BuildException("Element '" + elementName + "' must be defined");
			return invocationElementDefinition;
		}

		private void ReplaceElementPlaceHolderWithInvocationContents(XmlElement invocationElementDefinition,
		                                                             XmlElement elementPlaceHolder)
		{
			XmlNode parentElement = elementPlaceHolder.ParentNode;

			Log(Level.Verbose, "Replacing element " + elementPlaceHolder.OuterXml + " in " + parentElement.OuterXml);
			foreach (XmlNode definitionStep in invocationElementDefinition.ChildNodes)
			{
                //needs to be imported because the context where it came from could be different (different xml file)
                XmlNode actualNodeToBeInserted = parentElement.OwnerDocument.ImportNode(definitionStep, true);
			    parentElement.InsertBefore(actualNodeToBeInserted, elementPlaceHolder);
			}
			parentElement.RemoveChild(elementPlaceHolder);
		}

		private void Log(Level level, string s)
		{
			task.Log(level, s);
		}

		private void ExecuteInvocationTasks(XmlNode invocationTasks)
		{
			foreach (XmlNode childNode in invocationTasks)
			{
				if (!(childNode.NodeType == XmlNodeType.Element) ||
				    !childNode.NamespaceURI.Equals(task.NamespaceManager.LookupNamespace("nant")))
				{
					continue;
				}

				Task childTask = CreateChildTask(childNode);
				if (childTask != null)
				{
					childTask.Parent = this;
					childTask.Execute();
				}
			}
		}

		protected virtual Task CreateChildTask(XmlNode node)
		{
			return task.Project.CreateTask(node);
		}

		private static void RestoreProperties(ArrayList attributeList, Task task, PropertyDictionary oldValues)
		{
			PropertyDictionary projectProperties = task.Project.Properties;
			foreach (MacroAttribute macroAttribute in attributeList)
			{
				string localPropertyName = macroAttribute.LocalPropertyName;
				string oldValue = oldValues[localPropertyName];

				if (projectProperties.Contains(localPropertyName))
					projectProperties.Remove(localPropertyName);
				if (oldValue != null)
					projectProperties.Add(localPropertyName, oldValue);
			}
		}

		private static void SetUpProperties(ArrayList attributeList, Task task, XmlNode xml,
		                                    PropertyDictionary oldPropertyValues)
		{
			PropertyDictionary projectProperties = task.Project.Properties;
			StringBuilder logMessage = new StringBuilder();
			foreach (MacroAttribute macroAttribute in attributeList)
			{
				string attributeName = macroAttribute.name;
				XmlAttribute xmlAttribute = xml.Attributes[attributeName];
				string value = null;
				if (xmlAttribute != null)
				{
					value = projectProperties.ExpandProperties(xmlAttribute.Value, null);
				}
				else if (macroAttribute.defaultValue != null)
				{
					value = macroAttribute.defaultValue;
				}

				string localPropertyName = macroAttribute.LocalPropertyName;

				task.Log(Level.Debug, "Setting property " + localPropertyName + " to " + value);
				if (logMessage.Length > 0)
					logMessage.Append(", ");
				logMessage.Append(localPropertyName);
				logMessage.Append(" = '");
				logMessage.Append(value);
				logMessage.Append("'");

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
