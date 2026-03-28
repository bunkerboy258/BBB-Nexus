using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

var client = new ConfigToolClient("http://127.0.0.1:42718/");

if (args.Length == 0)
{
    PrintHelp();
    return 0;
}

try
{
    return args[0] switch
    {
        "health"        => await CmdHealth(client),
        "list-clips"    => await CmdListClips(client, args),
        "find-clip"     => await CmdFindClip(client, args),
        "fields"        => await CmdFields(client, args),
        "set"           => await CmdSet(client, args),
        "set-ref"       => await CmdSetRef(client, args),
        "list-so-types" => await CmdListSoTypes(client),
        "create-so"     => await CmdCreateSo(client, args),
        "--help" or "-h" => Help(),
        _ => Unknown(args[0]),
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
finally
{
    client.Dispose();
}

static async Task<int> CmdHealth(ConfigToolClient client)
{
    var health = await client.GetAsync<HealthResponse>("health");
    Console.WriteLine($"status: {health.Status}");
    Console.WriteLine($"project: {health.ProjectPath}");
    Console.WriteLine($"unity: {health.UnityVersion}");
    return 0;
}

static async Task<int> CmdListClips(ConfigToolClient client, string[] args)
{
    var filter = "";
    for (var i = 1; i < args.Length - 1; i++)
    {
        if (args[i] == "--filter")
        {
            filter = args[i + 1];
        }
    }

    var response = await client.PostAsync<FindClipRequest, ClipListResponse>("clips/find", new FindClipRequest
    {
        Query = filter
    });

    Console.WriteLine($"{"Name",-50} {"GUID",-32} {"LocalFileId",-14} AssetPath");
    Console.WriteLine(new string('-', 140));
    foreach (var clip in response.Clips)
    {
        Console.WriteLine($"{clip.Name,-50} {clip.Guid,-32} {clip.LocalFileId,-14} {clip.AssetPath}");
    }

    return 0;
}

static async Task<int> CmdFindClip(ConfigToolClient client, string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: find-clip <name>");
        return 1;
    }

    return await CmdListClips(client, new[] { "list-clips", "--filter", args[1] });
}

static async Task<int> CmdFields(ConfigToolClient client, string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: fields <asset-path>");
        return 1;
    }

    var response = await client.PostAsync<FieldsRequest, FieldListResponse>("assets/fields", new FieldsRequest
    {
        Path = args[1]
    });

    Console.WriteLine($"Asset: {response.AssetPath}");
    Console.WriteLine($"Type:  {response.AssetType}");
    foreach (var field in response.Fields)
    {
        Console.WriteLine();
        Console.WriteLine($"Field:   {field.Path}");
        Console.WriteLine($"Type:    {field.DeclaredType} ({field.Type})");
        Console.WriteLine($"Value:   {field.Value}");
        if (!string.IsNullOrWhiteSpace(field.Header))
        {
            Console.WriteLine($"Header:  {field.Header}");
        }

        if (!string.IsNullOrWhiteSpace(field.Tooltip))
        {
            Console.WriteLine($"Tooltip: {field.Tooltip}");
        }

        if (!string.IsNullOrWhiteSpace(field.Doc))
        {
            Console.WriteLine($"Doc:     {field.Doc}");
        }
    }

    return 0;
}

static async Task<int> CmdSet(ConfigToolClient client, string[] args)
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: set <asset-path> <field> <value>");
        return 1;
    }

    var response = await client.PostAsync<SetFieldRequest, SetFieldResponse>("assets/set", new SetFieldRequest
    {
        Path = args[1],
        Field = args[2],
        Value = args[3]
    });

    Console.WriteLine($"ok: {response.Field} = {response.Value} ({response.Type})");
    return 0;
}

static async Task<int> CmdSetRef(ConfigToolClient client, string[] args)
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: set-ref <asset-path> <field> <asset-name|null>");
        return 1;
    }

    var response = await client.PostAsync<SetReferenceRequest, SetFieldResponse>("assets/set-ref", new SetReferenceRequest
    {
        Path = args[1],
        Field = args[2],
        AssetName = args[3]
    });

    Console.WriteLine($"ok: {response.Field} -> {response.Value}");
    return 0;
}

static async Task<int> CmdListSoTypes(ConfigToolClient client)
{
    var response = await client.PostAsync<object, ScriptableObjectTypeListResponse>("so-types/list", new { });

    Console.WriteLine($"{"Type",-60} {"CreateAssetMenu",-16} Menu");
    Console.WriteLine(new string('-', 140));
    foreach (var type in response.Types)
    {
        var menu = string.IsNullOrWhiteSpace(type.MenuName) ? "-" : type.MenuName;
        Console.WriteLine($"{type.FullName,-60} {(type.HasCreateAssetMenu ? "yes" : "no"),-16} {menu}");
    }

    return 0;
}

