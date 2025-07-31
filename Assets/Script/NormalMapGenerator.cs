using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum NormalMapSourceType
{
    HeightMap,
    SimplePattern,
    PerlinNoise,
    VoronoiPattern,
    CustomTexture
}

public enum NormalMapIntensityMode
{
    Standard,
    Enhanced,
    Subtle
}

[ExecuteInEditMode]
public class NormalMapGenerator : MonoBehaviour
{
    [Header("法线贴图设置")]
    public NormalMapSourceType sourceType = NormalMapSourceType.PerlinNoise;
    [Range(64, 2048)] public int textureSize = 512;
    public NormalMapIntensityMode intensityMode = NormalMapIntensityMode.Standard;
    [Range(0.1f, 5f)] public float strength = 1.0f;
    public Color highlightColor = Color.cyan;
    public Color shadowColor = Color.magenta;
    public bool invertNormals = false;
    
    [Header("高度图设置（可选）")]
    public Texture2D heightMapTexture;
    
    [Header("自定义纹理（可选）")]
    public Texture2D customSourceTexture;
    
    [Header("噪波设置")]
    [Range(0.1f, 10f)] public float noiseScale = 5.0f;
    [Range(1, 8)] public int octaves = 3;
    [Range(0f, 1f)] public float persistence = 0.5f;
    [Range(1f, 5f)] public float lacunarity = 2.0f;
    
    [Header("形状设置")]
    [Range(1, 20)] public int voronoiCells = 5;
    [Range(0f, 1f)] public float voronoiBlend = 0.5f;
    
    [Header("导出选项")]
    public string textureName = "GeneratedNormalMap";
    public bool saveToAssetsTextures = true;
    
    [Header("预览")]
    [Range(0.1f, 3f)] public float previewIntensity = 1f;
    public Texture2D generatedNormalMap;
    
    // 内部变量
    public Material previewMaterial;
    private Vector2 noiseOffset = Vector2.zero;

    void OnEnable()
    {
        if (Application.isPlaying) return;
        InitializePreviewMaterial();
        GenerateNormalMap();
    }
    
    void InitializePreviewMaterial()
    {
        if (previewMaterial == null)
        {
            Shader previewShader = Shader.Find("Unlit/preview");
            
            previewMaterial = new Material(previewShader);
        }
    }
    
    public void GenerateNormalMap()
    {
        if (textureSize < 64) textureSize = 64;
        
        Texture2D sourceTexture = null;
        
        // 根据来源类型创建源纹理
        switch (sourceType)
        {
            case NormalMapSourceType.HeightMap:
                if (heightMapTexture != null) 
                    sourceTexture = heightMapTexture;
                else
                    sourceTexture = CreateHeightMap();
                break;
                
            case NormalMapSourceType.SimplePattern:
                sourceTexture = CreatePatternTexture();
                break;
                
            case NormalMapSourceType.PerlinNoise:
                sourceTexture = CreatePerlinNoise();
                break;
                
            case NormalMapSourceType.VoronoiPattern:
                sourceTexture = CreateVoronoiTexture();
                break;
                
            case NormalMapSourceType.CustomTexture:
                if (customSourceTexture != null) 
                    sourceTexture = customSourceTexture;
                else
                    Debug.LogWarning("自定义纹理未设置，使用默认噪波");
                    sourceTexture = CreatePerlinNoise();
                break;
        }
        
        if (sourceTexture == null)
        {
            Debug.LogError("无法生成源纹理");
            return;
        }
        
        if (sourceTexture.width != textureSize || sourceTexture.height != textureSize)
        {
            sourceTexture = ResizeTexture(sourceTexture, textureSize, textureSize);
        }
        
        // 生成法线贴图
        generatedNormalMap = GenerateNormalMapFromHeight(sourceTexture);
        
        if (generatedNormalMap != null && previewMaterial != null)
        {
            previewMaterial.SetTexture("_NormalMap", generatedNormalMap);
            previewMaterial.SetFloat("_Intensity", previewIntensity);
        }
    }
    
