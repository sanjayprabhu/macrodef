using System.Xml;
using NAnt.Core;
using NAnt.Core.Attributes;

namespace Macrodef
{
	/// <summary>
	/// Contains the template for the macro - the tasks that should be executed when the macro is called.
	/// </summary>
	[ElementName("sequential")]
	public class MacroDefSequential : Element
	{
		protected override bool CustomXmlProcessing
		{
			get { return true; }
		}

		internal XmlNode SequentialXml
		{
			get { return XmlNode; }
		}
	}
}
