using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace BetterHI3Launcher.Utility;

public static partial class Extension
{
	public static async Task<string> CalculateHashAsyncCore(
		this HashAlgorithm hasher,
		Stream             stream,
		CancellationToken  token = default)
	{
		byte[] buffer = ArrayPool<byte>.Shared.Rent(64 << 10);

		try
		{
			int read;
			while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
			{
				token.ThrowIfCancellationRequested();
				hasher.TransformBlock(buffer, 0, read, buffer, 0);
			}

			hasher.TransformFinalBlock(buffer, 0, read);
			return BitConverter.ToString(hasher.Hash).Replace("-", string.Empty);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	public static string CalculateHashCore(
		this HashAlgorithm hasher,
		Stream             stream)
	{
		byte[] buffer = ArrayPool<byte>.Shared.Rent(64 << 10);

		try
		{
			int read;
			while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
			{
				hasher.TransformBlock(buffer, 0, read, buffer, 0);
			}

			hasher.TransformFinalBlock(buffer, 0, read);
			return BitConverter.ToString(hasher.Hash).Replace("-", string.Empty);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}
}
