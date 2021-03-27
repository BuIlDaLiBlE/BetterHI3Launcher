using PartialZip.Exceptions;
using System.IO;

namespace PartialZip.Models
{
    internal class EndOfCentralDirectory64
    {
        internal static uint Size => 5 * sizeof(ulong) + 3 * sizeof(uint) + 2 * sizeof(ushort);

        internal EndOfCentralDirectory64(byte[] buffer)
        {
            if(buffer.Length >= Size)
            {
                using(BinaryReader reader = new BinaryReader(new MemoryStream(buffer)))
                {
                    Signature = reader.ReadUInt32();
                    EndOfCentralDirectoryRecordSize = reader.ReadUInt64();
                    VersionMadeBy = reader.ReadUInt16();
                    VersionNeeded = reader.ReadUInt16();
                    DiskNumber = reader.ReadUInt32();
                    StartCentralDirectoryDiskNumber = reader.ReadUInt32();
                    DiskCentralDirectoryRecordCount = reader.ReadUInt64();
                    CentralDirectoryRecordCount = reader.ReadUInt64();
                    CentralDirectorySize = reader.ReadUInt64();
                    CentralDirectoryStartOffset = reader.ReadUInt64();
                }
            }
            else
            {
                throw new PartialZipParsingException("Failed to parse end of ZIP64 central directory. The supplied buffer is too small");
            }
        }

        internal uint Signature {get; set;}

        internal ulong EndOfCentralDirectoryRecordSize {get; set;}

        internal ushort VersionMadeBy {get; set;}

        internal ushort VersionNeeded {get; set;}

        internal uint DiskNumber {get; set;}

        internal uint StartCentralDirectoryDiskNumber {get; set;}

        internal ulong DiskCentralDirectoryRecordCount {get; set;}

        internal ulong CentralDirectoryRecordCount {get; set;}

        internal ulong CentralDirectorySize {get; set;}

        internal ulong CentralDirectoryStartOffset {get; set;}
    }
}
