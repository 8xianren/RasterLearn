using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode] // 允许在编辑器模式下运行
public class CheckerboardGenerator : MonoBehaviour
{
    [Header("棋盘格设置")]
    [Range(32, 2048)] public int textureSize = 256;  // 纹理尺寸
    [Range(1, 64)] public int tileCount = 8;        // 棋盘格数量
    public Color colorA = Color.white;               // 颜色A
    public Color colorB = Color.gray;                // 颜色B
    
    [Header("纹理导出")]
    public string textureName = "CheckerboardTexture"; // 纹理名称
    public bool saveToAssetsTextures = true;        // 是否保存到Assets/Textures
    
    [HideInInspector] public Texture2D generatedTexture; // 生成的纹理
    
    // 当脚本被启用或值在检查器中改变时调用
    void OnEnable()
    {
        GenerateTexture();
    }
    
    void OnValidate()
    {
        GenerateTexture();
    }
    
    // 生成棋盘格纹理
    public void GenerateTexture()
    {
        if (tileCount < 1) tileCount = 1;
        
        generatedTexture = new Texture2D(textureSize, textureSize);
        int tileSize = Mathf.Max(1, textureSize / tileCount); // 确保至少1像素
        
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                int tileX = x / tileSize;
                int tileY = y / tileSize;
                
                // 根据位置确定使用哪种颜色
                Color color = ((tileX + tileY) % 2 == 0) ? colorA : colorB;
                generatedTexture.SetPixel(x, y, color);
            }
        }
        
        generatedTexture.Apply(); // 应用所有像素修改
        generatedTexture.filterMode = FilterMode.Point; // 确保棋盘格边缘清晰
    }
    
    // 保存生成的纹理
    public void SaveTexture()
    {
        if (generatedTexture == null)
        {
            Debug.LogWarning("请先生成纹理！");
            return;
        }
        
        // 确保有文件名
        if (string.IsNullOrEmpty(textureName))
            textureName = "CheckerboardTexture";
        
        // 目标目录处理
        string folderPath = saveToAssetsTextures ? "Assets/Textures" : "Assets";
        
        // 如果目录不存在则创建
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            System.IO.Directory.CreateDirectory(Application.dataPath + folderPath.Substring(6));
            AssetDatabase.Refresh();
        }
        
        // 保存纹理为PNG文件
        string fullPath = $"{folderPath}/{textureName}.png";
        System.IO.File.WriteAllBytes(Application.dataPath + fullPath.Substring(6), generatedTexture.EncodeToPNG());
        AssetDatabase.Refresh();
        
        // 导入设置
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(fullPath);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        
        Debug.Log($"棋盘格纹理已保存到: {fullPath}");
    }
    
    // 重置设置
    public void ResetSettings()
    {
        textureSize = 256;
        tileCount = 8;
        colorA = Color.white;
        colorB = Color.gray;
        textureName = "CheckerboardTexture";
        GenerateTexture();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(CheckerboardGenerator))]
public class CheckerboardGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        CheckerboardGenerator generator = (CheckerboardGenerator)target;
        
        EditorGUILayout.Space();
        
        // 预览纹理
        if (generator.generatedTexture != null)
        {
            GUILayout.Label("纹理预览:");
            Rect rect = GUILayoutUtility.GetRect(150, 150);
            EditorGUI.DrawPreviewTexture(rect, generator.generatedTexture);
        }
        
        EditorGUILayout.Space();
        
        // 功能按钮
        if (GUILayout.Button("生成纹理", GUILayout.Height(30)))
        {
            generator.GenerateTexture();
        }
        
        if (GUILayout.Button("保存纹理到Assets/Textures", GUILayout.Height(30)))
        {
            generator.saveToAssetsTextures = true;
            generator.SaveTexture();
        }
        
        if (GUILayout.Button("重置设置", GUILayout.Height(25)))
        {
            generator.ResetSettings();
        }
    }
}
#endif