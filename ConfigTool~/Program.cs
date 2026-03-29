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
    if (IsHelpCommand(args))
    {
        return PrintCommandHelp(GetHelpTopic(args));
    }

    return args[0] switch
    {
        "health"        => await CmdHealth(client),
        "list-clips"    => await CmdListClips(client, args),
        "find-clip"     => await CmdFindClip(client, args),
        "fields"        => await CmdFields(client, args),
        "inspect"       => await CmdInspect(client, args),
        "set"           => await CmdSet(client, args),
        "set-inspector" => await CmdSetInspector(client, args),
        "set-ref"       => await CmdSetRef(client, args),
        "list-get"      => await CmdListGet(client, args),
        "list-add"      => await CmdListAdd(client, args),
        "list-set"      => await CmdListSet(client, args),
        "list-remove"   => await CmdListRemove(client, args),
        "list-clear"    => await CmdListClear(client, args),
        "list-so-types" => await CmdListSoTypes(client),
        "create-so"     => await CmdCreateSo(client, args),
        "rename-asset"  => await CmdRenameAsset(client, args),
        "rebuild-so-meta" => await CmdRebuildSoMeta(client),
        "help" => PrintCommandHelp(args.Length > 1 ? args[1] : ""),
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
    if (HasCommandHelp(args))
    {
        return PrintCommandHelp("list-clips");
    }

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
    if (HasCommandHelp(args))
    {
        return PrintCommandHelp("find-clip");
    }

    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: config-tool find-clip <name>");
        return 1;
    }

    return await CmdListClips(client, new[] { "list-clips", "--filter", args[1] });
}

static async Task<int> CmdFields(ConfigToolClient client, string[] args)
{
    if (HasCommandHelp(args))
    {
        return PrintCommandHelp("fields");
    }

    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: config-tool fields <asset-path>");
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
    if (HasCommandHelp(args))
    {
        return PrintCommandHelp("set");
    }

    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: config-tool set <asset-path> <field> <value>");
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

static async Task<int> CmdInspect(ConfigToolClient client, string[] args)
{
    if (HasCommandHelp(args))
    {
        return PrintCommandHelp("inspect");
    }

    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: config-tool inspect <asset-path>");
        return 1;
    }

    var response = await client.PostAsync<InspectAssetRequest, InspectAssetResponse>("assets/inspect", new InspectAssetRequest
    {
        Path = args[1]
    });

    Console.WriteLine($"Asset: {response.AssetPath}");
    Console.WriteLine($"Type:  {response.AssetType}");
    foreach (var field in response.Fields)
    {
        Console.WriteLine();
        Console.WriteLine($"Field:   {field.Path}");
        Console.WriteLine($"Raw:     {field.RawPath}");
        Console.WriteLine($"Type:    {field.Type}");
        Console.WriteLine($"Value:   {field.Value}");
        if (!string.IsNullOrWhiteSpace(field.Tooltip))
        {
            Console.WriteLine($"Tooltip: {field.Tooltip}");
        }
    }

    return 0;
}

static async Task<int> CmdSetInspector(ConfigToolClient client, string[] args)
{
    if (HasCommandHelp(args))
    {
        return PrintCommandHelp("set-inspector");
    }

    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: config-tool set-inspector <asset-path> <field> <value>");
        return 1;
    }

    var response = await client.PostAsync<SetInspectorValueRequest, SetFieldResponse>("assets/set-inspector",
        new SetInspectorValueRequest
        {
            Path = args[1],
            Field = args[2],
            Value = args[3]
        });

    Console.WriteLine($"ok: {response.Field} = {response.Value}");
    return 0;
}

