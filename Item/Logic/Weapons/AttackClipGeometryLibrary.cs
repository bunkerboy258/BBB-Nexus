using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BBBNexus
{
    public static class AttackClipGeometryLibrary
    {
        private static readonly Dictionary<string, AttackClipGeometryDefinition> Cache =
            new Dictionary<string, AttackClipGeometryDefinition>(StringComparer.Ordinal);

        public static bool TryLoad(string geometryId, out AttackClipGeometryDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(geometryId))
            {
                return false;
            }

            geometryId = geometryId.Trim();
            if (Cache.TryGetValue(geometryId, out definition))
            {
                return definition != null;
            }

            if (!MetaLib.HasMeta(geometryId))
            {
                Cache[geometryId] = null;
                return false;
            }

            try
            {
                definition = MetaLib.GetObject<AttackClipGeometryDefinition>(geometryId);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AttackClipGeometryLibrary] Failed to load '{geometryId}': {e.Message}");
                definition = null;
            }

            Cache[geometryId] = definition;
            return definition != null;
        }

        public static AttackClipGeometryDefinition LoadOrNull(string geometryId)
        {
            TryLoad(geometryId, out AttackClipGeometryDefinition definition);
            return definition;
        }

        public static void ClearCache()
        {
            Cache.Clear();
        }

#if UNITY_EDITOR
        public static string ToResourcePath(string geometryId)
        {
            return $"AttackClipGeometry/{geometryId.Trim()}";
        }

        public static string ToAssetPath(string geometryId)
        {
            return Path.Combine("Assets/Resources", ToResourcePath(geometryId) + ".json").Replace("\\", "/");
        }

        public static void WriteDefinitionAndRegister(string geometryId, AttackClipGeometryDefinition definition, string displayName)
        {
            if (string.IsNullOrWhiteSpace(geometryId))
            {
                throw new InvalidOperationException("Attack geometry id is required.");
            }

            geometryId = geometryId.Trim();
            string resourcePath = ToResourcePath(geometryId);
            string assetPath = ToAssetPath(geometryId);
            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath).Replace("\\", "/");
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonConvert.SerializeObject(definition, Formatting.Indented);
            File.WriteAllText(absolutePath, json);

            MetaLib.Reload();
            MetaLib.Register(geometryId, new MetaLib.MetaEntry
            {
                ID = geometryId,
                PackID = geometryId,
                Kind = MetaLib.EntryKind.ResourceObject,
                Storage = MetaLib.StorageType.Resources,
                ResourcePath = resourcePath,
                ObjectType = typeof(AttackClipGeometryDefinition).FullName,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? geometryId : displayName,
                Author = "Codex",
                Version = "1.0.0",
                Description = "Attack clip geometry definition.",
                CustomFields = new Dictionary<string, string>
                {
                    ["AssetPath"] = assetPath
                }
            });
            MetaLib.Save();
            MetaLib.Reload();
            AssetDatabase.Refresh();
            ClearCache();
        }
#endif
    }
}
