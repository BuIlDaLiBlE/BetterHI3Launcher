using System.Text.Json.Serialization;
using BetterHI3Launcher.Config;

namespace BetterHI3Launcher.Utility.Json;

[JsonSerializable(typeof(LocalVersionInfo))]
public partial class JsonParseContext : JsonSerializerContext;
