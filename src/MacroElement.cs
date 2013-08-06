using NAnt.Core;
using NAnt.Core.Attributes;

namespace Macrodef
{
	/// <summary>
	/// Describe nested elements that can be supplied to the macrodef. These elements are callable by name.
	/// </summary>
	[ElementName("element")]
	public class MacroElement : Element
	{
	    /// <summary>
	    /// The name of the element.
	    /// </summary>
	    [TaskAttribute("name")]
	    public string ElementName { get; set; }
	}
}
