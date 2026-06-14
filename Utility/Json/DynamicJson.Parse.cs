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
		=> Parse(jsonMemory.Span, nodeOptions, documentOptions);

    /// <summary>
    /// Parses a JSON string from a <see cref="ReadOnlySpan{byte}"/> and returns a <see cref="DynamicJson"/> instance representing the root element of the JSON document.
    /// </summary>
    /// <param name="jsonSpan">The JSON string to parse.</param>
    /// <param name="nodeOptions">The options to use when parsing the JSON nodes.</param>
    /// <param name="documentOptions">The options to use when parsing the JSON document.</param>
    /// <returns>A <see cref="DynamicJson"/> instance representing the root element of the JSON document.</returns>
    public static DynamicJson Parse(ReadOnlySpan<byte> jsonSpan, JsonNodeOptions? nodeOptions = null, JsonDocumentOptions documentOptions = default)
	{
		nodeOptions ??= new JsonNodeOptions();

		int offsetCheck = jsonSpan.Length - 1;
		if (jsonSpan.IsEmpty)
        {
            throw new ArgumentNullException(nameof(jsonSpan));
        }

		// HACK: Try trim \0 at the end of the buffer.
		while (offsetCheck >= 0 && jsonSpan[offsetCheck] == 0)
		{
			offsetCheck--;
		}

        JsonNode? jsonNode = JsonNode.Parse(jsonSpan.Slice(0, offsetCheck + 1), nodeOptions, documentOptions);
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

	/// <summary>
	/// Creates a blank <see cref="DynamicJson"/> instance of the specified type <typeparamref name="T"/>. The type must be either <see cref="JsonObject"/> or <see cref="JsonArray"/>.
	/// </summary>
	/// <typeparam name="T">The type of JSON node to create. Must be either <see cref="JsonObject"/> or <see cref="JsonArray"/>.</typeparam>
	/// <param name="nodeOptions">The options to use when creating the JSON node.</param>
	/// <param name="documentOptions">The options to use when creating the JSON document.</param>
	/// <returns>A <see cref="DynamicJson"/> instance representing the created JSON node.</returns>
	/// <exception cref="InvalidOperationException"/>
	public static DynamicJson CreateBlank<T>(JsonNodeOptions? nodeOptions = null, JsonDocumentOptions documentOptions = default)
		where T : JsonNode
	{
		JsonNode node;
		if (typeof(T) == typeof(JsonObject))
		{
			node = new JsonObject(nodeOptions);
		}
		else if (typeof(T) == typeof(JsonArray))
		{
			node = new JsonArray(nodeOptions);
		}
		else
		{
			throw new InvalidOperationException("Unsupported JsonNode type");
		}

		nodeOptions ??= new JsonNodeOptions();
		return new DynamicJson(node, null, nodeOptions.Value);
	}
}
