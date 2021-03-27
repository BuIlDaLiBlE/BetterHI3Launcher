using System;

namespace PartialZip.Exceptions
{
    public class PartialZipParsingException : Exception
    {
        public PartialZipParsingException(string msg) : base(msg) {}
    }
}
