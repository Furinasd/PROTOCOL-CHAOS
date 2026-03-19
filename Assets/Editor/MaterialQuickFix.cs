using UnityEngine;
using UnityEditor;
using System.IO;

public class MaterialQuickFix : EditorWindow
{
    [MenuItem("Tools/Fix Enemy Materials")]
    public static void FixMaterials()
    {
        string folderPath = "Assets/Materials/Enemy";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            AssetDatabase.CreateFolder("Assets/Materials", "Enemy");
        }

        // 1. 创建或获取材质
        Material neutral = CreateURPMaterial(folderPath, "Enemy_Neutral", Color.gray, false);
        Material blueGlow = CreateURPMaterial(folderPath, "Enemy_BlueGlow", Color.blue, true);
        Material redGlow = CreateURPMaterial(folderPath, "Enemy_RedGlow", Color.red, true);
        Material stunned = CreateURPMaterial(folderPath, "Enemy_Stunned", Color.black, false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 2. 查找场景中的 Enemy 并赋值
        EnemyVisualController controller = FindFirstObjectByType<EnemyVisualController>();
        if (controller != null)
        {
            Undo.RecordObject(controller, "Fix Enemy Materials");
            controller.neutralMat = neutral;
            controller.blueGlowMat = blueGlow;
            controller.redGlowMat = redGlow;
            controller.stunnedMat = stunned;

            // 同时也给 MeshRenderer 赋个初始值，防止粉色
            MeshRenderer mr = controller.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Undo.RecordObject(mr, "Fix Enemy MeshRenderer");
                mr.sharedMaterial = neutral;
            }

            EditorUtility.SetDirty(controller);
            Debug.Log("[MaterialQuickFix] 成功配置了 EnemyVisualController 的所有材质！");
        }
        else
        {
            Debug.LogWarning("[MaterialQuickFix] 未在场景中找到 EnemyVisualController。请确保场景已打开且包含敌方物体。");
        }
    }

    private static Material CreateURPMaterial(string folder, string name, Color color, bool isEmission)
    {
        string path = $"{folder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null) urpShader = Shader.Find("Standard"); // 退回到标准 Shader

            mat = new Material(urpShader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.color = color;

        if (isEmission)
        {
            mat.EnableKeyword("_EMISSION");
            // 设置 HDR 颜色，增强发光感
            mat.SetColor("_EmissionColor", color * 2.5f); 
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }
}
