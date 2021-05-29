using PartialZip.Exceptions;
using System.IO;

namespace PartialZip.Models
{
	internal class EndOfCentralDirectoryLocator64
	{
		internal static uint Size => 3 * sizeof(uint) + 1 * sizeof(ulong);

		internal EndOfCentralDirectoryLocator64(byte[] buffer)
		{
			if(buffer.Length >= Size)
			{
				using(BinaryReader reader = new BinaryReader(new MemoryStream(buffer)))
				{
					Signature = reader.ReadUInt32();
					StartCentralDirectory64DiskNumber = reader.ReadUInt32();
					EndOfCentralDirectory64StartOffset = reader.ReadUInt64();
					DiskCount = reader.ReadUInt32();
				}
			}
			else
			{
				throw new PartialZipParsingException("Failed to parse end of ZIP64 central directory locator. The supplied buffer is too small");
			}
		}

		internal uint Signature {get; set;}

		internal uint StartCentralDirectory64DiskNumber {get; set;}

		internal ulong EndOfCentralDirectory64StartOffset {get; set;}

		internal uint DiskCount {get; set;}
	}
}