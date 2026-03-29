#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using Animancer;
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

            if (IsAnimationSpeedPath(fieldPath) && IsZeroFloatLiteral(rawValue))
            {
                throw new InvalidOperationException($"Animation speed cannot be 0: {fieldPath}. Use 1 for normal playback.");
            }

            var previousArraySize = property.propertyType == SerializedPropertyType.ArraySize ? property.intValue : -1;

            if (!TryAssignScalar(property, rawValue, out var assignedValue))
            {
                throw new InvalidOperationException($"Unsupported or invalid scalar assignment for {fieldPath} ({property.propertyType})");
            }

            if (property.propertyType == SerializedPropertyType.ArraySize && previousArraySize >= 0)
            {
                InitializeNewArrayElements(serializedObject, fieldPath, previousArraySize, property.intValue);
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

        public static RenameAssetResponse RenameAsset(string rawPath, string newName)
        {
            var assetPath = NormalizeAssetPath(rawPath);
            var trimmedName = (newName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                throw new InvalidOperationException("New asset name cannot be empty.");
            }

            trimmedName = Path.GetFileNameWithoutExtension(trimmedName);
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                throw new InvalidOperationException("New asset name is invalid.");
            }

            var extension = Path.GetExtension(assetPath);
            var directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "Assets";
            var expectedPath = $"{directory}/{trimmedName}{extension}";
            if (string.Equals(assetPath, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                return new RenameAssetResponse
                {
                    oldAssetPath = assetPath,
                    newAssetPath = assetPath
                };
            }

            if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
            {
                throw new InvalidOperationException($"Asset not found: {assetPath}");
            }

            if (AssetDatabase.LoadMainAssetAtPath(expectedPath) != null)
            {
                throw new InvalidOperationException($"Asset already exists: {expectedPath}");
            }

            var error = AssetDatabase.RenameAsset(assetPath, trimmedName);
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException($"Failed to rename asset: {error}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new RenameAssetResponse
            {
                oldAssetPath = assetPath,
                newAssetPath = expectedPath
            };
        }

        public static MetaLibSoRebuildResponse RebuildMetaLibSoEntries()
        {
            MetaLib.Reload();

            var preserved = MetaLib.GetAllMetas()
                .Where(entry => entry != null && entry.Kind != MetaLib.EntryKind.ResourceObject)
                .ToDictionary(entry => entry.EffectiveID, entry => entry, StringComparer.Ordinal);

            var duplicateMap = new Dictionary<string, DuplicateSoGroup>(StringComparer.Ordinal);
            var soEntries = BuildMetaLibSoEntries(duplicateMap);

            if (duplicateMap.Count > 0)
            {
                return new MetaLibSoRebuildResponse
                {
                    updated = false,
                    preservedCount = preserved.Count,
                    registeredSoCount = 0,
                    duplicates = duplicateMap.Values
                        .OrderBy(group => group.Id, StringComparer.Ordinal)
                        .Select(group => new MetaLibDuplicateDto
                        {
                            id = group.Id,
                            assetPaths = group.AssetPaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
                            resourcePaths = group.ResourcePaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()
                        })
                        .ToArray()
                };
            }

            var merged = new Dictionary<string, MetaLib.MetaEntry>(StringComparer.Ordinal);
            foreach (var pair in preserved)
            {
                merged[pair.Key] = pair.Value;
            }

            foreach (var pair in soEntries)
            {
                if (merged.ContainsKey(pair.Key))
                {
                    if (!duplicateMap.TryGetValue(pair.Key, out var conflict))
                    {
                        var existing = merged[pair.Key];
                        conflict = new DuplicateSoGroup
                        {
                            Id = pair.Key,
                            AssetPaths = new List<string>(),
                            ResourcePaths = new List<string>()
                        };

                        if (existing.CustomFields != null && existing.CustomFields.TryGetValue("AssetPath", out var existingAssetPath))
                        {
                            conflict.AssetPaths.Add(existingAssetPath);
                        }

                        if (!string.IsNullOrEmpty(existing.ResourcePath))
                        {
                            conflict.ResourcePaths.Add(existing.ResourcePath);
                        }

                        duplicateMap[pair.Key] = conflict;
                    }

                    if (pair.Value.CustomFields != null && pair.Value.CustomFields.TryGetValue("AssetPath", out var soAssetPath))
                    {
                        conflict.AssetPaths.Add(soAssetPath);
                    }

                    if (!string.IsNullOrEmpty(pair.Value.ResourcePath))
                    {
                        conflict.ResourcePaths.Add(pair.Value.ResourcePath);
                    }

                    continue;
                }

                merged[pair.Key] = pair.Value;
            }

            if (duplicateMap.Count > 0)
            {
                return new MetaLibSoRebuildResponse
                {
                    updated = false,
                    preservedCount = preserved.Count,
                    registeredSoCount = 0,
                    duplicates = duplicateMap.Values
                        .OrderBy(group => group.Id, StringComparer.Ordinal)
                        .Select(group => new MetaLibDuplicateDto
                        {
                            id = group.Id,
                            assetPaths = group.AssetPaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
                            resourcePaths = group.ResourcePaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()
                        })
                        .ToArray()
                };
            }

            MetaLib.Clear();
            foreach (var pair in merged)
            {
                MetaLib.Register(pair.Key, pair.Value);
            }

            MetaLib.Save();

            return new MetaLibSoRebuildResponse
            {
                updated = true,
                preservedCount = preserved.Count,
                registeredSoCount = soEntries.Count,
                duplicates = Array.Empty<MetaLibDuplicateDto>()
            };
        }

        public static InspectAssetResponse InspectAsset(string rawPath)
        {
            var assetPath = NormalizeAssetPath(rawPath);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"ScriptableObject not found: {assetPath}");
            }

            var serializedObject = new SerializedObject(asset);
            var fields = new List<InspectorFieldDto>();
            foreach (var semanticField in EnumerateInspectorFields(asset.GetType(), null))
            {
                fields.Add(ToInspectorField(serializedObject, semanticField));
            }

            return new InspectAssetResponse
            {
                assetPath = assetPath,
                assetType = asset.GetType().FullName,
                fields = fields.ToArray()
            };
        }

        public static SetFieldResponse SetInspectorValue(string rawPath, string semanticPath, string rawValue)
        {
            var assetPath = NormalizeAssetPath(rawPath);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"ScriptableObject not found: {assetPath}");
            }

            var mapping = ResolveInspectorField(asset.GetType(), semanticPath);
            var serializedObject = new SerializedObject(asset);

            if (mapping.Kind == InspectorFieldKind.RawProperty &&
                IsAnimationSpeedPath(mapping.RawPath) &&
                IsZeroFloatLiteral(rawValue))
            {
                throw new InvalidOperationException($"Animation speed cannot be 0: {semanticPath}. Use 1 for normal playback.");
            }

            if (mapping.Kind == InspectorFieldKind.ObjectReference)
            {
                var property = serializedObject.FindProperty(mapping.RawPath);
                if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    throw new InvalidOperationException($"Inspector field is not a reference: {semanticPath}");
                }

                var expectedType = ResolveReferenceFieldType(asset.GetType(), mapping.RawPath);
                var reference = IsNullLiteral(rawValue)
                    ? null
                    : FindSingleAsset(rawValue, expectedType);
                property.objectReferenceValue = reference;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                return new SetFieldResponse
                {
                    assetPath = assetPath,
                    assetType = asset.GetType().FullName,
                    field = semanticPath,
                    type = "InspectorReference",
                    value = reference == null ? "null" : AssetDatabase.GetAssetPath(reference)
                };
            }

            if (mapping.Kind == InspectorFieldKind.EndTime)
            {
                SetClipTransitionEndTime(serializedObject, mapping.RawPath, rawValue);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                return new SetFieldResponse
                {
                    assetPath = assetPath,
                    assetType = asset.GetType().FullName,
                    field = semanticPath,
                    type = "InspectorValue",
                    value = rawValue
                };
            }

            return SetField(assetPath, mapping.RawPath, rawValue);
        }

        public static ListFieldResponse GetListField(string rawPath, string fieldPath)
        {
            var assetPath = NormalizeAssetPath(rawPath);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"ScriptableObject not found: {assetPath}");
            }

            var serializedObject = new SerializedObject(asset);
            var property = RequireListProperty(serializedObject, fieldPath);

            var items = new List<ListElementDto>();
            for (var i = 0; i < property.arraySize; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                items.Add(new ListElementDto
                {
                    index = i,
                    type = element.propertyType.ToString(),
                    value = ReadValue(element)
                });
            }

            return new ListFieldResponse
            {
                assetPath = assetPath,
                assetType = asset.GetType().FullName,
                field = fieldPath,
                elementType = ResolveListElementType(asset.GetType(), fieldPath)?.Name ?? "",
                size = property.arraySize,
                items = items.ToArray()
            };
        }

        public static ListFieldResponse AddListItem(string rawPath, string fieldPath, string rawValue)
        {
            return MutateList(rawPath, fieldPath, serializedObject =>
            {
                var property = RequireListProperty(serializedObject, fieldPath);
                var index = property.arraySize;
                property.InsertArrayElementAtIndex(index);
                var element = property.GetArrayElementAtIndex(index);
                ResetArrayElement(element);
                AssignListElement(serializedObject.targetObject.GetType(), fieldPath, element, rawValue);
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        public static ListFieldResponse SetListItem(string rawPath, string fieldPath, int index, string rawValue)
        {
            return MutateList(rawPath, fieldPath, serializedObject =>
            {
                var property = RequireListProperty(serializedObject, fieldPath);
                if (index < 0 || index >= property.arraySize)
                {
                    throw new InvalidOperationException($"List index out of range: {fieldPath}[{index}]");
                }

                var element = property.GetArrayElementAtIndex(index);
                AssignListElement(serializedObject.targetObject.GetType(), fieldPath, element, rawValue);
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        public static ListFieldResponse RemoveListItem(string rawPath, string fieldPath, int index)
        {
            return MutateList(rawPath, fieldPath, serializedObject =>
            {
                var property = RequireListProperty(serializedObject, fieldPath);
                if (index < 0 || index >= property.arraySize)
                {
                    throw new InvalidOperationException($"List index out of range: {fieldPath}[{index}]");
                }

                var element = property.GetArrayElementAtIndex(index);
                if (element.propertyType == SerializedPropertyType.ObjectReference && element.objectReferenceValue != null)
                {
                    property.DeleteArrayElementAtIndex(index);
                }

                property.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        public static ListFieldResponse ClearListField(string rawPath, string fieldPath)
        {
            return MutateList(rawPath, fieldPath, serializedObject =>
            {
                var property = RequireListProperty(serializedObject, fieldPath);
                property.ClearArray();
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            });
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
                    if (string.Equals(rawValue, "NaN", StringComparison.OrdinalIgnoreCase))
                    {
                        property.floatValue = float.NaN;
                        assignedValue = "NaN";
                        return true;
                    }

                    if (string.Equals(rawValue, "Infinity", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(rawValue, "+Infinity", StringComparison.OrdinalIgnoreCase))
                    {
                        property.floatValue = float.PositiveInfinity;
                        assignedValue = "Infinity";
                        return true;
                    }

                    if (string.Equals(rawValue, "-Infinity", StringComparison.OrdinalIgnoreCase))
                    {
                        property.floatValue = float.NegativeInfinity;
                        assignedValue = "-Infinity";
                        return true;
                    }

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

                case SerializedPropertyType.ArraySize:
                    if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var arraySize))
                    {
                        return false;
                    }

                    if (arraySize < 0)
                    {
                        return false;
                    }

                    property.intValue = arraySize;
                    assignedValue = arraySize.ToString(CultureInfo.InvariantCulture);
                    return true;

                default:
                    return false;
            }
        }

        private static ListFieldResponse MutateList(string rawPath, string fieldPath, Action<SerializedObject> mutation)
        {
            var assetPath = NormalizeAssetPath(rawPath);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"ScriptableObject not found: {assetPath}");
            }

            var serializedObject = new SerializedObject(asset);
            RequireListProperty(serializedObject, fieldPath);
            mutation(serializedObject);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return GetListField(assetPath, fieldPath);
        }

        private static void InitializeNewArrayElements(SerializedObject serializedObject, string arraySizePath, int oldSize, int newSize)
        {
            if (newSize <= oldSize || !arraySizePath.EndsWith(".Array.size", StringComparison.Ordinal))
            {
                return;
            }

            var arrayPath = arraySizePath[..^".Array.size".Length];
            var arrayProperty = serializedObject.FindProperty(arrayPath);
            if (arrayProperty == null || !arrayProperty.isArray)
            {
                return;
            }

            for (var i = oldSize; i < newSize; i++)
            {
                InitializeElementDefaults(arrayProperty.GetArrayElementAtIndex(i));
            }
        }

        private static void InitializeElementDefaults(SerializedProperty element)
        {
            if (element == null)
            {
                return;
            }

            if (LooksLikeClipTransition(element))
            {
                SetFloatIfExists(element, "_FadeDuration", 0.25f);
                SetFloatIfExists(element, "_Speed", 1f);
                SetFloatIfExists(element, "_NormalizedStartTime", float.NaN);
            }
        }

        private static bool LooksLikeClipTransition(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.Generic &&
                   property.FindPropertyRelative("_Clip") != null &&
                   property.FindPropertyRelative("_Speed") != null;
        }

        private static void SetFloatIfExists(SerializedProperty parent, string relativePath, float value)
        {
            var child = parent.FindPropertyRelative(relativePath);
            if (child != null && child.propertyType == SerializedPropertyType.Float)
            {
                child.floatValue = value;
            }
        }

        private static bool IsAnimationSpeedPath(string propertyPath)
        {
            return !string.IsNullOrWhiteSpace(propertyPath) &&
                   (propertyPath.EndsWith("._Speed", StringComparison.Ordinal) ||
                    propertyPath.EndsWith(".Speed", StringComparison.Ordinal));
        }

        private static bool IsZeroFloatLiteral(string rawValue)
        {
            return float.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value) &&
                   Mathf.Approximately(value, 0f);
        }

        private static SerializedProperty RequireListProperty(SerializedObject serializedObject, string fieldPath)
        {
            var property = serializedObject.FindProperty(fieldPath);
            if (property == null)
            {
                throw new InvalidOperationException($"Field not found: {fieldPath}");
            }

            if (!property.isArray || property.propertyType == SerializedPropertyType.String)
            {
                throw new InvalidOperationException($"Field is not a supported list/array: {fieldPath}");
            }

            return property;
        }

        private static void ResetArrayElement(SerializedProperty element)
        {
            switch (element.propertyType)
            {
                case SerializedPropertyType.Integer:
                    element.intValue = 0;
                    break;
                case SerializedPropertyType.Boolean:
                    element.boolValue = false;
                    break;
                case SerializedPropertyType.Float:
                    element.floatValue = 0f;
                    break;
                case SerializedPropertyType.String:
                    element.stringValue = "";
                    break;
                case SerializedPropertyType.Enum:
                    element.enumValueIndex = 0;
                    break;
                case SerializedPropertyType.ObjectReference:
                    element.objectReferenceValue = null;
                    break;
            }

            InitializeElementDefaults(element);
        }

        private static void AssignListElement(Type assetType, string fieldPath, SerializedProperty element, string rawValue)
        {
            if (element.propertyType == SerializedPropertyType.ObjectReference)
            {
                var expectedType = ResolveListElementType(assetType, fieldPath);
                element.objectReferenceValue = IsNullLiteral(rawValue) ? null : FindSingleAsset(rawValue, expectedType);
                return;
            }

            if (element.propertyType == SerializedPropertyType.Float &&
                IsAnimationSpeedPath(element.propertyPath) &&
                IsZeroFloatLiteral(rawValue))
            {
                throw new InvalidOperationException($"Animation speed cannot be 0: {element.propertyPath}. Use 1 for normal playback.");
            }

            if (TryAssignScalar(element, rawValue, out _))
            {
                return;
            }

            throw new InvalidOperationException($"Unsupported list element assignment for {fieldPath} ({element.propertyType})");
        }

        private static UnityEngine.Object FindSingleAsset(string assetName, Type expectedType)
        {
            var normalizedName = (assetName ?? "").Trim();
            if (string.IsNullOrEmpty(normalizedName))
            {
                throw new InvalidOperationException("Asset name is required.");
            }

            if (TryParseAssetSelector(normalizedName, out var selectorPath, out var selectorName))
            {
                return FindSingleAssetByPath(selectorPath, selectorName, expectedType);
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
                var details = string.Join(Environment.NewLine,
                    matches.Take(10).Select(DescribeAssetCandidate));
                throw new InvalidOperationException(
                    $"Referenced asset is ambiguous: {normalizedName}{Environment.NewLine}Candidates:{Environment.NewLine}{details}");
            }

            return matches[0];
        }

        private static bool TryParseAssetSelector(string rawInput, out string assetPath, out string nameHint)
        {
            assetPath = null;
            nameHint = null;

            var normalized = (rawInput ?? "").Trim().Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                assetPath = normalized;
                return true;
            }

            var markerIndex = normalized.LastIndexOf(" @ Assets/", StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return false;
            }

            nameHint = normalized.Substring(0, markerIndex).Trim();
            assetPath = normalized.Substring(markerIndex + 3).Trim();
            return assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
        }

        private static UnityEngine.Object FindSingleAssetByPath(string assetPath, string nameHint, Type expectedType)
        {
            var normalizedPath = NormalizeAssetPath(assetPath);
            var candidates = AssetDatabase.LoadAllAssetsAtPath(normalizedPath)
                .Where(obj => obj != null && MatchesExpectedType(obj, expectedType))
                .ToList();

            if (!string.IsNullOrWhiteSpace(nameHint))
            {
                candidates = candidates
                    .Where(obj => string.Equals(obj.name, nameHint, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            candidates = candidates
                .GroupBy(obj => AssetDatabase.GetAssetPath(obj) + ":" + obj.name + ":" + obj.GetType().FullName)
                .Select(group => group.First())
                .ToList();

            if (candidates.Count == 0)
            {
                var detail = string.IsNullOrWhiteSpace(nameHint) ? normalizedPath : $"{nameHint} @ {normalizedPath}";
                throw new InvalidOperationException($"Referenced asset not found: {detail}");
            }

            if (candidates.Count > 1)
            {
                var details = string.Join(Environment.NewLine,
                    candidates.Take(10).Select(DescribeAssetCandidate));
                throw new InvalidOperationException(
                    $"Referenced asset is ambiguous at path: {normalizedPath}{Environment.NewLine}Candidates:{Environment.NewLine}{details}");
            }

            return candidates[0];
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

        private static InspectorFieldDto ToInspectorField(SerializedObject serializedObject, InspectorSemanticField semanticField)
        {
            if (semanticField.Kind == InspectorFieldKind.EndTime)
            {
                return new InspectorFieldDto
                {
                    path = semanticField.SemanticPath,
                    rawPath = semanticField.RawPath,
                    type = "Single",
                    value = ReadClipTransitionEndTime(serializedObject, semanticField.RawPath),
                    tooltip = semanticField.Tooltip,
                    editable = true,
                    derived = true
                };
            }

            var property = serializedObject.FindProperty(semanticField.RawPath);
            return new InspectorFieldDto
            {
                path = semanticField.SemanticPath,
                rawPath = semanticField.RawPath,
                type = property?.propertyType.ToString() ?? semanticField.DeclaredType,
                value = property != null ? ReadValue(property) : "",
                tooltip = semanticField.Tooltip,
                editable = true,
                derived = false
            };
        }

        private static IEnumerable<InspectorSemanticField> EnumerateInspectorFields(Type type, string parentPath)
        {
            foreach (var field in GetSerializableFields(type))
            {
                var propertyPath = string.IsNullOrEmpty(parentPath) ? field.Name : parentPath + "." + field.Name;
                var tooltip = GetAttribute<TooltipAttribute>(field)?.tooltip ?? "";

                if (field.FieldType == typeof(ClipTransition))
                {
                    yield return new InspectorSemanticField
                    {
                        SemanticPath = propertyPath + ".Animation",
                        RawPath = propertyPath + "._Clip",
                        Kind = InspectorFieldKind.ObjectReference,
                        DeclaredType = nameof(AnimationClip),
                        Tooltip = "The animation to play"
                    };
                    yield return new InspectorSemanticField
                    {
                        SemanticPath = propertyPath + ".FadeDuration",
                        RawPath = propertyPath + "._FadeDuration",
                        Kind = InspectorFieldKind.RawProperty,
                        DeclaredType = nameof(Single),
                        Tooltip = "The amount of time the transition will take"
                    };
                    yield return new InspectorSemanticField
                    {
                        SemanticPath = propertyPath + ".Speed",
                        RawPath = propertyPath + "._Speed",
                        Kind = InspectorFieldKind.RawProperty,
                        DeclaredType = nameof(Single),
                        Tooltip = "How fast the animation will play"
                    };
                    yield return new InspectorSemanticField
                    {
                        SemanticPath = propertyPath + ".StartTime",
                        RawPath = propertyPath + "._NormalizedStartTime",
                        Kind = InspectorFieldKind.RawProperty,
                        DeclaredType = nameof(Single),
                        Tooltip = "Normalized start time shown in the Animancer inspector"
                    };
                    yield return new InspectorSemanticField
                    {
                        SemanticPath = propertyPath + ".EndTime",
                        RawPath = propertyPath + "._Events",
                        Kind = InspectorFieldKind.EndTime,
                        DeclaredType = nameof(Single),
                        Tooltip = "Normalized end time shown in the Animancer inspector"
                    };
                    continue;
                }

                if (field.FieldType == typeof(AnimPlayOptions))
                {
                    foreach (var child in GetSerializableFields(field.FieldType))
                    {
                        yield return new InspectorSemanticField
                        {
                            SemanticPath = propertyPath + "." + child.Name,
                            RawPath = propertyPath + "." + child.Name,
                            Kind = InspectorFieldKind.RawProperty,
                            DeclaredType = GetFriendlyTypeName(child.FieldType),
                            Tooltip = GetAttribute<TooltipAttribute>(child)?.tooltip ?? tooltip
                        };
                    }

                    continue;
                }

                if (ShouldRecurseInto(field.FieldType))
                {
                    foreach (var child in EnumerateInspectorFields(field.FieldType, propertyPath))
                    {
                        yield return child;
                    }

                    continue;
                }

                yield return new InspectorSemanticField
                {
                    SemanticPath = propertyPath,
                    RawPath = propertyPath,
                    Kind = field.FieldType != typeof(string) && typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)
                        ? InspectorFieldKind.ObjectReference
                        : InspectorFieldKind.RawProperty,
                    DeclaredType = GetFriendlyTypeName(field.FieldType),
                    Tooltip = tooltip
                };
            }
        }

        private static InspectorSemanticField ResolveInspectorField(Type assetType, string semanticPath)
        {
            var match = EnumerateInspectorFields(assetType, null)
                .FirstOrDefault(field => string.Equals(field.SemanticPath, semanticPath, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                throw new InvalidOperationException($"Inspector field not found: {semanticPath}");
            }

            return match;
        }

        private static string ReadClipTransitionEndTime(SerializedObject serializedObject, string eventsPath)
        {
            var times = serializedObject.FindProperty(eventsPath + "._NormalizedTimes");
            var speedProperty = serializedObject.FindProperty(eventsPath[..^"._Events".Length] + "._Speed");
            var speed = speedProperty != null ? speedProperty.floatValue : 1f;

            if (times == null || !times.isArray || times.arraySize == 0)
            {
                return AnimancerEvent.Sequence.GetDefaultNormalizedEndTime(speed).ToString(CultureInfo.InvariantCulture);
            }

            var endElement = times.GetArrayElementAtIndex(times.arraySize - 1);
            return endElement.floatValue.ToString(CultureInfo.InvariantCulture);
        }

        private static void SetClipTransitionEndTime(SerializedObject serializedObject, string eventsPath, string rawValue)
        {
            if (!float.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
            {
                throw new InvalidOperationException($"Invalid end time: {rawValue}");
            }

            var times = serializedObject.FindProperty(eventsPath + "._NormalizedTimes");
            if (times == null)
            {
                throw new InvalidOperationException($"Event times field not found: {eventsPath}._NormalizedTimes");
            }

            if (!times.isArray)
            {
                throw new InvalidOperationException($"Event times field is not an array: {eventsPath}._NormalizedTimes");
            }

            if (times.arraySize == 0)
            {
                times.arraySize = 1;
            }

            times.GetArrayElementAtIndex(times.arraySize - 1).floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Type ResolveReferenceFieldType(Type assetType, string fieldPath)
        {
            if (assetType == null || string.IsNullOrWhiteSpace(fieldPath))
            {
                return null;
            }

            var currentType = assetType;
            foreach (var segment in fieldPath.Split('.'))
            {
                if (string.IsNullOrWhiteSpace(segment) || segment == "Array")
                {
                    return null;
                }

                if (segment == "data")
                {
                    continue;
                }

                var field = FindField(currentType, segment);
                if (field == null)
                {
                    return null;
                }

                currentType = field.FieldType;
                if (currentType.IsArray)
                {
                    currentType = currentType.GetElementType();
                }

                if (currentType != null && currentType.IsGenericType &&
                    currentType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    currentType = currentType.GetGenericArguments()[0];
                }
            }

            return currentType != null && typeof(UnityEngine.Object).IsAssignableFrom(currentType) ? currentType : null;
        }

        private static Type ResolveListElementType(Type assetType, string fieldPath)
        {
            var fieldType = ResolveFieldType(assetType, fieldPath);
            if (fieldType == null)
            {
                return null;
            }

            if (fieldType.IsArray)
            {
                return fieldType.GetElementType();
            }

            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return fieldType.GetGenericArguments()[0];
            }

            return null;
        }

        private static Type ResolveFieldType(Type assetType, string fieldPath)
        {
            if (assetType == null || string.IsNullOrWhiteSpace(fieldPath))
            {
                return null;
            }

            var currentType = assetType;
            foreach (var segment in fieldPath.Split('.'))
            {
                if (string.IsNullOrWhiteSpace(segment) || segment == "Array" || segment == "data")
                {
                    continue;
                }

                var field = FindField(currentType, segment);
                if (field == null)
                {
                    return null;
                }

                currentType = field.FieldType;
            }

            return currentType;
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
                var header = GetAttribute<HeaderAttribute>(field)?.header ?? "";
                var tooltip = GetAttribute<TooltipAttribute>(field)?.tooltip ?? "";

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

                    if (field.IsPublic || GetAttribute<SerializeField>(field) != null)
                    {
                        yield return field;
                    }
                }
            }
        }

        private static T GetAttribute<T>(MemberInfo member) where T : Attribute
        {
            return member.GetCustomAttributes(typeof(T), true).OfType<T>().LastOrDefault();
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
            if (string.IsNullOrEmpty(folder))
            {
                throw new InvalidOperationException($"Target folder is invalid: {folder}");
            }

            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            CreateFoldersRecursively(folder);
            AssetDatabase.Refresh();

            if (!AssetDatabase.IsValidFolder(folder))
            {
                throw new InvalidOperationException($"Failed to create target folder in Assets/: {folder}");
            }
        }

        private static void CreateFoldersRecursively(string folder)
        {
            if (string.Equals(folder, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
            {
                throw new InvalidOperationException($"Invalid folder path: {folder}");
            }

            if (!AssetDatabase.IsValidFolder(parent))
            {
                CreateFoldersRecursively(parent);
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        private static Dictionary<string, MetaLib.MetaEntry> BuildMetaLibSoEntries(Dictionary<string, DuplicateSoGroup> duplicateMap)
        {
            var results = new Dictionary<string, MetaLib.MetaEntry>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject"))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (assetPath.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) < 0 &&
                    assetPath.IndexOf("\\Resources\\", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var scriptableObject = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (scriptableObject == null)
                {
                    continue;
                }

                var type = scriptableObject.GetType();
                if (string.IsNullOrEmpty(type.Namespace) ||
                    !type.Namespace.StartsWith("BBBNexus", StringComparison.Ordinal))
                {
                    continue;
                }

                var id = scriptableObject.name;
                var resourcePath = ToResourcesPath(assetPath);

                if (results.TryGetValue(id, out var existing))
                {
                    duplicateMap[id] = new DuplicateSoGroup
                    {
                        Id = id,
                        AssetPaths = new List<string>
                        {
                            existing.CustomFields != null && existing.CustomFields.TryGetValue("AssetPath", out var existingAssetPath)
                                ? existingAssetPath
                                : existing.ResourcePath,
                            assetPath
                        },
                        ResourcePaths = new List<string>
                        {
                            existing.ResourcePath,
                            resourcePath
                        }
                    };

                    results.Remove(id);
                    continue;
                }

                if (duplicateMap.TryGetValue(id, out var duplicate))
                {
                    duplicate.AssetPaths.Add(assetPath);
                    duplicate.ResourcePaths.Add(resourcePath);
                    continue;
                }

                results[id] = new MetaLib.MetaEntry
                {
                    ID = id,
                    PackID = id,
                    Kind = MetaLib.EntryKind.ResourceObject,
                    Storage = MetaLib.StorageType.Resources,
                    ResourcePath = resourcePath,
                    ObjectType = type.FullName,
                    DisplayName = scriptableObject.name,
                    Author = "NekoTeam",
                    Version = "1.0.0",
                    Description = string.Empty,
                    CustomFields = new Dictionary<string, string>
                    {
                        ["AssetPath"] = assetPath
                    }
                };
            }

            return results;
        }

        private static string ToResourcesPath(string assetPath)
        {
            var normalized = assetPath.Replace('\\', '/');
            const string marker = "/Resources/";
            var markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                throw new InvalidOperationException($"Asset is not under a Resources folder: {assetPath}");
            }

            var relative = normalized.Substring(markerIndex + marker.Length);
            if (relative.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                relative = relative.Substring(0, relative.Length - ".asset".Length);
            }

            return relative;
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

        private static string DescribeAssetCandidate(UnityEngine.Object obj)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            var guid = "";
            long localFileId = 0;
            if (!string.IsNullOrEmpty(path))
            {
                guid = AssetDatabase.AssetPathToGUID(path);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out _, out localFileId);
            }

            return $"- {obj.name} ({obj.GetType().Name}) | guid={guid} | localFileId={localFileId} | path={path}";
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

        private sealed class DuplicateSoGroup
        {
            public string Id;
            public List<string> AssetPaths = new List<string>();
            public List<string> ResourcePaths = new List<string>();
        }

        private sealed class SerializableFieldPath
        {
            public string PropertyPath;
            public FieldInfo Field;
            public string Header;
            public string Tooltip;
        }

        private sealed class InspectorSemanticField
        {
            public string SemanticPath;
            public string RawPath;
            public InspectorFieldKind Kind;
            public string DeclaredType;
            public string Tooltip;
        }

        private enum InspectorFieldKind
        {
            RawProperty,
            ObjectReference,
            EndTime
        }
    }
}
#endif