static async Task<int> CmdSetRef(ConfigToolClient client, string[] args)
{
    if (HasCommandHelp(args))
    {
        return PrintCommandHelp("set-ref");
    }

    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: config-tool set-ref <asset-path> <field> <asset-name|null>");
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

static async Task<int> CmdListGet(ConfigToolClient client, string[] args)
{
    if (HasCommandHelp(args))
    {
        return PrintCommandHelp("list-get");
    }

    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: config-tool list-get <asset-path> <field>");
        return 1;
    }

    var response = await client.PostAsync<ListFieldRequest, ListFieldResponse>("assets/list-get", new ListFieldRequest
    {
        Path = args[1],
        Field = args[2]
    });

    PrintListResponse(response);
    return 0;
}

static async Task<int> CmdListAdd(ConfigToolClient client, string[] args)
{
    if (HasCommandHelp(args))
    {
        return PrintCommandHelp("list-add");
    }

    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: config-tool list-add <asset-path> <field> <value>");
        return 1;
    }

    var response = await client.PostAsync<ListAddRequest, ListFieldResponse>("assets/list-add", new ListAddRequest
    {
        Path = args[1],
        Field = args[2],
        Value = args[3]
    });

    PrintListResponse(response);
    return 0;
}

static async Task<int> CmdListSet(ConfigToolClient client, string[] args)
{
    if (HasCommandHelp(args))
    {
        return PrintCommandHelp("list-set");
    }

    if (args.Length < 5 || !int.TryParse(args[3], out var index))
    {
        Console.Error.WriteLine("Usage: config-tool list-set <asset-path> <field> <index> <value>");
        return 1;
    }

    var response = await client.PostAsync<ListSetRequest, ListFieldResponse>("assets/list-set", new ListSetRequest
    {
        Path = args[1],
        Field = args[2],
        Index = index,
        Value = args[4]
    });

    PrintListResponse(response);
    return 0;
}

static async Task<int> CmdListRemove(ConfigToolClient client, string[] args)
{
    if (HasCommandHelp(args))
    {
        return PrintCommandHelp("list-remove");
    }

    if (args.Length < 4 || !int.TryParse(args[3], out var index))
    {
        Console.Error.WriteLine("Usage: config-tool list-remove <asset-path> <field> <index>");
        return 1;
    }

    var response = await client.PostAsync<ListRemoveRequest, ListFieldResponse>("assets/list-remove", new ListRemoveRequest
    {
        Path = args[1],
        Field = args[2],
        Index = index
    });

    PrintListResponse(response);
    return 0;
}

static async Task<int> CmdListClear(ConfigToolClient client, string[] args)
{
    if (HasCommandHelp(args))
    {
        return PrintCommandHelp("list-clear");
    }

    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: config-tool list-clear <asset-path> <field>");
        return 1;
    }

    var response = await client.PostAsync<ListFieldRequest, ListFieldResponse>("assets/list-clear", new ListFieldRequest
    {
        Path = args[1],
        Field = args[2]
    });

    PrintListResponse(response);
    return 0;
}

static async Task<int> CmdCreateSo(ConfigToolClient client, string[] args)
{
    if (HasCommandHelp(args))
    {
        return PrintCommandHelp("create-so");
    }

    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: config-tool create-so <type-or-menu> <asset-path>");
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

static async Task<int> CmdRenameAsset(ConfigToolClient client, string[] args)
{
    if (HasCommandHelp(args))
    {
        return PrintCommandHelp("rename-asset");
    }

    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: config-tool rename-asset <asset-path> <new-file-name>");
        return 1;
    }

    var response = await client.PostAsync<RenameAssetRequest, RenameAssetResponse>("assets/rename",
        new RenameAssetRequest
        {
            Path = args[1],
            Name = args[2]
        });

    Console.WriteLine($"from: {response.OldAssetPath}");
    Console.WriteLine($"to:   {response.NewAssetPath}");
    return 0;
}

