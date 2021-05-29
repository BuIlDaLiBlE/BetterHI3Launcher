using PartialZip.Exceptions;
using System.IO;

namespace PartialZip.Models
{
	internal class LocalFileHeader
	{
		internal static uint Size => 4 * sizeof(uint) + 7 * sizeof(ushort);

		internal LocalFileHeader(byte[] buffer)
		{
			if(buffer.Length >= Size)
			{
				using (BinaryReader reader = new BinaryReader(new MemoryStream(buffer)))
				{
					Signature = reader.ReadUInt32();
					VersionNeeded = reader.ReadUInt16();
					Flags = reader.ReadUInt16();
					Compression = reader.ReadUInt16();
					ModifiedTime = reader.ReadUInt16();
					ModifiedDate = reader.ReadUInt16();
					CRC32 = reader.ReadUInt32();
					CompressedSize = reader.ReadUInt32();
					UncompressedSize = reader.ReadUInt32();
					FileNameLength = reader.ReadUInt16();
					ExtraFieldLength = reader.ReadUInt16();
				}
			}
			else
			{
				throw new PartialZipParsingException("Failed to parse local file header. The supplied buffer is too small");
			}
		}

		internal uint Signature {get; set;}

		internal ushort VersionNeeded {get; set;}

		internal ushort Flags {get; set;}

		internal ushort Compression {get; set;}

		internal ushort ModifiedTime {get; set;}

		internal ushort ModifiedDate {get; set;}

		internal uint CRC32 {get; set;}

		internal uint CompressedSize {get; set;}

		internal uint UncompressedSize {get; set;}

		internal ushort FileNameLength {get; set;}

		internal ushort ExtraFieldLength {get; set;}
	}
}