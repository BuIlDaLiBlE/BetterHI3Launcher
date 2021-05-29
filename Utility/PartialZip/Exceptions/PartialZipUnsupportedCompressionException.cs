using System;

namespace PartialZip.Exceptions
{
	public class PartialZipUnsupportedCompressionException : Exception
	{
		public PartialZipUnsupportedCompressionException(string msg) : base(msg) {}
	}
}