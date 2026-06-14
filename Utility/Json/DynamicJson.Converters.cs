#nullable enable
using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BetterHI3Launcher.Utility.Json;

public partial struct DynamicJson
{
	/// <summary>
	/// Convert the JSON value to a nullable boolean.
	/// </summary>
	public bool? ToBoolNullable()
	{
		return ValueKind switch
		{
			JsonValueKind.True   => true,
			JsonValueKind.False  => false,
			JsonValueKind.String => TryConvertFromString(Node),
			JsonValueKind.Number => TryConvertFromNumber(Node),
			_                    => null
		};

		static bool? TryConvertFromString(JsonNode? element)
		{
			if (element is not JsonValue jsonValue)
			{
				return false;
			}

			jsonValue.TryGetValue(out string? strValue);
			return bool.TryParse(strValue, out bool result) ? result : null;
		}

		static bool TryConvertFromNumber(JsonNode? element)
		{
			if (element is not JsonValue jsonValue)
			{
				return false;
			}

			if (jsonValue.TryGetValue(out int intValue))
			{
				return intValue != 0;
			}

			if (jsonValue.TryGetValue(out double doubleValue))
			{
				return doubleValue != 0.0;
			}
			return false;
		}
	}

	/// <summary>
	/// Convert the JSON value to a boolean. If the conversion fails, it returns false.
	/// </summary>
	public bool ToBool() => ToBoolNullable() ?? false;

	/// <summary>
	/// Convert the JSON value to a byte (<see cref="byte"/>).
	/// </summary>
	public byte ToByte()
		=> (Node as JsonValue)?.TryGetValue(out byte result) == true ? result : (byte)0;

	/// <summary>
	/// Convert the JSON value to a signed byte (<see cref="sbyte"/>).
	/// </summary>
	public sbyte ToSByte()
		=> (Node as JsonValue)?.TryGetValue(out sbyte result) == true ? result : (sbyte)0;

	/// <summary>
	/// Convert the JSON value to an unsigned short (<see cref="ushort"/>).
	/// </summary>
	public ushort ToUShort()
		=> (Node as JsonValue)?.TryGetValue(out ushort result) == true ? result : (ushort)0;

	/// <summary>
	/// Convert the JSON value to a signed short (short).
	/// </summary>
	public short ToShort()
		=> (Node as JsonValue)?.TryGetValue(out short result) == true ? result : (short)0;

	/// <summary>
	/// Convert the JSON value to an unsigned integer (<see cref="uint"/>).
	/// </summary>
	public uint ToUInt()
		=> (Node as JsonValue)?.TryGetValue(out uint result) == true ? result : 0;

	/// <summary>
	/// Convert the JSON value to a signed integer (<see cref="int"/>).
	/// </summary>
	public int ToInt()
		=> (Node as JsonValue)?.TryGetValue(out int result) == true ? result : 0;

	/// <summary>
	/// Convert the JSON value to an unsigned long (<see cref="ulong"/>).
	/// </summary>
	public ulong ToULong()
		=> (Node as JsonValue)?.TryGetValue(out ulong result) == true ? result : 0;

	/// <summary>
	/// Convert the JSON value to a signed long (<see cref="long"/>).
	/// </summary>
	public long ToLong()
		=> (Node as JsonValue)?.TryGetValue(out long result) == true ? result : 0;

	/// <summary>
	/// Convert the JSON value to a single-precision floating-point number (<see cref="float"/>).
	/// </summary>
	public float ToFloat()
		=> (Node as JsonValue)?.TryGetValue(out float result) == true ? result : 0f;

	/// <summary>
	/// Convert the JSON value to a double-precision floating-point number (<see cref="double"/>).
	/// </summary>
	public double ToDouble()
		=> (Node as JsonValue)?.TryGetValue(out double result) == true ? result : 0d;

	/// <summary>
	/// Convert the JSON value to a decimal number (<see cref="decimal"/>).
	/// </summary>
	public decimal ToDecimal()
		=> (Node as JsonValue)?.TryGetValue(out decimal result) == true ? result : 0m;

	/// <summary>
	/// Convert the JSON value to a <see cref="DateTime"/>. If the conversion fails, it returns the default value of <see cref="DateTime"/> (January 1, 0001, 00:00:00).
	/// </summary>
	public DateTime ToDateTime()
		=> (Node as JsonValue)?.TryGetValue(out DateTime result) == true ? result : default;

	/// <summary>
	/// Convert the JSON value to a <see cref="DateTimeOffset"/>. If the conversion fails, it returns the default value of <see cref="DateTimeOffset"/> (January 1, 0001, 00:00:00 +00:00).
	/// </summary>
	public DateTimeOffset ToDateTimeOffset()
		=> (Node as JsonValue)?.TryGetValue(out DateTimeOffset result) == true ? result : default;

	/// <summary>
	/// Convert the JSON value to a byte array by interpreting the string as a Base64-encoded value. If the conversion fails, it returns null.
	/// </summary>
	public byte[]? ToBytesNullable()
		=> (Node as JsonValue)?.TryGetValue(out byte[]? result) == true ? result : null;

	/// <summary>
	/// Convert the JSON value to a byte array by interpreting the string as a Base64-encoded value. If the conversion fails, it returns an empty byte array.
	/// </summary>
	public byte[] ToBytes() => ToBytesNullable() ?? [];

	/// <summary>
	/// Convert the JSON value to a <see cref="StructVersion"/>.
	/// </summary>
	public StructVersion ToStructVersion()
		=> (Node as JsonValue)?.TryGetValue(out string? result) == true && StructVersion.TryParse(result.AsSpan(), out StructVersion resultT) ? resultT : default;

	/// <summary>
	/// Convert the JSON value to a <see cref="Version"/>.
	/// </summary>
	public Version ToVersion()
		=> (Node as JsonValue)?.TryGetValue(out string? result) == true && Version.TryParse(result, out Version? resultT) ? resultT : new Version();

	/// <summary>
	/// Convert the JSON value to a string.
	/// </summary>
	public override string? ToString()
		=> (Node as JsonValue)?.TryGetValue(out string? result) == true ? result : null;
}