    Texture2D CreatePatternTexture()
    {
        Texture2D tex = new Texture2D(textureSize, textureSize);
        
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float value = Mathf.Sin(x * 0.1f) * Mathf.Cos(y * 0.1f);
                tex.SetPixel(x, y, new Color(value, value, value));
            }
        }
        
        tex.Apply();
        return tex;
    }
    
    Texture2D CreatePerlinNoise()
    {
        noiseOffset = new Vector2(Random.Range(0f, 100f), Random.Range(0f, 100f));
        Texture2D tex = new Texture2D(textureSize, textureSize);
        
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float value = GeneratePerlinNoise(x, y);
                tex.SetPixel(x, y, new Color(value, value, value));
            }
        }
        
        tex.Apply();
        return tex;
    }
    
    float GeneratePerlinNoise(int x, int y)
    {
        float amplitude = 1;
        float frequency = 1;
        float noiseHeight = 0;
        
        for (int i = 0; i < octaves; i++)
        {
            float sampleX = x / (noiseScale * frequency) + noiseOffset.x;
            float sampleY = y / (noiseScale * frequency) + noiseOffset.y;
            
            float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
            noiseHeight += perlinValue * amplitude;
            
            amplitude *= persistence;
            frequency *= lacunarity;
        }
        
        return Mathf.Clamp01(noiseHeight);
    }
    
    Texture2D CreateVoronoiTexture()
    {
        Texture2D tex = new Texture2D(textureSize, textureSize);
        Vector2[] points = new Vector2[voronoiCells];
        
        // 生成随机点
        for (int i = 0; i < voronoiCells; i++)
        {
            points[i] = new Vector2(
                Random.Range(0f, textureSize),
                Random.Range(0f, textureSize)
            );
        }
        
        // 填充纹理
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                // 计算最近的点的距离
                Vector2 point = new Vector2(x, y);
                float minDist = float.MaxValue;
                
                for (int i = 0; i < voronoiCells; i++)
                {
                    float dist = Vector2.Distance(point, points[i]);
                    minDist = Mathf.Min(minDist, dist);
                }
                
                float value = Mathf.Clamp01(minDist / textureSize * 5f);
                tex.SetPixel(x, y, new Color(value, value, value));
            }
        }
        
        tex.Apply();
        return tex;
    }
    
    Texture2D CreateHeightMap()
    {
        // 创建简单的高度图
        Texture2D tex = new Texture2D(textureSize, textureSize);
        
        int halfSize = textureSize / 2;
        
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distToCenter = Vector2.Distance(new Vector2(x, y), new Vector2(halfSize, halfSize));
                float value = 1f - Mathf.Clamp01(distToCenter / halfSize);
                
                // 添加一些噪波使表面更自然
                float noise = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.1f;
                value = Mathf.Clamp01(value + noise);
                
                tex.SetPixel(x, y, new Color(value, value, value));
            }
        }
        
        tex.Apply();
        return tex;
    }
    
    Texture2D ResizeTexture(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);
        
        Texture2D result = new Texture2D(width, height);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();
        
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }
    
    Texture2D GenerateNormalMapFromHeight(Texture2D heightMap)
    {
        Texture2D normalMap = new Texture2D(heightMap.width, heightMap.height, TextureFormat.RGB24, false);
        
        float intensity;
        switch (intensityMode)
        {
            case NormalMapIntensityMode.Enhanced:
                intensity = strength * 2.0f;
                break;
            case NormalMapIntensityMode.Subtle:
                intensity = strength * 0.5f;
                break;
            default:
                intensity = strength;
                break;
        }
        
        for (int y = 0; y < heightMap.height; y++)
        {
            for (int x = 0; x < heightMap.width; x++)
            {
                // 获取相邻像素的高度值
                float left = heightMap.GetPixel(Mathf.Max(0, x - 1), y).grayscale;
                float right = heightMap.GetPixel(Mathf.Min(heightMap.width - 1, x + 1), y).grayscale;
                float bottom = heightMap.GetPixel(x, Mathf.Max(0, y - 1)).grayscale;
                float top = heightMap.GetPixel(x, Mathf.Min(heightMap.height - 1, y + 1)).grayscale;
                
                // 计算法线向量
                Vector3 normal = new Vector3(
                    (left - right) * intensity, 
                    (bottom - top) * intensity, 
                    1.0f
                ).normalized;
                
                // 转换为颜色值 (0-1)
                Color normalColor = new Color(
                    normal.x * 0.5f + 0.5f,
                    normal.y * 0.5f + 0.5f,
                    normal.z * 0.5f + 0.5f
                );
                
                normalMap.SetPixel(x, y, invertNormals ? (Color.white - normalColor) : normalColor);
            }
        }
        
        normalMap.Apply();
        return normalMap;
    }
    
    public void SaveNormalMap()
    {
        if (generatedNormalMap == null)
        {
            Debug.LogWarning("没有可保存的法线贴图，请先生成");
            GenerateNormalMap();
        }
        
        if (string.IsNullOrEmpty(textureName))
            textureName = "GeneratedNormalMap";
        
        string folderPath = saveToAssetsTextures ? "Assets/Textures" : "Assets";
        
        #if UNITY_EDITOR
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            System.IO.Directory.CreateDirectory($"{Application.dataPath}/Textures");
            AssetDatabase.Refresh();
        }
        
        //string fullPath = $"{folderPath}/{textureName}.png";
        string fullSavePath = Application.dataPath + "/Textures/GeneratedNormalMap.png";
        Debug.Log($"保存法线贴图到: {fullSavePath}");
        System.IO.File.WriteAllBytes(fullSavePath, 
                                    generatedNormalMap.EncodeToPNG());
        
        AssetDatabase.Refresh();
        
        // 设置导入选项
        string assetPath = AssetDatabase.GetAssetPath(generatedNormalMap);
        if (string.IsNullOrEmpty(assetPath)) // 如果贴图还没在资源中
        {
            assetPath = fullSavePath;
        }
        
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
        
        Debug.Log($"法线贴图已保存到: {fullSavePath}");
        #else
        Debug.Log("保存功能仅在编辑器模式下可用");
        #endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(NormalMapGenerator))]
public class NormalMapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        NormalMapGenerator generator = (NormalMapGenerator)target;
        
        EditorGUILayout.Space();
        
        if (generator.generatedNormalMap != null)
        {
            GUILayout.Label("法线贴图预览:");
            Rect rect = GUILayoutUtility.GetRect(256, 256);
            EditorGUI.DrawPreviewTexture(rect, generator.generatedNormalMap, generator.previewMaterial);
        }
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("生成法线贴图", GUILayout.Height(30)))
        {
            generator.GenerateNormalMap();
        }
        
        if (GUILayout.Button("随机噪波", GUILayout.Height(25)))
        {
            generator.GenerateNormalMap();
        }
        
        if (GUILayout.Button("保存到Assets/Textures", GUILayout.Height(30)))
        {
            generator.saveToAssetsTextures = true;
            generator.SaveNormalMap();
        }
        
        if (generator.sourceType == NormalMapSourceType.CustomTexture || 
            generator.sourceType == NormalMapSourceType.HeightMap)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("使用自定义纹理时请确保它们已导入Unity中", MessageType.Info);
        }
    }
}
#endif