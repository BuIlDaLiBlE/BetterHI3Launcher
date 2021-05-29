using System;

namespace PartialZip.Exceptions
{
	public class PartialZipFileNotFoundException : Exception
	{
		public PartialZipFileNotFoundException(string msg) : base(msg) {}
	}
}