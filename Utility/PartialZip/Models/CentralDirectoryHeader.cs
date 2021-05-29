using PartialZip.Exceptions;
using System.Collections.Generic;
using System.IO;

namespace PartialZip.Models
{
	internal class CentralDirectoryHeader
	{
		internal static uint Size => 6 * sizeof(uint) + 11 * sizeof(ushort);

		internal CentralDirectoryHeader(BinaryReader reader)
		{
			Signature = reader.ReadUInt32();
			VersionMade = reader.ReadUInt16();
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
			FileCommentLength = reader.ReadUInt16();
			DiskNumberStart = reader.ReadUInt16();
			InternalFileAttributes = reader.ReadUInt16();
			ExternalFileAttributes = reader.ReadUInt32();
			LocalHeaderOffset = reader.ReadUInt32();

			FileName = new string(reader.ReadChars(FileNameLength));

			if(ExtraFieldLength >= ExtendedInformationExtraField64.Size)
				ExtraField = new ExtendedInformationExtraField64(reader.ReadBytes(ExtraFieldLength));

			FileComment = new string(reader.ReadChars(FileCommentLength));
		}

		internal static IEnumerable<CentralDirectoryHeader> GetFromBuffer(byte[] buffer, ulong cdEntries)
		{
			if(buffer.Length >= EndOfCentralDirectory.Size)
			{
				using(BinaryReader reader = new BinaryReader(new MemoryStream(buffer)))
				{
					ulong entriesRead = 0;
					while(reader.BaseStream.Position + Size <= reader.BaseStream.Length && entriesRead < cdEntries)
					{
						yield return new CentralDirectoryHeader(reader);
						entriesRead++;
					}
				}
			}
			else
			{
				throw new PartialZipParsingException("Failed to parse central directory headers. The supplied buffer is too small");
			}
		}

		internal(ushort, ushort, ulong, ulong, ulong, uint) GetFileInfo()
		{
			int extraIndex = 0;

			ushort modifiedTime = (ModifiedTime == ushort.MaxValue) ? (ushort)ExtraField.ExtraField[extraIndex++] : ModifiedTime;
			ushort modifiedDate = (ModifiedDate == ushort.MaxValue) ? (ushort)ExtraField.ExtraField[extraIndex++] : ModifiedDate;
			ulong uncompressedSize = (UncompressedSize == uint.MaxValue) ? ExtraField.ExtraField[extraIndex++] : UncompressedSize;
			ulong compressedSize = (CompressedSize == uint.MaxValue) ? ExtraField.ExtraField[extraIndex++] : CompressedSize;
			ulong headerOffset = (LocalHeaderOffset == uint.MaxValue) ? ExtraField.ExtraField[extraIndex++] : LocalHeaderOffset;
			uint diskNum = (DiskNumberStart == ushort.MaxValue) ? (uint)ExtraField.ExtraField[extraIndex++] : DiskNumberStart;

			return (modifiedTime, modifiedDate, uncompressedSize, compressedSize, headerOffset, diskNum);
		}

		internal uint Signature {get; set;}

		internal ushort VersionMade {get; set;}

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

		internal ushort FileCommentLength {get; set;}

		internal ushort DiskNumberStart {get; set;}

		internal ushort InternalFileAttributes {get; set;}

		internal uint ExternalFileAttributes {get; set;}

		internal uint LocalHeaderOffset {get; set;}

		internal string FileName {get; set;}

		internal ExtendedInformationExtraField64 ExtraField {get; set;}

		internal string FileComment {get; set;}
	}
}