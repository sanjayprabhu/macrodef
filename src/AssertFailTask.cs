using System;
using NAnt.Core;
using NAnt.Core.Attributes;

namespace Macrodef
{
	/// <summary>
	/// Makes sure that the nested block fails. Used in tests of macrodef.
	/// </summary>
	[TaskName("assert-fail")]
	public class AssertFailTask : TaskContainer
	{
		private string message = "Block expected to fail";
		
		/// <summary>
		/// Optional error message - thrown if nested block doesn't fail.
		/// </summary>
		[TaskAttribute("message")]
		public string Message
		{
			get { return message; }
			set { message = value; }
		}

		protected override void ExecuteTask()
		{
			bool failed = false;
			try
			{
				base.ExecuteTask();
			}
			catch(Exception e)
			{
				Log(Level.Info, "Expected exception: " + e.Message);
				Log(Level.Verbose, "Expected exception _was_ thrown: " + e);
				failed = true;
			}
			if(!failed)
			{
				throw new BuildException(message);
			}
		}
	}
}
