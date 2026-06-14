#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BetterHI3Launcher.Utility.Json;

/// <summary>
/// A lightweight wrapper around <see cref="JsonElement"/> that provides dynamic access to JSON value, properties and array elements.
/// </summary>
public partial struct DynamicJson
{
	private readonly JsonNodeOptions _nodeOptions;

	/// <summary>
	/// The parent <see cref="JsonNode"/> of the current JSON <see cref="Node"/>. If the current <see cref="Node"/> is the root, this will be <see langword="null"/>.
	/// </summary>
	public readonly JsonNode? Parent;

	/// <summary>
	/// The underlying <see cref="JsonNode"/> Node. It represents the current JSON node being accessed.
	/// </summary>
	public JsonNode? Node;

	/// <summary>
	/// The <see cref="JsonValueKind"/> of the current JSON node, indicating the type of the JSON value (e.g., Object, Array, String, Number, etc.).
	/// </summary>
	public JsonValueKind ValueKind => Node?.GetValueKind() ?? JsonValueKind.Undefined;

	private DynamicJson(JsonNode? node, JsonNode? parent, JsonNodeOptions options)
	{
		Node         = node;
		Parent       = parent;
		_nodeOptions = options;
	}

	/// <summary>
	/// Whether the current JSON node is an array.
	/// </summary>
	public bool IsArray => ValueKind == JsonValueKind.Array;

	/// <summary>
	/// Whether the current JSON node is an object.
	/// </summary>
	public bool IsObject => ValueKind == JsonValueKind.Object;

	/// <summary>
	/// Whether the current JSON node is a primitive value (string, number, boolean) and not an object, array, null or undefined.
	/// </summary>
	public bool IsValue => ValueKind is not JsonValueKind.Object and not JsonValueKind.Array and not JsonValueKind.Undefined and not JsonValueKind.Null;

	/// <summary>
	/// Whether the current JSON node is null or undefined.
	/// </summary>
	public bool IsNull => ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

	/// <summary>
	/// Gets and sets the JSON Node from of a property of the current JSON object. If the property does not exist, it returns an empty <see cref="DynamicJson"/>.
	/// </summary>
	/// <param name="propertyName">The name of the property to retrieve.</param>
	/// <returns>A <see cref="DynamicJson"/> representing the property value.</returns>
	public DynamicJson this[string propertyName]
	{
		get
		{
			// Ensure the current node is an object.
			if (Node is not JsonObject nodeAsObject)
			{
				throw new InvalidCastException("Current node is not a JSON Object");
			}

			// Try to check if property value exists (even if it's null).
			if (nodeAsObject.TryGetPropertyValue(propertyName, out JsonNode? propertyValue) &&
				propertyValue != null)
			{
				return new DynamicJson(propertyValue, nodeAsObject, _nodeOptions);
			}

			return new DynamicJson(null, nodeAsObject, _nodeOptions);
		}
		set
		{
			if (Node is not JsonObject nodeAsObject)
			{
				throw new InvalidCastException("Current node is not a JSON Object");
			}

			nodeAsObject[propertyName] = value.Node;
		}
	}

	/// <summary>
	/// Gets the JSON Node from of an element of the current JSON array by its index (if the current JSON node is an array).
	/// </summary>
	/// <param name="index">The index of the element to retrieve.</param>
	/// <returns>A <see cref="DynamicJson"/> representing the array element.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the current JSON node is not an array.</exception>
	/// <exception cref="IndexOutOfRangeException">Thrown if the index is out of range.</exception>
	public DynamicJson this[int index]
	{
		get
		{
			if (Node is not JsonArray jsonArray)
			{
				throw new InvalidOperationException("ValueKind is not an array!");
			}

			if (index < 0 || index >= jsonArray.Count)
			{
				throw new IndexOutOfRangeException("Index is out of range!");
			}
			
			return new DynamicJson(jsonArray[index], Node, _nodeOptions);
		}
	}

	/// <summary>
	/// Tries to set the value of a property in the current JSON object. If the current node is not an object, it throws an <see cref="InvalidOperationException"/>.
	/// </summary>
	/// <typeparam name="T">The type generic of the value</typeparam>
	/// <param name="propertyName">A property name to be used for setting the property value.</param>
	/// <param name="value">The value to set for the property.</param>
	/// <returns><see langword="true"/> if the value was successfully set.</returns>
	/// <exception cref="InvalidOperationException"></exception>
	public bool TrySetValue<T>(string propertyName, T value)
	{
		if (Node is not JsonObject nodeAsObject)
		{
			throw new InvalidOperationException("Current node is not a JSON Object");
		}

		nodeAsObject[propertyName] = JsonValue.Create(value);
		return true;
	}

	/// <summary>
	/// Create a new <see cref="JsonObject"/> node inside current <see cref="Parent"/> node.
	/// </summary>
	/// <param name="propertyName">A property name to be used for the new property <see cref="JsonNode"/>.</param>
	/// <param name="result">The <see cref="DynamicJson"/> representing the newly created object.</param>
	/// <returns><see langword="true"/> if the property successfully created. <see langword="false"/> if the property already exists.</returns>
	/// <exception cref="InvalidOperationException"></exception>
	public bool TryCreateObject(string propertyName, out DynamicJson result)
	{
		Unsafe.SkipInit(out result);
		if (Node is not JsonObject parentAsObject)
		{
			throw new InvalidOperationException("Parent is null or not an object");
		}

		JsonObject node = new(_nodeOptions);
		bool isExist = parentAsObject.TryAdd(propertyName, node);
		result = new DynamicJson(parentAsObject[propertyName], parentAsObject, _nodeOptions);

		return isExist;
	}

	/// <summary>
	/// Create a new <see cref="JsonArray"/> node inside current <see cref="Parent"/> node.
	/// </summary>
	/// <param name="propertyName">A property name to be used for the new property <see cref="JsonNode"/>.</param>
	/// <param name="result">The <see cref="DynamicJson"/> representing the newly created array.</param>
	/// <returns><see langword="true"/> if the property successfully created. <see langword="false"/> if the property already exists.</returns>
	/// <exception cref="InvalidOperationException"></exception>
	public bool TryCreateArray(string propertyName, out DynamicJson result)
	{
		Unsafe.SkipInit(out result);
		if (Node is not JsonObject parentAsObject)
		{
			throw new InvalidOperationException("Parent is null or not an object");
		}

		JsonArray node = new(_nodeOptions);
		bool isExist = parentAsObject.TryAdd(propertyName, node);
		result = new DynamicJson(parentAsObject[propertyName], parentAsObject, _nodeOptions);

		return isExist;
	}
}
