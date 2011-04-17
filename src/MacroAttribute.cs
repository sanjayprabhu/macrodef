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
		public string name
		{
			get { return _name; }
			set { _name = value; }
		}

		/// <summary>
		/// Property name to store the value in - defaults to the name of the attribute.
		/// </summary>
		[TaskAttribute("property")]
		public string property
		{
			get { return _property; }
			set { _property = value; }
		}

		public string LocalPropertyName
		{
			get
			{
				if (_property == null)
					return _name;
				return _property;
			}
		}

		/// <summary>
		/// Default value - the property will be set to this if the attribute is not present.
		/// </summary>
		[TaskAttribute("default")]
		public string defaultValue
		{
			get { return _default; }
			set { _default = value; }
		}

		private string _name;
		private string _default;
		private string _property;
	}
}
