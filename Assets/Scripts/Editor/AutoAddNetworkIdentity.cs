using UnityEditor;
using UnityEngine;
using Mirror;

public class AutoAddNetworkIdentity
{
    [MenuItem("Tools/一键修补缺失的 NetworkIdentity", false, 100)]
    public static void PerformAdd()
    {
        int sceneCount = 0;
        int prefabCount = 0;

        // 1. 处理当前打开场景中的所有物体
        NetworkBehaviour[] nbs = Object.FindObjectsByType<NetworkBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var nb in nbs)
        {
            if (nb.gameObject.GetComponent<NetworkIdentity>() == null)
            {
                Undo.AddComponent<NetworkIdentity>(nb.gameObject);
                sceneCount++;
                // 标记为已修改以便保存场景
                EditorUtility.SetDirty(nb.gameObject);
            }
        }

        // 2. 遍历整个项目所有的预制体（Prefab）
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                // 注意：在提取预制体组件时，要遍历它下面挂载了 NetworkBehaviour 的所有子节点
                NetworkBehaviour[] pNbs = prefab.GetComponentsInChildren<NetworkBehaviour>(true);
                if (pNbs.Length > 0)
                {
                    bool changed = false;
                    foreach (var nb in pNbs)
                    {
                        if (nb.gameObject.GetComponent<NetworkIdentity>() == null)
                        {
                            // 对于未实例化的 Prefab，直接使用 AddComponent，不走 Undo
                            nb.gameObject.AddComponent<NetworkIdentity>();
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        EditorUtility.SetDirty(prefab);
                        PrefabUtility.SavePrefabAsset(prefab);
                        prefabCount++;
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"<color=#00FF00>【联机兼容性修复完毕】</color> 成功为场景中 {sceneCount} 个对象，以及库里 {prefabCount} 个 Prefab 补充了 NetworkIdentity！");
    }
}
