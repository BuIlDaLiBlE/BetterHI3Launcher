using PartialZip.Exceptions;
using PartialZip.Models;
using PartialZip.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PartialZip
{
	public class PartialZipDownloader
	{
		private string _archiveUrl;

		private HttpService _httpService;
		private DeflateService _deflateService;

		private PartialZipDownloader(string archiveUrl)
		{
			_archiveUrl = archiveUrl;

			_httpService = new HttpService(_archiveUrl);
			_deflateService = new DeflateService();
		}

		/// <summary>
		/// Returns a list of all filenames in the remote .zip archive
		/// </summary>
		/// <param name="archiveUrl">URL of the .zip archive</param>
		/// <returns>List of filenames</returns>
		public static async Task<IEnumerable<string>> GetFileList(string archiveUrl)
		{
			PartialZipDownloader downloader = new PartialZipDownloader(archiveUrl);
			PartialZipInfo info = await downloader.Open();

			return info.CentralDirectory.Select(cd => cd.FileName).OrderBy(f => f);
		}

		/// <summary>
		/// Downloads a specific file from a remote .zip archive
		/// </summary>
		/// <param name="archiveUrl">URL of the .zip archive</param>
		/// <param name="filePath">Path of the file in archive</param>
		/// <param name="writePath">Path where the file will be written</param>
		/// <param name="preserveTime">Preserve modification date</param>
		/// <returns>File content</returns>
		public static async Task DownloadFile(string archiveUrl, string filePath, string writePath)
		{
			PartialZipDownloader downloader = new PartialZipDownloader(archiveUrl);
			PartialZipInfo info = await downloader.Open();
			var content = await downloader.Download(info, filePath);
			File.WriteAllBytes(writePath, content.Item1);
			File.SetLastWriteTimeUtc(writePath, content.Item2);
		}

		private async Task<PartialZipInfo> Open()
		{
			bool supportsPartialZip = await _httpService.SupportsPartialZip();

			if(!supportsPartialZip)
				throw new PartialZipNotSupportedException("The web server does not support PartialZip as byte ranges are not accepted.");

			PartialZipInfo info = new PartialZipInfo();

			info.Length = await _httpService.GetContentLength();

			byte[] eocdBuffer = await _httpService.GetRange(info.Length - EndOfCentralDirectory.Size, info.Length - 1);
			info.EndOfCentralDirectory = new EndOfCentralDirectory(eocdBuffer);

			ulong startCD, endCD;

			if(info.EndOfCentralDirectory.IsZip64)
			{
				byte[] eocdLocator64Buffer = await _httpService.GetRange(info.Length - EndOfCentralDirectory.Size - EndOfCentralDirectoryLocator64.Size, info.Length - EndOfCentralDirectory.Size);
				info.EndOfCentralDirectoryLocator64 = new EndOfCentralDirectoryLocator64(eocdLocator64Buffer);

				byte[] eocd64Buffer = await _httpService.GetRange(info.EndOfCentralDirectoryLocator64.EndOfCentralDirectory64StartOffset, info.EndOfCentralDirectoryLocator64.EndOfCentralDirectory64StartOffset + EndOfCentralDirectory64.Size - 1);
				info.EndOfCentralDirectory64 = new EndOfCentralDirectory64(eocd64Buffer);

				(startCD, endCD) = (info.EndOfCentralDirectory64.CentralDirectoryStartOffset, info.EndOfCentralDirectory64.CentralDirectoryStartOffset + info.EndOfCentralDirectory64.CentralDirectorySize + EndOfCentralDirectory64.Size - 1);
				info.CentralDirectoryEntries = info.EndOfCentralDirectory64.CentralDirectoryRecordCount;
			}
			else
			{
				(startCD, endCD) = (info.EndOfCentralDirectory.CentralDirectoryStartOffset, info.EndOfCentralDirectory.CentralDirectoryStartOffset + info.EndOfCentralDirectory.CentralDirectorySize + EndOfCentralDirectory.Size - 1);
				info.CentralDirectoryEntries = info.EndOfCentralDirectory.CentralDirectoryRecordCount;
			}

			byte[] cdBuffer = await _httpService.GetRange(startCD, endCD);
			info.CentralDirectory = CentralDirectoryHeader.GetFromBuffer(cdBuffer, info.CentralDirectoryEntries);

			return info;
		}

		private async Task<Tuple<byte[], DateTime>> Download(PartialZipInfo info, string filePath)
		{
			CentralDirectoryHeader cd = info.CentralDirectory.FirstOrDefault(c => c.FileName == filePath);

			if(cd != null)
			{
				(ushort modifiedTime, ushort modifiedDate, ulong uncompressedSize, ulong compressedSize, ulong headerOffset, uint diskNum) = cd.GetFileInfo();

				byte[] localFileBuffer = await _httpService.GetRange(headerOffset, headerOffset + LocalFileHeader.Size - 1);
				LocalFileHeader localFileHeader = new LocalFileHeader(localFileBuffer);

				ulong start = headerOffset + LocalFileHeader.Size + localFileHeader.FileNameLength + localFileHeader.ExtraFieldLength;
				byte[] compressedContent = await _httpService.GetRange(start, start + compressedSize - 1);

				var dateTimeModified = ConvertDOSDateTime(modifiedDate, modifiedTime);

				switch(localFileHeader.Compression)
				{
					case 0:
						return Tuple.Create(compressedContent, dateTimeModified);
					case 8:
						return Tuple.Create(_deflateService.Inflate(compressedContent), dateTimeModified);
					default:
						throw new PartialZipUnsupportedCompressionException("Unknown compression.");
				}
			}

			throw new PartialZipFileNotFoundException($"Could not find file in archive.");
		}

		internal struct Systime
		{
			internal ushort Year;
			internal ushort Month;
			internal ushort DayOfWeek;
			internal ushort Day;
			internal ushort Hour;
			internal ushort Minute;
			internal ushort Second;
			internal ushort Milliseconds;
		}

		internal struct FileTime
		{
			internal uint dwLowDateTime;
			internal uint dwHighDateTime;
		}

		[DllImport("Kernel32.dll", CharSet = CharSet.Ansi)]
		private static extern bool DosDateTimeToFileTime(ushort wFatDate, ushort wFatTime, ref FileTime lpFileTime);
		[DllImport("Kernel32.dll", CharSet = CharSet.Ansi)]
		private static extern bool FileTimeToSystemTime(ref FileTime lpFileTime, ref Systime lpSystemTime);

		private static DateTime ConvertDOSDateTime(ushort date, ushort time)
		{
			FileTime fileTime = new FileTime();
			Systime systemTime = new Systime();

			DosDateTimeToFileTime(date, time, ref fileTime);
			FileTimeToSystemTime(ref fileTime, ref systemTime);

			return new DateTime(systemTime.Year, systemTime.Month, systemTime.Day, systemTime.Hour, systemTime.Minute, systemTime.Second + 1, DateTimeKind.Utc).AddHours(-3);
		}
	}
}