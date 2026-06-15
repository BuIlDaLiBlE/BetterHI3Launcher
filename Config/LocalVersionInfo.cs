using System.Text.Json.Serialization;
using BetterHI3Launcher.Utility;
using BetterHI3Launcher.Utility.Json.Converter;

#nullable enable
namespace BetterHI3Launcher.Config;

public class LocalVersionInfo
{
	[JsonPropertyName("launch_options")]
	public string? LaunchOptions { get; set; }

	[JsonPropertyName("game_info")]
	public GameInfo? GameInfo { get; set; }
}

public class GameInfo
{
	[JsonPropertyName("installed")]
	public bool IsInstalled { get; set; }

	[JsonPropertyName("version")]
	[JsonConverter(typeof(StructVersionJsonConverter))]
	public StructVersion Version { get; set; }

	[JsonPropertyName("install_path")]
	public string? InstallPath { get; set; }
}
