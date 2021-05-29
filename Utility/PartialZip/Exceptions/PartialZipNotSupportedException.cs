using System;

namespace PartialZip.Exceptions
{
	public class PartialZipNotSupportedException : Exception
	{
		public PartialZipNotSupportedException(string msg) : base(msg) {}
	}
}