static async Task<int> CmdRebuildSoMeta(ConfigToolClient client)
{
    var response = await client.PostAsync<object, MetaLibSoRebuildResponse>("metas/rebuild-so", new { });

    if (response.Updated)
    {
        Console.WriteLine("updated: yes");
        Console.WriteLine($"preserved-non-so: {response.PreservedCount}");
        Console.WriteLine($"registered-so:    {response.RegisteredSoCount}");
        return 0;
    }

    Console.WriteLine("updated: no");
    Console.WriteLine("reason: duplicate ids");
    foreach (var duplicate in response.Duplicates)
    {
        Console.WriteLine();
        Console.WriteLine($"id: {duplicate.Id}");
        foreach (var assetPath in duplicate.AssetPaths)
        {
            Console.WriteLine($"asset:    {assetPath}");
        }

        foreach (var resourcePath in duplicate.ResourcePaths)
        {
            Console.WriteLine($"resource: {resourcePath}");
        }
    }

    return 1;
}

static int Help()
{
    PrintHelp();
    return 0;
}

static bool IsHelpCommand(string[] args)
{
    if (args.Length == 0)
    {
        return false;
    }

    if (args[0] is "help" or "--help" or "-h")
    {
        return true;
    }

    return args.Length > 1 && HasCommandHelp(args);
}

static string GetHelpTopic(string[] args)
{
    if (args.Length == 0)
    {
        return "";
    }

    if (args[0] is "--help" or "-h")
    {
        return "";
    }

    if (args[0] == "help")
    {
        return args.Length > 1 ? args[1] : "";
    }

    return HasCommandHelp(args) ? args[0] : "";
}

