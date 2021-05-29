using PartialZip.Exceptions;
using System.IO;

namespace PartialZip.Models
{
	internal class EndOfCentralDirectory
	{
		internal static uint Size => 3 * sizeof(uint) + 5 * sizeof(ushort);

		internal EndOfCentralDirectory(byte[] buffer)
		{
			if(buffer.Length >= Size)
			{
				using(BinaryReader reader = new BinaryReader(new MemoryStream(buffer)))
				{
					Signature = reader.ReadUInt32();
					DiskNumber = reader.ReadUInt16();
					StartCentralDirectoryDiskNumber = reader.ReadUInt16();
					DiskCentralDirectoryRecordCount = reader.ReadUInt16();
					CentralDirectoryRecordCount = reader.ReadUInt16();
					CentralDirectorySize = reader.ReadUInt32();
					CentralDirectoryStartOffset = reader.ReadUInt32();
					CommentLength = reader.ReadUInt16();
				}
			}
			else
			{
				throw new PartialZipParsingException("Failed to parse end of central directory. The supplied buffer is too small");
			}
		}

		internal uint Signature {get; set;}

		internal ushort DiskNumber {get; set;}

		internal ushort StartCentralDirectoryDiskNumber {get; set;}

		internal ushort DiskCentralDirectoryRecordCount {get; set;}

		internal ushort CentralDirectoryRecordCount {get; set;}

		internal uint CentralDirectorySize {get; set;}

		internal uint CentralDirectoryStartOffset {get; set;}

		internal ushort CommentLength {get; set;}

		internal bool IsZip64 => DiskNumber == ushort.MaxValue || 
			StartCentralDirectoryDiskNumber == ushort.MaxValue ||
			DiskCentralDirectoryRecordCount == ushort.MaxValue || 
			CentralDirectorySize == uint.MaxValue ||
			CentralDirectoryStartOffset == uint.MaxValue || 
			CommentLength == ushort.MaxValue;
	}
}