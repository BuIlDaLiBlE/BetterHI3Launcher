using System.Collections.Generic;

namespace PartialZip.Models
{
    public class PartialZipInfo
    {
        internal ulong Length {get; set;}

        internal ulong CentralDirectoryEntries {get; set;}

        internal EndOfCentralDirectory EndOfCentralDirectory {get; set;}

        internal EndOfCentralDirectory64 EndOfCentralDirectory64 {get; set;}

        internal EndOfCentralDirectoryLocator64 EndOfCentralDirectoryLocator64 {get; set;}

        internal IEnumerable<CentralDirectoryHeader> CentralDirectory {get; set;}
    }
}
