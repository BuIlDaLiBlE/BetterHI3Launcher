#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace BetterHI3Launcher.Utility.Json;

public partial struct DynamicJson : IEnumerable<DynamicJson>, IEnumerable<KeyValuePair<string, DynamicJson>>
{
	/// <summary>
	/// Enumerates the elements of the JSON array represented by this <see cref="DynamicJson"/> instance.
	/// </summary>
	/// <returns>An <see cref="IEnumerable{DynamicJson}"/> that can be used to iterate through the elements of the JSON array.</returns>
	public IEnumerable<DynamicJson> EnumerateArray()
	{
		if (Node is not JsonArray asJsonArray)
		{
			throw new InvalidOperationException("The current JSON node is not an array.");
		}

		foreach (JsonNode? element in asJsonArray)
		{
			yield return new DynamicJson(element, asJsonArray, _nodeOptions);
		}
	}

	/// <summary>
	/// Enumerates the properties of the JSON object represented by this <see cref="DynamicJson"/> instance.
	/// </summary>
	/// <returns>An <see cref="IEnumerable{KeyValuePair}"/> that can be used to iterate through the properties of the JSON object.</returns>
	public IEnumerable<KeyValuePair<string, DynamicJson>> EnumerateObject()
	{
		if (Node is not JsonObject asJsonObject)
		{
			throw new InvalidOperationException("The current JSON node is not an object.");
		}

		foreach (KeyValuePair<string, JsonNode?> property in asJsonObject)
		{
			yield return new KeyValuePair<string, DynamicJson>(property.Key, new DynamicJson(property.Value, asJsonObject, _nodeOptions));
		}
	}

	/// <inheritdoc/>
	IEnumerator<KeyValuePair<string, DynamicJson>> IEnumerable<KeyValuePair<string, DynamicJson>>.GetEnumerator() => EnumerateObject().GetEnumerator();

	/// <inheritdoc/>
	IEnumerator<DynamicJson> IEnumerable<DynamicJson>.GetEnumerator() => EnumerateArray().GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<DynamicJson>)this).GetEnumerator();
}
