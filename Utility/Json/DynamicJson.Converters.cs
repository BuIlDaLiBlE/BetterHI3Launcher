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
	{
		if (Node is not JsonValue asJsonValue)
		{
			return 0;
		}

		if (asJsonValue.TryGetValue(out byte result) ||
			(asJsonValue.GetValueKind() == JsonValueKind.String &&
			 asJsonValue.TryGetValue(out string? strValue) &&
			 byte.TryParse(strValue, out result)))
		{
			return result;
		}

		return 0;
	}

	/// <summary>
	/// Convert the JSON value to a signed byte (<see cref="sbyte"/>).
	/// </summary>
	public sbyte ToSByte()
	{
		if (Node is not JsonValue asJsonValue)
		{
			return 0;
		}

		if (asJsonValue.TryGetValue(out sbyte result) ||
			(asJsonValue.GetValueKind() == JsonValueKind.String &&
			 asJsonValue.TryGetValue(out string? strValue) &&
			 sbyte.TryParse(strValue, out result)))
		{
			return result;
		}

		return 0;
	}

	/// <summary>
	/// Convert the JSON value to an unsigned short (<see cref="ushort"/>).
	/// </summary>
	public ushort ToUShort()
	{
		if (Node is not JsonValue asJsonValue)
		{
			return 0;
		}

		if (asJsonValue.TryGetValue(out ushort result) ||
			(asJsonValue.GetValueKind() == JsonValueKind.String &&
			 asJsonValue.TryGetValue(out string? strValue) &&
			 ushort.TryParse(strValue, out result)))
		{
			return result;
		}

		return 0;
	}

	/// <summary>
	/// Convert the JSON value to a signed short (short).
	/// </summary>
	public short ToShort()
	{
		if (Node is not JsonValue asJsonValue)
		{
			return 0;
		}

		if (asJsonValue.TryGetValue(out short result) ||
			(asJsonValue.GetValueKind() == JsonValueKind.String &&
			 asJsonValue.TryGetValue(out string? strValue) &&
			 short.TryParse(strValue, out result)))
		{
			return result;
		}

		return 0;
	}

	/// <summary>
	/// Convert the JSON value to an unsigned integer (<see cref="uint"/>).
	/// </summary>
	public uint ToUInt()
	{
		if (Node is not JsonValue asJsonValue)
		{
			return 0;
		}

		if (asJsonValue.TryGetValue(out uint result) ||
			(asJsonValue.GetValueKind() == JsonValueKind.String &&
			 asJsonValue.TryGetValue(out string? strValue) &&
			 uint.TryParse(strValue, out result)))
		{
			return result;
		}

		return 0;
	}

	/// <summary>
	/// Convert the JSON value to a signed integer (<see cref="int"/>).
	/// </summary>
	public int ToInt()
	{
		if (Node is not JsonValue asJsonValue)
		{
			return 0;
		}

		if (asJsonValue.TryGetValue(out int result) ||
			(asJsonValue.GetValueKind() == JsonValueKind.String &&
			 asJsonValue.TryGetValue(out string? strValue) &&
			 int.TryParse(strValue, out result)))
		{
			return result;
		}

		return 0;
	}

	/// <summary>
	/// Convert the JSON value to an unsigned long (<see cref="ulong"/>).
	/// </summary>
	public ulong ToULong()
	{
		if (Node is not JsonValue asJsonValue)
		{
			return 0;
		}

		if (asJsonValue.TryGetValue(out ulong result) ||
			(asJsonValue.GetValueKind() == JsonValueKind.String &&
			 asJsonValue.TryGetValue(out string? strValue) &&
			 ulong.TryParse(strValue, out result)))
		{
			return result;
		}

		return 0;
	}

	/// <summary>
	/// Convert the JSON value to a signed long (<see cref="long"/>).
	/// </summary>
	public long ToLong()
	{
		if (Node is not JsonValue asJsonValue)
		{
			return 0;
		}

		if (asJsonValue.TryGetValue(out long result) ||
			(asJsonValue.GetValueKind() == JsonValueKind.String &&
			 asJsonValue.TryGetValue(out string? strValue) &&
			 long.TryParse(strValue, out result)))
		{
			return result;
		}

		return 0;
	}

	/// <summary>
	/// Convert the JSON value to a single-precision floating-point number (<see cref="float"/>).
	/// </summary>
	public float ToFloat()
	{
		if (Node is not JsonValue asJsonValue)
		{
			return 0;
		}

		if (asJsonValue.TryGetValue(out float result) ||
			(asJsonValue.GetValueKind() == JsonValueKind.String &&
			 asJsonValue.TryGetValue(out string? strValue) &&
			 float.TryParse(strValue, out result)))
		{
			return result;
		}

		return 0;
	}

	/// <summary>
	/// Convert the JSON value to a double-precision floating-point number (<see cref="double"/>).
	/// </summary>
	public double ToDouble()
	{
		if (Node is not JsonValue asJsonValue)
		{
			return 0;
		}

		if (asJsonValue.TryGetValue(out double result) ||
			(asJsonValue.GetValueKind() == JsonValueKind.String &&
			 asJsonValue.TryGetValue(out string? strValue) &&
			 double.TryParse(strValue, out result)))
		{
			return result;
		}

		return 0;
	}

	/// <summary>
	/// Convert the JSON value to a decimal number (<see cref="decimal"/>).
	/// </summary>
	public decimal ToDecimal()
	{
		if (Node is not JsonValue asJsonValue)
		{
			return 0;
		}

		if (asJsonValue.TryGetValue(out decimal result) ||
			(asJsonValue.GetValueKind() == JsonValueKind.String &&
			 asJsonValue.TryGetValue(out string? strValue) &&
			 decimal.TryParse(strValue, out result)))
		{
			return result;
		}

		return 0;
	}

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
