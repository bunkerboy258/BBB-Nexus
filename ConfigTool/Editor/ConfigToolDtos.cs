#if UNITY_EDITOR
using System;

namespace BBBNexus
{
    [Serializable]
    internal sealed class ConfigToolEnvelope
    {
        public bool ok;
        public string error;
        public string json;

        public static ConfigToolEnvelope Ok(string json)
        {
            return new ConfigToolEnvelope { ok = true, error = "", json = json ?? "" };
        }

        public static ConfigToolEnvelope Fail(string error)
        {
            return new ConfigToolEnvelope { ok = false, error = error ?? "Unknown error", json = "" };
        }
    }

    [Serializable]
    internal sealed class HealthResponse
    {
        public string status;
        public string projectPath;
        public string unityVersion;
    }

    [Serializable]
    internal sealed class FieldsRequest
    {
        public string path;
    }

    [Serializable]
    internal sealed class SetFieldRequest
    {
        public string path;
        public string field;
        public string value;
    }

    [Serializable]
    internal sealed class SetReferenceRequest
    {
        public string path;
        public string field;
        public string assetName;
    }

    [Serializable]
    internal sealed class CreateScriptableObjectRequest
    {
        public string type;
        public string path;
    }

    [Serializable]
    internal sealed class FindClipRequest
    {
        public string query;
    }

    [Serializable]
    internal sealed class FieldInfoDto
    {
        public string name;
        public string path;
        public string type;
        public string declaredType;
        public string value;
        public string header;
        public string tooltip;
        public string doc;
        public bool isArray;
    }

    [Serializable]
    internal sealed class FieldListResponse
    {
        public string assetPath;
        public string assetType;
        public FieldInfoDto[] fields;
    }

    [Serializable]
    internal sealed class SetFieldResponse
    {
        public string assetPath;
        public string assetType;
        public string field;
        public string type;
        public string value;
    }

    [Serializable]
    internal sealed class AssetRefDto
    {
        public string name;
        public string type;
        public string guid;
        public long localFileId;
        public string assetPath;
    }

    [Serializable]
    internal sealed class ClipListResponse
    {
        public AssetRefDto[] clips;
    }

    [Serializable]
    internal sealed class ScriptableObjectTypeDto
    {
        public string name;
        public string fullName;
        public string assemblyName;
        public bool hasCreateAssetMenu;
        public string menuName;
        public string fileName;
    }

    [Serializable]
    internal sealed class ScriptableObjectTypeListResponse
    {
        public ScriptableObjectTypeDto[] types;
    }

    [Serializable]
    internal sealed class CreateScriptableObjectResponse
    {
        public string assetPath;
        public string assetType;
        public string menuName;
    }
}
#endif
