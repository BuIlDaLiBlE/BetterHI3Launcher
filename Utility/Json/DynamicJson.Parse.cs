#nullable enable
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace BetterHI3Launcher.Utility.Json;

public partial struct DynamicJson
{
	/// <summary>
	/// Parses a JSON string and returns a <see cref="DynamicJson"/> instance representing the root element of the JSON document.
	/// </summary>
	/// <param name="jsonString">The JSON string to parse.</param>
	/// <param name="nodeOptions">The options to use when parsing the JSON nodes.</param>
	/// <param name="documentOptions">The options to use when parsing the JSON document.</param>
	/// <returns>A <see cref="DynamicJson"/> instance representing the root element of the JSON document.</returns>
	/// <exception cref="ArgumentNullException">Thrown when the jsonString is null or empty.</exception>
	public static DynamicJson Parse(string jsonString, JsonNodeOptions? nodeOptions = null, JsonDocumentOptions documentOptions = default)
	{
		if (string.IsNullOrEmpty(jsonString))
		{
			throw new ArgumentNullException(nameof(jsonString));
		}

		nodeOptions ??= new JsonNodeOptions();
		JsonNode? jsonNode = JsonNode.Parse(jsonString, nodeOptions, documentOptions);
		return new DynamicJson(jsonNode, null, nodeOptions.Value);
	}

	/// <summary>
	/// Parses a JSON string from a <see cref="ReadOnlyMemory{byte}"/> and returns a <see cref="DynamicJson"/> instance representing the root element of the JSON document.
	/// </summary>
	/// <param name="jsonMemory">The JSON string to parse.</param>
	/// <param name="nodeOptions">The options to use when parsing the JSON nodes.</param>
	/// <param name="documentOptions">The options to use when parsing the JSON document.</param>
	/// <returns>A <see cref="DynamicJson"/> instance representing the root element of the JSON document.</returns>
	public static DynamicJson Parse(ReadOnlyMemory<byte> jsonMemory, JsonNodeOptions? nodeOptions = null, JsonDocumentOptions documentOptions = default)
	{
		nodeOptions ??= new JsonNodeOptions();
		JsonNode? jsonNode = JsonNode.Parse(jsonMemory.Span, nodeOptions, documentOptions);
		return new DynamicJson(jsonNode, null, nodeOptions.Value);
	}

	/// <summary>
	/// Parses a JSON string from a <see cref="Stream"/> and returns a <see cref="DynamicJson"/> instance representing the root element of the JSON document.
	/// </summary>
	/// <param name="jsonStream">The JSON string to parse.</param>
	/// <param name="nodeOptions">The options to use when parsing the JSON nodes.</param>
	/// <param name="documentOptions">The options to use when parsing the JSON document.</param>
	/// <returns>A <see cref="DynamicJson"/> instance representing the root element of the JSON document.</returns>
	public static DynamicJson Parse(Stream jsonStream, JsonNodeOptions? nodeOptions = null, JsonDocumentOptions documentOptions = default)
	{
		nodeOptions ??= new JsonNodeOptions();
		JsonNode? jsonNode = JsonNode.Parse(jsonStream, nodeOptions, documentOptions);
		return new DynamicJson(jsonNode, null, nodeOptions.Value);
	}

	/// <summary>
	/// Asynchronously parses a JSON string from a <see cref="Stream"/> and returns a <see cref="DynamicJson"/> instance representing the root element of the JSON document.
	/// </summary>
	/// <param name="jsonStream">The JSON string to parse.</param>
	/// <param name="nodeOptions">The options to use when parsing the JSON nodes.</param>
	/// <param name="documentOptions">The options to use when parsing the JSON document.</param>
	/// <param name="token">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
	/// <returns>A <see cref="DynamicJson"/> instance representing the root element of the JSON document.</returns>
	public static async Task<DynamicJson> ParseAsync(Stream jsonStream, JsonNodeOptions? nodeOptions = null, JsonDocumentOptions documentOptions = default, CancellationToken token = default)
	{
		nodeOptions ??= new JsonNodeOptions();
		JsonNode? jsonNode = await JsonNode.ParseAsync(jsonStream, nodeOptions, documentOptions, token);
		return new DynamicJson(jsonNode, null, nodeOptions.Value);
	}
}