static bool HasCommandHelp(string[] args)
{
    return args.Any(arg => arg is "--help" or "-h");
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
          config-tool <command> [arguments]

        Commands:
          health                     查看 Unity Editor 服务状态
          list-clips                 列出 AnimationClip，可用 --filter 过滤
          find-clip                  按名字片段搜索 AnimationClip
          fields                     查看原始序列化字段
          inspect                    查看贴近 Inspector 的语义字段
          set                        按原始字段路径设置标量
          set-inspector              按 Inspector 语义字段设置值
          set-ref                    按原始字段路径设置引用
          list-so-types              列出可创建的 BBBNexus ScriptableObject 类型
          create-so                  在指定路径创建 ScriptableObject
          rename-asset               重命名现有资产文件
          rebuild-so-meta            重建 MetaLib 中的 BBBNexus SO 条目

        Help:
          config-tool help
          config-tool help <command>
          config-tool <command> --help
        """);
}

static int PrintCommandHelp(string command)
{
    switch ((command ?? "").Trim())
    {
        case "":
            PrintHelp();
            return 0;
        case "health":
            Console.WriteLine("""
                config-tool health

                说明:
                  查看当前 Unity Editor ConfigTool 服务是否在线。

                示例:
                  config-tool health
                """);
            return 0;
        case "list-clips":
            Console.WriteLine("""
                config-tool list-clips [--filter <text>]

                说明:
                  列出项目中的 AnimationClip。可用 --filter 按名称片段过滤。

                示例:
                  config-tool list-clips
                  config-tool list-clips --filter Fists
                """);
            return 0;
        case "find-clip":
            Console.WriteLine("""
                config-tool find-clip <name>

                说明:
                  按名字片段搜索 AnimationClip。等价于 list-clips --filter。

                示例:
                  config-tool find-clip Fists
                """);
            return 0;
        case "fields":
            Console.WriteLine("""
                config-tool fields <asset-path>

                说明:
                  查看 ScriptableObject 的原始可编辑字段路径、类型、值、Header、Tooltip。

                示例:
                  config-tool fields "Assets/BBBNexus/Assests/Configs/Characters/AI/Input/StatusEffectSO_Smoke.asset"
                """);
            return 0;
        case "inspect":
            Console.WriteLine("""
                config-tool inspect <asset-path>

                说明:
                  查看贴近 Unity Inspector 的语义字段，例如 Clip.Animation、Clip.StartTime、Clip.EndTime。

                示例:
                  config-tool inspect "Assets/BBBNexus/Assests/Configs/Characters/AI/Input/StatusEffectSO_Smoke.asset"
                """);
            return 0;
        case "set":
            Console.WriteLine("""
                config-tool set <asset-path> <field> <value>

                说明:
                  按原始序列化字段路径设置标量值。

                示例:
                  config-tool set "Assets/.../StatusEffectSO_Smoke.asset" Duration 2.5
                  config-tool set "Assets/.../StatusEffectSO_Smoke.asset" "Clip._Speed" 1.25
                """);
            return 0;
        case "set-inspector":
            Console.WriteLine("""
                config-tool set-inspector <asset-path> <field> <value>

                说明:
                  按贴近 Inspector 的语义字段设置值。

                示例:
                  config-tool set-inspector "Assets/.../StatusEffectSO_Smoke.asset" "Clip.Animation" "Big Hit To Head"
                  config-tool set-inspector "Assets/.../StatusEffectSO_Smoke.asset" "Clip.EndTime" 0.8
                """);
            return 0;
        case "set-ref":
            Console.WriteLine("""
                config-tool set-ref <asset-path> <field> <asset-name|null>

                说明:
                  按原始字段路径设置 Unity 引用字段。传入 null 可清空引用。

                示例:
                  config-tool set-ref "Assets/.../StatusEffectSO_Smoke.asset" "Clip._Clip" "Big Hit To Head"
                  config-tool set-ref "Assets/.../StatusEffectSO_Smoke.asset" "Clip._Clip" null
                """);
            return 0;
        case "list-get":
            Console.WriteLine("""
                config-tool list-get <asset-path> <field>

                说明:
                  查看列表/数组字段的所有元素。

                示例:
                  config-tool list-get "Assets/.../ZombieBrain.asset" AvailableStates
                  config-tool list-get "Assets/.../ZombieBrain.asset" GlobalInterceptors
                """);
            return 0;
        case "list-add":
            Console.WriteLine("""
                config-tool list-add <asset-path> <field> <value>

                说明:
                  向列表/数组字段末尾追加一个元素。

                示例:
                  config-tool list-add "Assets/.../ZombieBrain.asset" AvailableStates Idle
                  config-tool list-add "Assets/.../ZombieBrain.asset" GlobalInterceptors "AimInterceptor @ Assets/.../AimInterceptor.asset"
                """);
            return 0;
        case "list-set":
            Console.WriteLine("""
                config-tool list-set <asset-path> <field> <index> <value>

                说明:
                  修改列表/数组字段指定下标的元素。

                示例:
                  config-tool list-set "Assets/.../ZombieBrain.asset" AvailableStates 0 Idle
                """);
            return 0;
        case "list-remove":
            Console.WriteLine("""
                config-tool list-remove <asset-path> <field> <index>

                说明:
                  删除列表/数组字段指定下标的元素。

                示例:
                  config-tool list-remove "Assets/.../ZombieBrain.asset" GlobalInterceptors 2
                """);
            return 0;
        case "list-clear":
            Console.WriteLine("""
                config-tool list-clear <asset-path> <field>

                说明:
                  清空列表/数组字段。

                示例:
                  config-tool list-clear "Assets/.../ZombieBrain.asset" UpperBodyInterceptors
                """);
            return 0;
        case "list-so-types":
            Console.WriteLine("""
                config-tool list-so-types

                说明:
                  列出当前项目中可创建的 BBBNexus ScriptableObject 类型。

                示例:
                  config-tool list-so-types
                """);
            return 0;
        case "create-so":
            Console.WriteLine("""
                config-tool create-so <type-or-menu> <asset-path>

                说明:
                  在指定路径创建新的 ScriptableObject 资产。

                示例:
                  config-tool create-so StatusEffectSO "Assets/BBBNexus/Assests/Configs/Characters/AI/Input/StatusEffectSO_New.asset"
                  config-tool create-so "BBBNexus/Combat/StatusEffect" "Assets/BBBNexus/Assests/Configs/Characters/AI/Input/StatusEffectSO_New.asset"
                """);
            return 0;
        case "rebuild-so-meta":
            Console.WriteLine("""
                config-tool rebuild-so-meta

                说明:
                  扫描 Resources 下的 BBBNexus ScriptableObject，重建 MetaLib 的 SO 条目。
                  如果存在重复 ID，会返回所有冲突资产路径，不会写入 MetaLib。

                示例:
                  config-tool rebuild-so-meta
                """);
            return 0;
        case "rename-asset":
            Console.WriteLine("""
                config-tool rename-asset <asset-path> <new-file-name>

                说明:
                  通过 Unity AssetDatabase 重命名现有资产文件。

                示例:
                  config-tool rename-asset "Assets/.../CoreModule.asset" "Core.player.asset"
                """);
            return 0;
        default:
            Console.Error.WriteLine($"Unknown help topic: {command}");
            return 1;
    }
}

static void PrintListResponse(ListFieldResponse response)
{
    Console.WriteLine($"Asset: {response.AssetPath}");
    Console.WriteLine($"Type:  {response.AssetType}");
    Console.WriteLine($"Field: {response.Field}");
    Console.WriteLine($"Size:  {response.Size}");
    if (!string.IsNullOrWhiteSpace(response.ElementType))
    {
        Console.WriteLine($"Elem:  {response.ElementType}");
    }

    foreach (var item in response.Items)
    {
        Console.WriteLine($"[{item.Index}] {item.Value} ({item.Type})");
    }
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

internal sealed class InspectAssetRequest
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";
}

internal sealed class SetInspectorValueRequest
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

internal sealed class CreateScriptableObjectRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";
}

internal sealed class RenameAssetRequest
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

internal sealed class ListFieldRequest
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("field")]
    public string Field { get; set; } = "";
}

internal sealed class ListSetRequest
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

internal sealed class ListAddRequest
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

internal sealed class ListRemoveRequest
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [JsonPropertyName("index")]
    public int Index { get; set; }
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

internal sealed class ListElementInfo
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

internal sealed class ListFieldResponse
{
    [JsonPropertyName("assetPath")]
    public string AssetPath { get; set; } = "";

    [JsonPropertyName("assetType")]
    public string AssetType { get; set; } = "";

    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [JsonPropertyName("elementType")]
    public string ElementType { get; set; } = "";

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("items")]
    public ListElementInfo[] Items { get; set; } = Array.Empty<ListElementInfo>();
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

internal sealed class RenameAssetResponse
{
    [JsonPropertyName("oldAssetPath")]
    public string OldAssetPath { get; set; } = "";

    [JsonPropertyName("newAssetPath")]
    public string NewAssetPath { get; set; } = "";
}

internal sealed class MetaLibDuplicateResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("assetPaths")]
    public string[] AssetPaths { get; set; } = Array.Empty<string>();

    [JsonPropertyName("resourcePaths")]
    public string[] ResourcePaths { get; set; } = Array.Empty<string>();
}

internal sealed class MetaLibSoRebuildResponse
{
    [JsonPropertyName("updated")]
    public bool Updated { get; set; }

    [JsonPropertyName("preservedCount")]
    public int PreservedCount { get; set; }

    [JsonPropertyName("registeredSoCount")]
    public int RegisteredSoCount { get; set; }

    [JsonPropertyName("duplicates")]
    public MetaLibDuplicateResponse[] Duplicates { get; set; } = Array.Empty<MetaLibDuplicateResponse>();
}

internal sealed class InspectAssetResponse
{
    [JsonPropertyName("assetPath")]
    public string AssetPath { get; set; } = "";

    [JsonPropertyName("assetType")]
    public string AssetType { get; set; } = "";

    [JsonPropertyName("fields")]
    public InspectorFieldInfo[] Fields { get; set; } = Array.Empty<InspectorFieldInfo>();
}

internal sealed class InspectorFieldInfo
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("rawPath")]
    public string RawPath { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("tooltip")]
    public string Tooltip { get; set; } = "";
}
