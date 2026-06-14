#nullable enable
using System;
using System.Text.Json.Nodes;

namespace BetterHI3Launcher.Utility.Json;

public partial struct DynamicJson : IEquatable<DynamicJson>
{
	public bool Equals(DynamicJson other) => JsonNode.DeepEquals(Node, other.Node);

	public override bool Equals(object? obj) => obj is DynamicJson other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(Node);

	public static bool operator ==(DynamicJson left, DynamicJson right) => left.Equals(right);

	public static bool operator !=(DynamicJson left, DynamicJson right) => !(left == right);

	public static bool operator ==(DynamicJson? left, object? right) => right is DynamicJson rightJson &&
																	   (left?.Equals(rightJson) ?? false);

	public static bool operator !=(DynamicJson? left, object? right) => !(left == right);

	#region ToValues

	public static implicit operator bool?(DynamicJson obj) => obj.ToBoolNullable();

	public static implicit operator bool(DynamicJson obj) => obj.ToBool();

	public static implicit operator byte(DynamicJson obj) => obj.ToByte();

	public static implicit operator sbyte(DynamicJson obj) => obj.ToSByte();

	public static implicit operator ushort(DynamicJson obj) => obj.ToUShort();

	public static implicit operator short(DynamicJson obj) => obj.ToShort();

	public static implicit operator uint(DynamicJson obj) => obj.ToUInt();

	public static implicit operator int(DynamicJson obj) => obj.ToInt();

	public static implicit operator ulong(DynamicJson obj) => obj.ToULong();

	public static implicit operator long(DynamicJson obj) => obj.ToLong();

	public static implicit operator float(DynamicJson obj) => obj.ToFloat();

	public static implicit operator double(DynamicJson obj) => obj.ToDouble();

	public static implicit operator decimal(DynamicJson obj) => obj.ToDecimal();

	public static implicit operator DateTime(DynamicJson obj) => obj.ToDateTime();

	public static implicit operator DateTimeOffset(DynamicJson obj) => obj.ToDateTimeOffset();

	public static implicit operator StructVersion(DynamicJson obj) => obj.ToStructVersion();

	public static implicit operator Version(DynamicJson obj) => obj.ToVersion();

	public static implicit operator byte[](DynamicJson obj) => obj.ToBytes();

	public static implicit operator string?(DynamicJson obj) => obj.ToString();

	public static implicit operator JsonNode?(DynamicJson obj) => obj.Node;

	#endregion

	#region FromValues

	public static implicit operator DynamicJson(bool? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(byte? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(sbyte? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(ushort? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(short? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(uint? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(int? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(ulong? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(long? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(float? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(double? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(decimal? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(DateTime? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(DateTimeOffset? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(StructVersion? value) => new(JsonValue.Create(value?.ToString()), null, default);

	public static implicit operator DynamicJson(Version? value) => new(JsonValue.Create(value?.ToString()), null, default);

	public static implicit operator DynamicJson(byte[]? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(string? value) => new(JsonValue.Create(value), null, default);

	public static implicit operator DynamicJson(JsonNode? value) => new(value, null, default);

	#endregion
}
