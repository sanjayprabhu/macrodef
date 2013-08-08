using NAnt.Core;
using NAnt.Core.Attributes;

namespace Macrodef
{
	/// <summary>
	/// Describes a parameter/attribute of the macro. Can be accessed as properties within the macro definition (i.e. ${prop}).
	/// </summary>
	[ElementName("attribute")]
	public class MacroAttribute : Element
	{
	    /// <summary>
	    /// The name of the attribute.
	    /// </summary>
	    [TaskAttribute("name")]
	    public string AttributeName { get; set; }

	    /// <summary>
	    /// Property name to store the value in - defaults to the name of the attribute.
	    /// </summary>
	    [TaskAttribute("property")]
	    public string Property { get; set; }

	    /// <summary>
	    /// Default value - the property will be set to this if the attribute is not present.
	    /// </summary>
	    [TaskAttribute("default")]
	    public string DefaultValue { get; set; }

	    public string LocalPropertyName
		{
			get
			{
				if (Property == null)
					return AttributeName;

				return Property;
			}
		}
	}
}
