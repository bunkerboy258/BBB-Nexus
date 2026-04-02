using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "New KeyItemSO", menuName = "BBBNexus/Items/Key Item")]
    public class KeyItemSO : ItemDefinitionSO
    {
        [Header("--- 钥匙语义 ---")]
        [Tooltip("给关卡看的备注。真正用于判定的仍然是 ItemID。")]
        public string KeyTag;

        protected override void OnValidate()
        {
            base.OnValidate();

#if UNITY_EDITOR
            string canonicalName = name;
            string assetPath = AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                canonicalName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            }

            if (!string.IsNullOrWhiteSpace(canonicalName) && ItemID != canonicalName)
            {
                ItemID = canonicalName;
                EditorUtility.SetDirty(this);
            }
#endif
        }
    }

}
