#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BBBNexus
{
    internal static class ConfigToolAssetService
    {
        public static FieldListResponse GetFields(string rawPath)
        {
            var assetPath = NormalizeAssetPath(rawPath);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"ScriptableObject not found: {assetPath}");
            }

            var serializedObject = new SerializedObject(asset);
            var metadata = BuildFieldMetadata(asset);
            var fields = new List<FieldInfoDto>();
            foreach (var fieldPath in EnumerateSerializablePropertyPaths(asset.GetType(), null))
            {
                var property = serializedObject.FindProperty(fieldPath);
                if (property == null || !ShouldIncludeField(property))
                {
                    continue;
                }

                fields.Add(ToFieldInfo(property.Copy(), metadata));
            }

            return new FieldListResponse
            {
                assetPath = assetPath,
                assetType = asset.GetType().FullName,
                fields = fields.ToArray()
            };
        }

        public static SetFieldResponse SetField(string rawPath, string fieldPath, string rawValue)
        {
            var assetPath = NormalizeAssetPath(rawPath);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"ScriptableObject not found: {assetPath}");
            }

            var serializedObject = new SerializedObject(asset);
            var property = serializedObject.FindProperty(fieldPath);
            if (property == null)
            {
                throw new InvalidOperationException($"Field not found: {fieldPath}");
            }

            if (!TryAssignScalar(property, rawValue, out var assignedValue))
            {
                throw new InvalidOperationException($"Unsupported or invalid scalar assignment for {fieldPath} ({property.propertyType})");
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            return new SetFieldResponse
            {
                assetPath = assetPath,
                assetType = asset.GetType().FullName,
                field = fieldPath,
                type = property.propertyType.ToString(),
                value = assignedValue
            };
        }

        public static SetFieldResponse SetReference(string rawPath, string fieldPath, string assetName)
        {
            var assetPath = NormalizeAssetPath(rawPath);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"ScriptableObject not found: {assetPath}");
            }

            var serializedObject = new SerializedObject(asset);
            var property = serializedObject.FindProperty(fieldPath);
            if (property == null)
            {
                throw new InvalidOperationException($"Field not found: {fieldPath}");
            }

            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                throw new InvalidOperationException($"Field is not an object reference: {fieldPath}");
            }

            var expectedReferenceType = ResolveReferenceFieldType(asset.GetType(), fieldPath);
            var reference = IsNullLiteral(assetName)
                ? null
                : FindSingleAsset(assetName, expectedReferenceType);
            property.objectReferenceValue = reference;

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            return new SetFieldResponse
            {
                assetPath = assetPath,
                assetType = asset.GetType().FullName,
                field = fieldPath,
                type = property.propertyType.ToString(),
                value = reference == null ? "null" : AssetDatabase.GetAssetPath(reference)
            };
        }

        public static ClipListResponse FindClips(string query)
        {
            var normalizedQuery = (query ?? "").Trim();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var clips = new List<AssetRefDto>();

            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(obj is AnimationClip clip))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(normalizedQuery) &&
                        clip.name.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string clipGuid, out long localFileId))
                    {
                        continue;
                    }

                    var key = clipGuid + ":" + localFileId;
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    clips.Add(new AssetRefDto
                    {
                        name = clip.name,
                        type = nameof(AnimationClip),
                        guid = clipGuid,
                        localFileId = localFileId,
                        assetPath = path
                    });
                }
            }

            return new ClipListResponse
            {
                clips = clips
                    .OrderBy(c => c.name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(c => c.assetPath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(c => c.localFileId)
                    .ToArray()
            };
        }

        public static ScriptableObjectTypeListResponse ListScriptableObjectTypes()
        {
            var types = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .Where(type =>
                    type != null &&
                    !type.IsAbstract &&
                    !type.IsGenericType &&
                    !string.IsNullOrEmpty(type.Namespace) &&
                    type.Namespace.StartsWith("BBBNexus", StringComparison.Ordinal))
                .Select(type =>
                {
                    var menu = type.GetCustomAttribute<CreateAssetMenuAttribute>();
                    return new ScriptableObjectTypeDto
                    {
                        name = type.Name,
                        fullName = type.FullName,
                        assemblyName = type.Assembly.GetName().Name,
                        hasCreateAssetMenu = menu != null,
                        menuName = menu?.menuName ?? "",
                        fileName = menu?.fileName ?? ""
                    };
                })
                .Where(type => type.hasCreateAssetMenu)
                .OrderByDescending(type => type.hasCreateAssetMenu)
                .ThenBy(type => type.menuName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(type => type.fullName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new ScriptableObjectTypeListResponse
            {
                types = types
            };
        }

        public static CreateScriptableObjectResponse CreateScriptableObject(string typeOrMenu, string rawPath)
        {
            var descriptor = ResolveCreatableScriptableObjectType(typeOrMenu);
            var assetPath = NormalizeCreateAssetPath(rawPath);
            EnsureParentFolderExists(assetPath);

            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath) != null || File.Exists(assetPath))
            {
                throw new InvalidOperationException($"Asset already exists: {assetPath}");
            }

            var instance = ScriptableObject.CreateInstance(descriptor.Type);
            if (instance == null)
            {
                throw new InvalidOperationException($"Failed to create ScriptableObject instance: {descriptor.Type.FullName}");
            }

            try
            {
                AssetDatabase.CreateAsset(instance, assetPath);
                EditorUtility.SetDirty(instance);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(instance);
                throw;
            }

            return new CreateScriptableObjectResponse
            {
                assetPath = assetPath,
                assetType = descriptor.Type.FullName,
                menuName = descriptor.MenuName
            };
        }

        private static FieldInfoDto ToFieldInfo(SerializedProperty property, Dictionary<string, FieldMetadata> metadata)
        {
            metadata.TryGetValue(property.propertyPath, out var info);
            return new FieldInfoDto
            {
                name = property.name,
                path = property.propertyPath,
                type = property.propertyType.ToString(),
                declaredType = info?.DeclaredType ?? "",
                value = ReadValue(property),
                header = info?.Header ?? "",
                tooltip = info?.Tooltip ?? "",
                doc = info?.Doc ?? "",
                isArray = property.isArray && property.propertyType != SerializedPropertyType.String
            };
        }

        private static bool ShouldIncludeField(SerializedProperty property)
        {
            if (property == null)
            {
                return false;
            }

            if (property.propertyPath == "m_Script")
            {
                return false;
            }

            if (property.propertyPath.EndsWith(".Array.size", StringComparison.Ordinal))
            {
                return false;
            }

            if (property.name == "_NormalizedTimes" ||
                property.name == "_Callbacks" ||
                property.name == "_Names" ||
                property.name == "references" ||
                property.propertyPath.StartsWith("references.", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        private static string ReadValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean:
                    return property.boolValue ? "true" : "false";
                case SerializedPropertyType.Float:
                    return property.floatValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.String:
                    return property.stringValue ?? "";
                case SerializedPropertyType.Enum:
                    return property.enumDisplayNames[property.enumValueIndex];
                case SerializedPropertyType.ObjectReference:
                    return DescribeObjectReference(property.objectReferenceValue);
                case SerializedPropertyType.Color:
                    return property.colorValue.ToString();
                case SerializedPropertyType.Vector2:
                    return property.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return property.vector3Value.ToString();
                case SerializedPropertyType.Vector4:
                    return property.vector4Value.ToString();
                case SerializedPropertyType.Rect:
                    return property.rectValue.ToString();
                case SerializedPropertyType.Bounds:
                    return property.boundsValue.ToString();
                case SerializedPropertyType.Quaternion:
                    return property.quaternionValue.eulerAngles.ToString();
                default:
                    if (property.isArray && property.propertyType != SerializedPropertyType.String)
                    {
                        return $"Array(size={property.arraySize})";
                    }

                    if (property.propertyType == SerializedPropertyType.Generic)
                    {
                        return "Object";
                    }

                    return property.displayName;
            }
        }

        private static bool TryAssignScalar(SerializedProperty property, string rawValue, out string assignedValue)
        {
            assignedValue = "";
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    {
                        return false;
                    }

                    property.intValue = intValue;
                    assignedValue = intValue.ToString(CultureInfo.InvariantCulture);
                    return true;

                case SerializedPropertyType.Boolean:
                    if (!bool.TryParse(rawValue, out var boolValue))
                    {
                        return false;
                    }

                    property.boolValue = boolValue;
                    assignedValue = boolValue ? "true" : "false";
                    return true;

                case SerializedPropertyType.Float:
                    if (!float.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var floatValue))
                    {
                        return false;
                    }

                    property.floatValue = floatValue;
                    assignedValue = floatValue.ToString(CultureInfo.InvariantCulture);
                    return true;

                case SerializedPropertyType.String:
                    property.stringValue = rawValue ?? "";
                    assignedValue = property.stringValue;
                    return true;

                case SerializedPropertyType.Enum:
                    var enumIndex = Array.FindIndex(property.enumDisplayNames,
                        n => string.Equals(n, rawValue, StringComparison.OrdinalIgnoreCase));
                    if (enumIndex < 0)
                    {
                        enumIndex = Array.FindIndex(property.enumNames,
                            n => string.Equals(n, rawValue, StringComparison.OrdinalIgnoreCase));
                    }

                    if (enumIndex < 0 && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIndex))
                    {
                        enumIndex = parsedIndex;
                    }

                    if (enumIndex < 0 || enumIndex >= property.enumDisplayNames.Length)
                    {
                        return false;
                    }

                    property.enumValueIndex = enumIndex;
                    assignedValue = property.enumDisplayNames[enumIndex];
                    return true;

                default:
                    return false;
            }
        }

        private static UnityEngine.Object FindSingleAsset(string assetName, Type expectedType)
        {
            var normalizedName = (assetName ?? "").Trim();
            if (string.IsNullOrEmpty(normalizedName))
            {
                throw new InvalidOperationException("Asset name is required.");
            }

            var exactMatches = new List<UnityEngine.Object>();
            var fuzzyMatches = new List<UnityEngine.Object>();

            foreach (var guid in AssetDatabase.FindAssets(normalizedName))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj == null)
                    {
                        continue;
                    }

                    if (!MatchesExpectedType(obj, expectedType))
                    {
                        continue;
                    }

                    if (string.Equals(obj.name, normalizedName, StringComparison.OrdinalIgnoreCase))
                    {
                        exactMatches.Add(obj);
                    }
                    else if (obj.name.IndexOf(normalizedName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        fuzzyMatches.Add(obj);
                    }
                }
            }

            var matches = exactMatches.Count > 0 ? exactMatches : fuzzyMatches;
            matches = matches
                .GroupBy(obj => AssetDatabase.GetAssetPath(obj) + ":" + obj.name)
                .Select(group => group.First())
                .ToList();

            if (matches.Count == 0)
            {
                throw new InvalidOperationException($"Referenced asset not found: {normalizedName}");
            }

            if (matches.Count > 1)
            {
                var details = string.Join(", ",
                    matches.Take(5).Select(obj => $"{obj.name} ({obj.GetType().Name}) @ {AssetDatabase.GetAssetPath(obj)}"));
                throw new InvalidOperationException($"Referenced asset is ambiguous: {normalizedName}. Matches: {details}");
            }

            return matches[0];
        }

        private static bool IsNullLiteral(string value)
        {
            return string.Equals((value ?? "").Trim(), "null", StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesExpectedType(UnityEngine.Object obj, Type expectedType)
        {
            if (expectedType == null)
            {
                return true;
            }

            return expectedType.IsAssignableFrom(obj.GetType());
        }

        private static Type ResolveReferenceFieldType(Type assetType, string fieldPath)
        {
            if (assetType == null || string.IsNullOrWhiteSpace(fieldPath))
            {
                return null;
            }

            var firstSegment = fieldPath.Split('.')[0];
            if (string.IsNullOrWhiteSpace(firstSegment) || firstSegment == "Array")
            {
                return null;
            }

            var field = FindField(assetType, firstSegment);
            if (field == null)
            {
                return null;
            }

            var fieldType = field.FieldType;
            if (fieldType.IsArray)
            {
                fieldType = fieldType.GetElementType();
            }

            if (fieldType != null && fieldType.IsGenericType)
            {
                var genericType = fieldType.GetGenericTypeDefinition();
                if (genericType == typeof(List<>))
                {
                    fieldType = fieldType.GetGenericArguments()[0];
                }
            }

            return typeof(UnityEngine.Object).IsAssignableFrom(fieldType) ? fieldType : null;
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            while (type != null)
            {
                var field = type.GetField(fieldName, flags);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static Dictionary<string, FieldMetadata> BuildFieldMetadata(ScriptableObject asset)
        {
            var result = new Dictionary<string, FieldMetadata>(StringComparer.Ordinal);
            var sourceDocs = TryLoadFieldDocs(asset);
            foreach (var field in EnumerateSerializableFields(asset.GetType(), null))
            {
                sourceDocs.TryGetValue(field.PropertyPath, out var doc);

                result[field.PropertyPath] = new FieldMetadata
                {
                    DeclaredType = GetFriendlyTypeName(field.Field.FieldType),
                    Header = field.Header,
                    Tooltip = field.Tooltip,
                    Doc = doc ?? ""
                };
            }

            return result;
        }

        private static Dictionary<string, string> TryLoadFieldDocs(ScriptableObject asset)
        {
            try
            {
                return LoadFieldDocs(asset);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        private static IEnumerable<SerializableFieldPath> EnumerateSerializableFields(Type type, string parentPath)
        {
            foreach (var field in GetSerializableFields(type))
            {
                var propertyPath = string.IsNullOrEmpty(parentPath) ? field.Name : parentPath + "." + field.Name;
                var header = field.GetCustomAttribute<HeaderAttribute>()?.header ?? "";
                var tooltip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? "";

                yield return new SerializableFieldPath
                {
                    PropertyPath = propertyPath,
                    Field = field,
                    Header = header,
                    Tooltip = tooltip
                };

                if (ShouldRecurseInto(field.FieldType))
                {
                    foreach (var child in EnumerateSerializableFields(field.FieldType, propertyPath))
                    {
                        yield return child;
                    }
                }
            }
        }

        private static IEnumerable<string> EnumerateSerializablePropertyPaths(Type type, string parentPath)
        {
            foreach (var field in EnumerateSerializableFields(type, parentPath))
            {
                yield return field.PropertyPath;
            }
        }

        private static IEnumerable<FieldInfo> GetSerializableFields(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var stack = new Stack<Type>();
            while (type != null && type != typeof(ScriptableObject) && type != typeof(UnityEngine.Object))
            {
                stack.Push(type);
                type = type.BaseType;
            }

            while (stack.Count > 0)
            {
                foreach (var field in stack.Pop().GetFields(flags))
                {
                    if (field.IsStatic || field.IsNotSerialized)
                    {
                        continue;
                    }

                    if (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
                    {
                        yield return field;
                    }
                }
            }
        }

        private static bool ShouldRecurseInto(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (type == typeof(string) ||
                typeof(UnityEngine.Object).IsAssignableFrom(type) ||
                type.IsEnum ||
                type.IsPrimitive)
            {
                return false;
            }

            if (type.IsArray)
            {
                return false;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return false;
            }

            return type.IsSerializable || (!type.IsAbstract && !type.IsInterface);
        }

        private static Dictionary<string, string> LoadFieldDocs(ScriptableObject asset)
        {
            var script = MonoScript.FromScriptableObject(asset);
            if (script == null)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            var path = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            var docs = new Dictionary<string, string>(StringComparer.Ordinal);
            var lines = File.ReadAllLines(path, DetectEncoding(path));

            for (var i = 0; i < lines.Length; i++)
            {
                var fieldName = TryExtractFieldName(lines[i]);
                if (string.IsNullOrEmpty(fieldName) || docs.ContainsKey(fieldName))
                {
                    continue;
                }

                var collected = new List<string>();
                for (var j = i - 1; j >= 0; j--)
                {
                    var trimmed = lines[j].Trim();
                    if (trimmed.StartsWith("///"))
                    {
                        collected.Insert(0, TrimXmlComment(trimmed));
                        continue;
                    }

                    if (trimmed.Length == 0 || trimmed.StartsWith("["))
                    {
                        continue;
                    }

                    break;
                }

                if (collected.Count > 0)
                {
                    docs[fieldName] = string.Join(" ", collected.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
                }
            }

            return docs;
        }

        private static Encoding DetectEncoding(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return new UTF8Encoding(true);
            }

            return new UTF8Encoding(false);
        }

        private static string TryExtractFieldName(string line)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) ||
                trimmed.StartsWith("//") ||
                trimmed.StartsWith("[") ||
                trimmed.Contains(" class ") ||
                trimmed.Contains(" struct ") ||
                trimmed.Contains("("))
            {
                return null;
            }

            trimmed = trimmed.TrimEnd(';');
            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex >= 0)
            {
                trimmed = trimmed.Substring(0, equalsIndex).TrimEnd();
            }

            var tokens = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
            {
                return null;
            }

            return tokens[tokens.Length - 1];
        }

        private static string TrimXmlComment(string line)
        {
            var text = line.Trim();
            if (text.StartsWith("///"))
            {
                text = text.Substring(3).Trim();
            }

            text = text.Replace("<summary>", "").Replace("</summary>", "").Trim();
            return text;
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type == null)
            {
                return "";
            }

            if (type.IsArray)
            {
                return GetFriendlyTypeName(type.GetElementType()) + "[]";
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return $"List<{GetFriendlyTypeName(type.GetGenericArguments()[0])}>";
            }

            return type.Name;
        }

        private static string NormalizeAssetPath(string rawPath)
        {
            var path = (rawPath ?? "").Trim().Replace('\\', '/');
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException("Asset path is required.");
            }

            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(path, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Asset path must be project-relative and start with Assets/: {path}");
            }

            return path;
        }

        private static string NormalizeCreateAssetPath(string rawPath)
        {
            var path = NormalizeAssetPath(rawPath);
            if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"ScriptableObject asset path must end with .asset: {path}");
            }

            return path;
        }

        private static void EnsureParentFolderExists(string assetPath)
        {
            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                throw new InvalidOperationException($"Target folder does not exist in Assets/: {folder}");
            }
        }

        private static CreatableScriptableObjectDescriptor ResolveCreatableScriptableObjectType(string typeOrMenu)
        {
            var normalized = (typeOrMenu ?? "").Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                throw new InvalidOperationException("ScriptableObject type is required.");
            }

            var candidates = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .Where(type =>
                    type != null &&
                    !type.IsAbstract &&
                    !type.IsGenericType &&
                    !string.IsNullOrEmpty(type.Namespace) &&
                    type.Namespace.StartsWith("BBBNexus", StringComparison.Ordinal))
                .Select(type => new CreatableScriptableObjectDescriptor
                {
                    Type = type,
                    MenuName = type.GetCustomAttribute<CreateAssetMenuAttribute>()?.menuName ?? ""
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.MenuName))
                .ToList();

            var exact = candidates.Where(item =>
                    string.Equals(item.Type.Name, normalized, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.Type.FullName, normalized, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.MenuName, normalized, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (exact.Count == 1)
            {
                return exact[0];
            }

            if (exact.Count > 1)
            {
                var details = string.Join(", ", exact.Select(item => item.Type.FullName));
                throw new InvalidOperationException($"ScriptableObject type is ambiguous: {normalized}. Matches: {details}");
            }

            var fuzzy = candidates.Where(item =>
                    item.Type.Name.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.Type.FullName.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.MenuName.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(5)
                .ToList();
            if (fuzzy.Count == 0)
            {
                throw new InvalidOperationException($"No creatable BBBNexus ScriptableObject type matched: {normalized}");
            }

            var fuzzyDetails = string.Join(", ", fuzzy.Select(item => $"{item.Type.FullName} [{item.MenuName}]"));
            throw new InvalidOperationException($"ScriptableObject type is ambiguous or incomplete: {normalized}. Candidates: {fuzzyDetails}");
        }

        private static string DescribeObjectReference(UnityEngine.Object value)
        {
            if (value == null)
            {
                return "null";
            }

            var path = AssetDatabase.GetAssetPath(value);
            return string.IsNullOrEmpty(path) ? value.name : $"{value.name} @ {path}";
        }

        private sealed class FieldMetadata
        {
            public string DeclaredType;
            public string Header;
            public string Tooltip;
            public string Doc;
        }

        private sealed class CreatableScriptableObjectDescriptor
        {
            public Type Type;
            public string MenuName;
        }

        private sealed class SerializableFieldPath
        {
            public string PropertyPath;
            public FieldInfo Field;
            public string Header;
            public string Tooltip;
        }
    }
}
#endif
