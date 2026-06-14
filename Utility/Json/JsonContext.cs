using BetterHI3Launcher.Config;
using System.Text.Json.Serialization;

namespace BetterHI3Launcher.Utility.Json;

[JsonSerializable(typeof(LocalVersionInfo))]
public partial class JsonParseContext : JsonSerializerContext;
