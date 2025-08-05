using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class SoftRasterWireframe : MonoBehaviour
{
    public RawImage rawImage;
    public Camera sceneCamera;
    public GameObject cube;



    private Texture textureCube;

    private Texture noramlTexture;

    private Texture2D cubeTexture2D;

    private Texture2D cubeNoramlTexture2D;

    private Texture2D shadowTextureDirectLight;

    public int shadowMapResolution = 512;
    

    private float[] depthBufferShadow;



    private Material material;

    

    public  GameObject pointLight;

    public GameObject directLight;




    private Texture2D rasterTexture;

    private float[] depthBuffer;

    private Vector2[] uvCoords;
    private OriginalVertex[] myOriginalVertices;

    private VertexTest[] myVertexTests;

    private int[] myTriangles;

    private int textureWidth = 1920;
    private int textureHeight = 1080;
    private Matrix4x4 viewMatrix;
    private Matrix4x4 projectionMatrix;

    private Matrix4x4 lightViewMatrix;

    private Matrix4x4 lgithProjMatrix;
    Matrix4x4 normView3DScreen = Matrix4x4.identity;

    public bool isWireframe = true;
    
    public bool isLight = false;
    
    public bool textureOn = false;

    private void Start()
    {

        rasterTexture = new Texture2D(textureWidth, textureHeight);
        rasterTexture.wrapMode = TextureWrapMode.Clamp;
        rawImage.texture = rasterTexture;

        material = cube.GetComponent<MeshRenderer>().material;
        textureCube = material.mainTexture;
        cubeTexture2D = textureCube as Texture2D;

        noramlTexture = material.GetTexture("_BumpMap");

        cubeNoramlTexture2D = noramlTexture as Texture2D;

        
        
        

        depthBuffer = new float[textureWidth * textureHeight];
        for (int i = 0; i < depthBuffer.Length; i++)
        {
            depthBuffer[i] = float.MaxValue;
        }

        InitializeCube();

        
        UpdateCameraMatrices();
    }

    private void InitializeCube()
    {
        MeshFilter meshFilter = cube.GetComponent<MeshFilter>();


        Mesh mesh = meshFilter.mesh;
        
        uvCoords = new Vector2[mesh.vertexCount];

        myOriginalVertices = new OriginalVertex[mesh.vertexCount];
        myVertexTests = new VertexTest[mesh.vertexCount];
        for (int i = 0; i < mesh.vertexCount; ++i)
        {
            Vector3 pos = mesh.vertices[i];
            Vector3 normal = mesh.normals[i];
            Color color = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, 1.0f); // 随机颜色
            myOriginalVertices[i] = new OriginalVertex(pos, normal, color);

            uvCoords[i] = mesh.uv[i];
            
        }

        
        myTriangles = mesh.triangles;

        for (int i = 0; i < mesh.vertexCount; ++i)
        {
            Debug.Log("Vertex " + i + ": " + myOriginalVertices[i].positon + ", Normal: " + myOriginalVertices[i].noraml );
        }

        for (int i = 0; i < myTriangles.Length; ++i)
        {
            Debug.Log("Triangle " + i + ": " + myTriangles[i]);
        }
        
        
    }

    private void CalculateViewCoordinates()
    {
        //
        Vector3 camForward = sceneCamera.transform.forward;
        Vector3 camUp = sceneCamera.transform.up;
        Vector3 camPos = sceneCamera.transform.position;

        Vector3 u = Vector3.Cross(camUp, camForward).normalized;
        Vector3 v = Vector3.Cross( camForward, u).normalized;

        
        viewMatrix.SetRow(0, new Vector4(u.x, u.y, u.z, -Vector3.Dot(u, camPos)));
        viewMatrix.SetRow(1, new Vector4(v.x, v.y, v.z, -Vector3.Dot(v, camPos)));
        viewMatrix.SetRow(2, new Vector4(camForward.x, camForward.y, camForward.z, -Vector3.Dot(camForward, camPos)));
        viewMatrix.SetRow(3, new Vector4(0, 0, 0, 1));

        

    }

    private void CalculateProjectionCoordinates()
    {
        //FOV axis Vertical default; projectionPoint is at the 0 0 0 in View Coordinates
        float near = sceneCamera.nearClipPlane;
        float far = sceneCamera.farClipPlane;
        float fov = sceneCamera.fieldOfView;
        float aspect = sceneCamera.aspect;
        
        projectionMatrix.SetRow(0, new Vector4(1 / (aspect * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad)), 0, 0, 0));
        projectionMatrix.SetRow(1, new Vector4(0, 1 / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad), 0, 0));
        projectionMatrix.SetRow(2, new Vector4(0, 0, far  / (far - near), far * near / (near - far)));
        projectionMatrix.SetRow(3, new Vector4(0, 0, 1, 0));

        
    }

    private void UpdateCameraMatrices()
    {
        CalculateViewCoordinates();

        CalculateProjectionCoordinates();

        
    
    }

    private Vector3 Calculate3DScreenCoordinates(Vector4 normalizedDeviceCoordinates)
    {

        normView3DScreen.SetRow(0, new Vector4(textureWidth / 2, 0, 0, textureWidth / 2));
        normView3DScreen.SetRow(1, new Vector4(0, textureHeight / 2, 0, textureHeight / 2));
        normView3DScreen.SetRow(2, new Vector4(0, 0, 1, 0));
        normView3DScreen.SetRow(3, new Vector4(0, 0, 0, 1));

        Vector4 screenCoordinates = normView3DScreen * normalizedDeviceCoordinates;
        return screenCoordinates;
    }

    private void Update()
    {
        // 每帧更新相机矩阵
        UpdateCameraMatrices();

        
        ClearTexture(Color.white);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            isWireframe = !isWireframe;
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            textureOn = !textureOn;
            
        }

        if (isWireframe)
        {
            CreateWireFrame();

        }
        else 
        {

            CreateShadow();
            CreateSimpleFillColor();
            
            
        }

        
    }


    void CreateShadow()
    {
        CalLightViewMatrix(directLight);
        //List<Vector3> corners = new List<Vector3>();
        List<Vector3> corners = CalCameraBounds();
        List<Vector3> cornersInLightView = new List<Vector3>();
        cornersInLightView.Capacity = corners.Count;

        Vector3 minXYZ = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 maxXYZ = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < corners.Count; ++i)
        {
            cornersInLightView.Add( lightViewMatrix.MultiplyPoint(corners[i]) ); 
            //Debug.Log(cornersInLightView[i]);
            minXYZ = Vector3.Min(cornersInLightView[i], minXYZ);
            maxXYZ = Vector3.Max(cornersInLightView[i], maxXYZ);

        }




    }


    private void CalLightViewMatrix(GameObject direLight )
    {
        //
        Vector3 lightForward = direLight.transform.forward;
        Vector3 lightUp = direLight.transform.up;
        Vector3 lightPos = direLight.transform.position;

        Vector3 u = Vector3.Cross(lightUp, lightForward).normalized;
        Vector3 v = Vector3.Cross( lightForward, u).normalized;

        
        lightViewMatrix.SetRow(0, new Vector4(u.x, u.y, u.z, -Vector3.Dot(u, lightPos)));
        lightViewMatrix.SetRow(1, new Vector4(v.x, v.y, v.z, -Vector3.Dot(v, lightPos)));
        lightViewMatrix.SetRow(2, new Vector4(lightForward.x, lightForward.y, lightForward.z, -Vector3.Dot(lightForward, lightPos)));
        lightViewMatrix.SetRow(3, new Vector4(0, 0, 0, 1));

        

    }

    private List<Vector3> CalCameraBounds()
    {
        float near = sceneCamera.nearClipPlane;
        float far = sceneCamera.farClipPlane;
        float fov = sceneCamera.fieldOfView;
        float aspect = sceneCamera.aspect;

        int[] dx = { 1, -1 , -1 , 1};
        int[] dy = { 1, 1, -1, -1};

        float halfHeightNear = near * Mathf.Tan(fov/2 * Mathf.Deg2Rad);
        float halfWidthNear = halfHeightNear * aspect;
        float halfHeightFar = far * Mathf.Tan(fov/2 * Mathf.Deg2Rad);
        float halfWidthFar = halfHeightFar * aspect;

        List<Vector3> resCorners = new List<Vector3>();
        for (int i = 0; i < 4; ++i)
        {
            
            resCorners.Add(new Vector3(halfWidthNear * dx[i], halfHeightNear * dy[i], near));
            resCorners.Add(new Vector3( halfWidthFar*dx[i], halfHeightFar*dy[i] , far) );
        }
        return resCorners;
    }


    
    
    private void CreateSimpleFillColor()
    {
        for (int i = 0; i < depthBuffer.Length; ++i)
        {
            depthBuffer[i] = float.MaxValue;
        }

        Matrix4x4 mvpMatrix = projectionMatrix * viewMatrix * cube.transform.localToWorldMatrix;

        List<Vector4> clipSpaceVertices = new List<Vector4>();
        foreach (OriginalVertex originalVertex in myOriginalVertices)
        {


            Vector4 transformed = mvpMatrix * new Vector4(originalVertex.positon.x, originalVertex.positon.y, originalVertex.positon.z, 1.0f);
            clipSpaceVertices.Add(transformed);
        }

        List<Vector4> screenVertices = new List<Vector4>();
        foreach (Vector4 clipPos in clipSpaceVertices)
        {
            float realDepth = clipPos.w;
            Vector3 clipPos3D = new Vector3(clipPos.x / clipPos.w, clipPos.y / clipPos.w, clipPos.z / clipPos.w);
            Vector4 scrPos = Calculate3DScreenCoordinates(new Vector4(clipPos3D.x, clipPos3D.y, clipPos3D.z, 1.0f));
            screenVertices.Add(new Vector4(scrPos.x, scrPos.y, scrPos.z, 1.0f / realDepth));
        }

        for (int i = 0; i < myVertexTests.Length; ++i)
        {
            /*
            Vector4 vetexTemp = new Vector4(myOriginalVertices[i].positon.x, myOriginalVertices[i].positon.y, myOriginalVertices[i].positon.z, 1.0f);
            Vector4 normalTemp = new Vector4(myOriginalVertices[i].noraml.x, myOriginalVertices[i].noraml.y, myOriginalVertices[i].noraml.z, 1.0f);
            */
            myVertexTests[i] = new VertexTest();
            myVertexTests[i].worldPos = cube.transform.localToWorldMatrix.MultiplyPoint(myOriginalVertices[i].positon);
            myVertexTests[i].worldNormal = cube.transform.localToWorldMatrix.MultiplyVector(myOriginalVertices[i].noraml);



            myVertexTests[i].viewPos = viewMatrix.MultiplyPoint(myVertexTests[i].worldPos);
            myVertexTests[i].viewNoraml = viewMatrix.MultiplyVector(myVertexTests[i].worldNormal);


            myVertexTests[i].screenPos = new Vector3(screenVertices[i].x, screenVertices[i].y, screenVertices[i].z);
            myVertexTests[i].perspectFactor = screenVertices[i].w;
        }


        for (int i = 0; i < myTriangles.Length; i += 3)
        {
            rasterTriangles(i, ref myVertexTests);
        }

        rasterTexture.Apply();
    }

    private void rasterTriangles(int index,ref VertexTest[] vertexInfos)
    {
        int v0 = myTriangles[index];
        int v1 = myTriangles[index + 1];
        int v2 = myTriangles[index + 2];

        Vector3 p0 = vertexInfos[v0].screenPos;
        Vector3 p1 = vertexInfos[v1].screenPos;
        Vector3 p2 = vertexInfos[v2].screenPos;

        float minX = Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x));
        float maxX = Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x));
        float minY = Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y));
        float maxY = Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y));

        int startX = Mathf.Max(0, Mathf.FloorToInt(minX));
        int endX = Mathf.Min(textureWidth - 1, Mathf.CeilToInt(maxX));
        int startY = Mathf.Max(0, Mathf.FloorToInt(minY));
        int endY = Mathf.Min(textureHeight - 1, Mathf.CeilToInt(maxY));

        for (int i = startX; i <= endX; ++i)
        {
            for (int j = startY; j <= endY; ++j)
            {
                Vector3 currentPixel = new Vector3(i + 0.5f, j + 0.5f, 0.0f);

                float area = EdgeFuction(p0, p1, p2);

                if (area <= 1e-5f)
                {
                    continue;
                }

                float w0 = EdgeFuction(p1, p2, currentPixel);

                float w1 = EdgeFuction(p2, p0, currentPixel);

                float w2 = EdgeFuction(p0, p1, currentPixel);

                if ((w0 >= 0 && w1 >= 0 && w2 >= 0) )
                {
                    float alpha = w0 / area;

                    float beta = w1 / area;

                    float gamma = w2 / area;

                    float correcW = alpha * vertexInfos[v0].perspectFactor + beta * vertexInfos[v1].perspectFactor + gamma * vertexInfos[v2].perspectFactor;

                    float w = 1.0f / correcW;
                    
                    Color calculatedColor;

                    if (textureOn)
                    {
                        Vector2 uvInterp = w * (alpha * uvCoords[v0] * vertexInfos[v0].perspectFactor + beta * uvCoords[v1] * vertexInfos[v1].perspectFactor + gamma * uvCoords[v2] * vertexInfos[v2].perspectFactor);

                        calculatedColor = pixelColor(uvInterp);

                        if (isLight)
                        {

                            float px = alpha* vertexInfos[v0].viewPos.x* vertexInfos[v0].perspectFactor + beta * vertexInfos[v1].viewPos.x * vertexInfos[v1].perspectFactor+ gamma * vertexInfos[v2].viewPos.x* vertexInfos[v2].perspectFactor;
                            float py = alpha * vertexInfos[v0].viewPos.y * vertexInfos[v0].perspectFactor + beta * vertexInfos[v1].viewPos.y * vertexInfos[v1].perspectFactor + gamma * vertexInfos[v2].viewPos.y * vertexInfos[v2].perspectFactor;
                            Vector3 viewPos = new Vector3(px, py, w);

                            Color noramlColor = cubeNoramlTexture2D.GetPixelBilinear(uvInterp.x, uvInterp.y);
                            Vector3 noramlInterp = UnpackNoraml(noramlColor); 
                            Color lightColorTemp = CalculateLightColor(noramlInterp, calculatedColor, viewPos, material);
                            calculatedColor = lightColorTemp;
                        }
                    }
                    else
                    {
                        calculatedColor = alpha * myOriginalVertices[v0].color + beta * myOriginalVertices[v1].color + gamma * myOriginalVertices[v2].color;

                        calculatedColor.a = 1.0f;
                    }

                    /*

                            Vector3 viewPos = w * (alpha * vertexInfos[v0].viewPos + beta * vertexInfos[v1].viewPos + gamma * vertexInfos[v2].viewPos);

                            Vector3 viewNormal = w * (alpha * vertexInfos[v0].viewNoraml + beta * vertexInfos[v1].viewNoraml + gamma * vertexInfos[v2].viewNoraml).normalized;

                            */

                    

                    float depth = alpha * vertexInfos[v0].screenPos.z + beta * vertexInfos[v1].screenPos.z + gamma * vertexInfos[v2].screenPos.z;

                    int idx = j * textureWidth + i;

                    if (idx > depthBuffer.Length - 1 || idx < 0)
                    {
                        Debug.LogError("Index out of bounds: " + idx + ", DepthBuffer Length: " + depthBuffer.Length);
                        continue;
                    }

                    if (depth < depthBuffer[idx])
                        {

                            depthBuffer[idx] = depth;


                            rasterTexture.SetPixel(i, j, calculatedColor);



                        }



                    
                }
            }
        }



        
    }
    

    private Vector3 UnpackNoraml(Color noramlColor)
    {
        // 假设法线贴图是以 RGB 格式存储的
        // 将颜色值转换为法线向量
        Vector3 normal = new Vector3(noramlColor.r * 2.0f - 1.0f, noramlColor.g * 2.0f - 1.0f, noramlColor.b * 2.0f - 1.0f);
        return normal.normalized; // 确保法线向量是单位向量
    }

    private Color CalculateLightColor(Vector3 noraml, Color calColor, Vector3 viewPos, Material material)
    {


        return Color.white;
    }

    private Color pixelColor(Vector2 uv)
    {
        Color pixel = cubeTexture2D.GetPixelBilinear(uv.x, uv.y);
        return pixel;

    }

    private float EdgeFuction(Vector3 A, Vector3 B, Vector3 P)
    {
        return (P.x - A.x) * (B.y - A.y) - (P.y - A.y) * (B.x - A.x);
    }
    
    // private Matrix4x4 CalculateLightVP(Camera mainCamera, Light directionalLight, out Bounds lightBounds)
    // {
    //     Vector3 lightDir = directionalLight.transform.forward;

        

    //     // 对于远平面，我们需要从摄像机设置获取
    //     float shadowFarDistance = QualitySettings.shadowDistance;
    
    //     // 使用摄像机位置作为基础计算光源位置
    //     Vector3 lightPosition = mainCamera.transform.position - lightDir * shadowFarDistance;
        
    //     // 创建光源视图矩阵
    //     Matrix4x4 lightView = Matrix4x4.LookAt(
    //         lightPosition,
    //         lightPosition + lightDir,
    //         Vector3.up
    //     );
    
    //     // 获取摄像机视锥体角点
    //     Vector3[] frustumCorners = new Vector3[8];
    //     mainCamera.CalculateFrustumCorners(
    //         new Rect(0, 0, 1, 1),
    //         shadowFarDistance,
    //         Camera.MonoOrStereoscopicEye.Mono,
    //         frustumCorners
    //     );

    //     // 计算光源空间包围盒
    //     Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
    //     Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        
    //     for (int i = 0; i < frustumCorners.Length; i++)
    //     {
    //         Vector3 worldCorner = mainCamera.transform.TransformPoint(frustumCorners[i]);
    //         Vector3 lightSpaceCorner = lightView.MultiplyPoint(worldCorner);
            
    //         min = Vector3.Min(min, lightSpaceCorner);
    //         max = Vector3.Max(max, lightSpaceCorner);
    //     }
        
    //     // 设置包围盒
    //     lightBounds = new Bounds();
    //     lightBounds.SetMinMax(min, max);
        
    //     // 扩展10%避免裁剪问题
    //     lightBounds.Expand(lightBounds.size * 0.1f);
        
    //     // 创建正交投影矩阵
    //     float left = lightBounds.min.x;
    //     float right = lightBounds.max.x;
    //     float bottom = lightBounds.min.y;
    //     float top = lightBounds.max.y;
    //     float near = Mathf.Max(directionalLight.shadowNearPlane, 0.01f); // 确保不为零
    //     float far = lightBounds.max.z - lightBounds.min.z + near;
        
    //     Matrix4x4 lightProjection = Matrix4x4.Ortho(left, right, bottom, top, near, far);
        
    //     // 返回视图投影矩阵 (Projection * View)
    //     return lightProjection * lightView;
    // }

    private void CreateWireFrame()
    {
        // 清空纹理
        Matrix4x4 modelMatrix = cube.transform.localToWorldMatrix;

        // 完整 MVP 矩阵
        Matrix4x4 mvpMatrix = projectionMatrix * viewMatrix * modelMatrix;

        // 转换所有顶点
        List<Vector4> clipSpaceVertices = new List<Vector4>();
        foreach (OriginalVertex originalVertex in myOriginalVertices)
        {
            Vector3 vertex = originalVertex.positon;

            Vector4 transformed = mvpMatrix * new Vector4(vertex.x, vertex.y, vertex.z, 1.0f);
            clipSpaceVertices.Add(transformed);
        }

        // 将裁剪空间坐标转换为屏幕坐标
        List<Vector2> screenVertices = new List<Vector2>();
        foreach (Vector4 clipPos in clipSpaceVertices)
        {
            Vector3 clipPos3D = new Vector3(clipPos.x / clipPos.w, clipPos.y / clipPos.w, clipPos.z / clipPos.w);

            Vector4 ndcPos = Calculate3DScreenCoordinates(new Vector4(clipPos3D.x, clipPos3D.y, clipPos3D.z, 1.0f));

            screenVertices.Add(new Vector2(ndcPos.x, ndcPos.y));
        }

        for (int i = 0; i < myTriangles.Length; i += 3)
        {
            int v0 = myTriangles[i];
            int v1 = myTriangles[i + 1];
            int v2 = myTriangles[i + 2];

            Vector2 p0 = screenVertices[v0];
            Vector2 p1 = screenVertices[v1];
            Vector2 p2 = screenVertices[v2];

            // 绘制三角形的边
            DrawLine(p0, p1, (myOriginalVertices[v0].color + myOriginalVertices[v1].color) / 2);
            DrawLine(p1, p2, (myOriginalVertices[v1].color + myOriginalVertices[v2].color) / 2);
            DrawLine(p2, p0, (myOriginalVertices[v2].color + myOriginalVertices[v0].color) / 2);
        }

        // 应用纹理更改
        rasterTexture.Apply();
    }

    private void ClearTexture(Color color)
    {
        Color[] clearPixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < clearPixels.Length; i++)
        {
            clearPixels[i] = color;
        }
        rasterTexture.SetPixels(clearPixels);
    }

    private void DrawLine(Vector2 start, Vector2 end, Color color)
    {
        // 使用 Bresenham 算法画线
        int x0 = Mathf.RoundToInt(start.x);
        int y0 = Mathf.RoundToInt(start.y);
        int x1 = Mathf.RoundToInt(end.x);
        int y1 = Mathf.RoundToInt(end.y);

        bool steep = Mathf.Abs(y1 - y0) > Mathf.Abs(x1 - x0);

        if (steep)
        {
            Swap(ref x0, ref y0);
            Swap(ref x1, ref y1);
        }

        if (x0 > x1)
        {
            Swap(ref x0, ref x1);
            Swap(ref y0, ref y1);
        }

        int dx = x1 - x0;
        int dy = Mathf.Abs(y1 - y0);

        int error = dx / 2;
        int ystep = (y0 < y1) ? 1 : -1;
        int y = y0;

        for (int x = x0; x <= x1; x++)
        {
            int px = steep ? y : x;
            int py = steep ? x : y;

            if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
            {
                rasterTexture.SetPixel(px, py, color);
            }

            error -= dy;
            if (error < 0)
            {
                y += ystep;
                error += dx;
            }
        }
    }

    private void Swap<T>(ref T a, ref T b)
    {
        T temp = a;
        a = b;
        b = temp;
    }
}

public struct LineEdge
{
    public int startIndex;
    public int endIndex;

    public LineEdge(int start, int end)
    {
        startIndex = start;
        endIndex = end;
    }
}

public struct OriginalVertex
{
    public Vector3 positon;

    public Vector3 noraml;

    public Color color;

    public OriginalVertex(Vector3 _pos, Vector3 _normal, Color _color)
    {
        positon = _pos;
        noraml = _normal;
        this.color = _color;
    }
}

public class VertexTest
{
    public Vector3 worldPos;
    public Vector3 worldNormal;
    public Vector3 viewPos;
    public Vector3 viewNoraml;

    public Vector3 screenPos;

    public float perspectFactor;
    

}

public class Triangle
{
    // anticlockwise
    public int v0;
    public int v1;
    public int v2;

    public Triangle(int _v0, int _v1, int _v2)
    {
        v0 = _v0;
        v1 = _v1;
        v2 = _v2;


    }
}


