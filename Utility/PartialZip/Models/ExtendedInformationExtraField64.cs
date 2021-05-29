using PartialZip.Exceptions;
using System.IO;

namespace PartialZip.Models
{
	internal class ExtendedInformationExtraField64
	{
		internal static uint Size => 2 * sizeof(ushort);

		internal ExtendedInformationExtraField64(byte[] buffer)
		{
			if(buffer.Length >= Size)
			{
				using(BinaryReader reader = new BinaryReader(new MemoryStream(buffer)))
				{
					FieldTag = reader.ReadUInt16();
					FieldSize = reader.ReadUInt16();

					ExtraField = new ulong[(reader.BaseStream.Length - reader.BaseStream.Position) / sizeof(ulong)];

					for(int i = 0; i < ExtraField.Length; i++)
						ExtraField[i] = reader.ReadUInt64();
				}
			}
			else
			{
				throw new PartialZipParsingException("Failed to parse ZIP64 extended information field. The supplied buffer is too small");
			}
		}

		internal ushort FieldTag {get; set;}

		internal ushort FieldSize {get; set;}

		internal ulong[] ExtraField {get; set;}
	}
}