using System;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable
namespace BetterHI3Launcher.Utility.Json.Converter;

public class StructVersionJsonConverter : JsonConverter<StructVersion>
{
	public override StructVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		=> StructVersion.TryParse(reader.ValueSpan, out StructVersion result) ? result : default;

	public override void Write(Utf8JsonWriter writer, StructVersion value, JsonSerializerOptions options)
	{
		if (value == StructVersion.Empty && options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingDefault)
		{
			return;
		}

		Span<byte> charBuffer = stackalloc byte[48]; // Maximum possible char length
		if (!value.TryFormat(charBuffer, out int bytesWritten, ReadOnlySpan<char>.Empty, null))
		{
			throw new InvalidOperationException("Failed to format StructVersion.");
		}

		writer.WriteStringValue(charBuffer.Slice(0, bytesWritten));
	}
}