static async Task<int> CmdCreateSo(ConfigToolClient client, string[] args)
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: create-so <type-or-menu> <asset-path>");
        return 1;
    }

    var response = await client.PostAsync<CreateScriptableObjectRequest, CreateScriptableObjectResponse>("so/create",
        new CreateScriptableObjectRequest
        {
            Type = args[1],
            Path = args[2]
        });

    Console.WriteLine($"created: {response.AssetPath}");
    Console.WriteLine($"type:    {response.AssetType}");
    if (!string.IsNullOrWhiteSpace(response.MenuName))
    {
        Console.WriteLine($"menu:    {response.MenuName}");
    }

    return 0;
}

static int Help()
{
    PrintHelp();
    return 0;
}

static int Unknown(string cmd)
{
    Console.Error.WriteLine($"Unknown command: {cmd}");
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine("""
        config-tool

        Usage:
          config-tool health
          config-tool list-clips [--filter <text>]
          config-tool find-clip <name>
          config-tool fields <asset-path>
          config-tool set <asset-path> <field> <value>
          config-tool set-ref <asset-path> <field> <asset-name|null>
          config-tool list-so-types
          config-tool create-so <type-or-menu> <asset-path>
        """);
}

internal sealed class ConfigToolClient : IDisposable
{
    private readonly HttpClient httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ConfigToolClient(string baseAddress)
    {
        httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<T> GetAsync<T>(string path)
    {
        using var response = await httpClient.GetAsync(path);
        return await ReadEnvelope<T>(response);
    }

    public async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request)
    {
        using var response = await httpClient.PostAsJsonAsync(path, request, JsonOptions);
        return await ReadEnvelope<TResponse>(response);
    }

    private static async Task<T> ReadEnvelope<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        ConfigToolEnvelope? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<ConfigToolEnvelope>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid response from ConfigTool server: {ex.Message}");
        }

        if (envelope == null)
        {
            throw new InvalidOperationException("Empty response from ConfigTool server.");
        }

        if (!envelope.Ok)
        {
            throw new InvalidOperationException(envelope.Error);
        }

        if (string.IsNullOrWhiteSpace(envelope.Json))
        {
            throw new InvalidOperationException("ConfigTool server returned an empty payload.");
        }

        var payload = JsonSerializer.Deserialize<T>(envelope.Json, JsonOptions);
        if (payload == null)
        {
            throw new InvalidOperationException("Failed to parse ConfigTool payload.");
        }

        return payload;
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }
}

internal sealed class ConfigToolEnvelope
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = "";

    [JsonPropertyName("json")]
    public string Json { get; set; } = "";
}

internal sealed class HealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; set; } = "";

    [JsonPropertyName("unityVersion")]
    public string UnityVersion { get; set; } = "";
}

internal sealed class FindClipRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = "";
}

internal sealed class FieldsRequest
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";
}

internal sealed class SetFieldRequest
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

internal sealed class SetReferenceRequest
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [JsonPropertyName("assetName")]
    public string AssetName { get; set; } = "";
}

internal sealed class CreateScriptableObjectRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";
}

internal sealed class ClipListResponse
{
    [JsonPropertyName("clips")]
    public ClipInfo[] Clips { get; set; } = Array.Empty<ClipInfo>();
}

internal sealed class ClipInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("guid")]
    public string Guid { get; set; } = "";

    [JsonPropertyName("localFileId")]
    public long LocalFileId { get; set; }

    [JsonPropertyName("assetPath")]
    public string AssetPath { get; set; } = "";
}

internal sealed class FieldListResponse
{
    [JsonPropertyName("assetPath")]
    public string AssetPath { get; set; } = "";

    [JsonPropertyName("assetType")]
    public string AssetType { get; set; } = "";

    [JsonPropertyName("fields")]
    public FieldInfo[] Fields { get; set; } = Array.Empty<FieldInfo>();
}

internal sealed class FieldInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("declaredType")]
    public string DeclaredType { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("header")]
    public string Header { get; set; } = "";

    [JsonPropertyName("tooltip")]
    public string Tooltip { get; set; } = "";

    [JsonPropertyName("doc")]
    public string Doc { get; set; } = "";
}

internal sealed class SetFieldResponse
{
    [JsonPropertyName("assetPath")]
    public string AssetPath { get; set; } = "";

    [JsonPropertyName("assetType")]
    public string AssetType { get; set; } = "";

    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

internal sealed class ScriptableObjectTypeListResponse
{
    [JsonPropertyName("types")]
    public ScriptableObjectTypeInfo[] Types { get; set; } = Array.Empty<ScriptableObjectTypeInfo>();
}

internal sealed class ScriptableObjectTypeInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = "";

    [JsonPropertyName("assemblyName")]
    public string AssemblyName { get; set; } = "";

    [JsonPropertyName("hasCreateAssetMenu")]
    public bool HasCreateAssetMenu { get; set; }

    [JsonPropertyName("menuName")]
    public string MenuName { get; set; } = "";

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";
}

internal sealed class CreateScriptableObjectResponse
{
    [JsonPropertyName("assetPath")]
    public string AssetPath { get; set; } = "";

    [JsonPropertyName("assetType")]
    public string AssetType { get; set; } = "";

    [JsonPropertyName("menuName")]
    public string MenuName { get; set; } = "";
}
