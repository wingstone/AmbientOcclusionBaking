using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections;
using System.IO;
using System.Collections.Generic;


public struct Point
{
    public Point(Vector3 p, Vector3 normal, Vector2 uv)
    {
        this.p = p;
        this.n = normal;
        this.uv = uv;
    }
    public Vector3 p;
    public Vector2 uv;
    public Vector3 n;
}

public struct Triangle
{
    public Triangle(Point p0, Point p1, Point p2)
    {
        this.p0 = p0;
        this.p1 = p1;
        this.p2 = p2;
    }
    public Point p0, p1, p2;
}

public class AOBakeWindow : EditorWindow
{
    int resolution = 1024;
    int rayCount = 128;
    float range = 1.5f;
    bool useBlur = true;
    bool enablePBR = false;

    Texture2D[] aoTex = null;
    int aoIndex = -1;
    List<MeshCollider> meshColliders = null;

    [MenuItem("Window/AOBakeWindow")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        AOBakeWindow window = (AOBakeWindow)EditorWindow.GetWindow(typeof(AOBakeWindow));
        window.Show();
    }
    private bool Get2Flag(int num)
    {
        if (num < 1) return false;
        return (num & num - 1) == 0;
    }

    void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        GUILayout.Label("Base Settings", EditorStyles.boldLabel);

        resolution = EditorGUILayout.IntSlider("AO Resolution", resolution, 128, 1024);
        while (!Get2Flag(resolution))
        {
            resolution--;
        }
        rayCount = EditorGUILayout.IntSlider("Ray Count", rayCount, 0, 1024);
        range = EditorGUILayout.Slider("Range", range, 0, 3);
        useBlur = EditorGUILayout.Toggle("Use Blur", useBlur);
        enablePBR = EditorGUILayout.Toggle("Enable PBR", enablePBR);

        if (GUILayout.Button("Bake Start"))
        {
            LightmapData[] lightmapDatas = LightmapSettings.lightmaps;
            if (lightmapDatas == null || lightmapDatas.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "Must bake lightmap first!", "OK");
                return;
            }
            AddMeshCollider();
            StartBake();
            RemoveMeshCollider();
            SetAOTextureImporter();
        }
        if (GUILayout.Button("Preview AO"))
        {
            PreviewAO();
        }
        EditorGUILayout.EndVertical();
    }

    void SetAOTextureImporter()
    {
        string path = SceneManager.GetActiveScene().path;
        path = Path.GetDirectoryName(path);
        string name = SceneManager.GetActiveScene().name;
        path = Path.Combine(path, name);

        LightmapData[] lightmapDatas = LightmapSettings.lightmaps;
        for (int i = 0; i < lightmapDatas.Length; i++)
        {
            string aoPath = path + "/AmbientOcclusion_" + i + ".tga";
            TextureImporter importer = AssetImporter.GetAtPath(aoPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }
    }

    void PreviewAO()
    {
        string path = SceneManager.GetActiveScene().path;
        path = Path.GetDirectoryName(path);
        string name = SceneManager.GetActiveScene().name;
        path = Path.Combine(path, name);

        LightmapData[] lightmapDatas = LightmapSettings.lightmaps;
        for (int i = 0; i < lightmapDatas.Length; i++)
        {
            string aoPath = path + "/AmbientOcclusion_" + i + ".tga";
            Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath);
            if (ao == null)
            {
                EditorUtility.DisplayDialog("Error", "There is no AO tex!", "OK");
                return;
            }
            lightmapDatas[i].lightmapColor = ao;
        }

        LightmapSettings.lightmaps = lightmapDatas;
    }

    void AddMeshCollider()
    {
        // find all mf
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] rootGOes = scene.GetRootGameObjects();

        List<MeshFilter> meshFilters = new List<MeshFilter>();

        foreach (var item in rootGOes)
        {
            MeshFilter[] findMFs = item.GetComponentsInChildren<MeshFilter>();
            foreach (var mf in findMFs)
            {
                var haveLightMap = (GameObjectUtility.GetStaticEditorFlags(mf.gameObject) & StaticEditorFlags.ContributeGI) != 0;
                if (haveLightMap)
                    meshFilters.Add(mf);
            }
        }

        meshColliders = new List<MeshCollider>();

        foreach (var item in meshFilters)
        {
            Collider collider = item.GetComponent<Collider>();
            if (collider == null)
            {
                MeshCollider meshCollider = item.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = item.sharedMesh;
                meshColliders.Add(meshCollider);
            }
        }
    }

    void RemoveMeshCollider()
    {
        foreach (var item in meshColliders)
        {
            GameObject.DestroyImmediate(item);
        }
    }

    void CreateAOTex()
    {
        LightmapData[] lightmaps = LightmapSettings.lightmaps;
        int len = lightmaps.Length;
        aoTex = new Texture2D[len];
        Color[] colors = new Color[resolution * resolution];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.white;
        }
        for (int i = 0; i < len; i++)
        {
            aoTex[i] = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
            aoTex[i].SetPixels(colors);
        }
    }

    void SaveAOTex()
    {
        string path = SceneManager.GetActiveScene().path;
        path = Path.GetDirectoryName(path);
        string name = SceneManager.GetActiveScene().name;
        path = Path.Combine(path, name);

        for (int i = 0; i < aoTex.Length; i++)
        {
            byte[] data = aoTex[i].EncodeToTGA();
            File.WriteAllBytes(path + "/AmbientOcclusion_" + i + ".tga", data);
        }
        AssetDatabase.Refresh();
    }

    void drawLine(int x1, int x2, int y, Point p1, Point p2, Point p3)
    {
        for (int x = x1; x <= x2; x++)
        {
            Vector2 f = new Vector2(x, y);
            // calculate vectors from point f to vertices p1, p2 and p3:
            var f1 = p1.uv - f;
            var f2 = p2.uv - f;
            var f3 = p3.uv - f;

            // calculate the areas and factors (order of parameters doesn't matter):
            var a = Vector3.Cross(p1.uv - p2.uv, p1.uv - p3.uv).magnitude; // main triangle area a
            var a1 = Vector3.Cross(f2, f3).magnitude / a; // p1's triangle area / a
            var a2 = Vector3.Cross(f3, f1).magnitude / a; // p2's triangle area / a 
            var a3 = Vector3.Cross(f1, f2).magnitude / a; // p3's triangle area / a

            var pos = p1.p * a1 + p2.p * a2 + p3.p * a3;
            var nor = p1.n * a1 + p2.n * a2 + p3.n * a3;

            if (aoIndex >= 0 && aoIndex < aoTex.Length)
            {
                aoTex[aoIndex].SetPixel(x, y, Color.black);
            }
        }

    }

    void fillBottomFlatTriangle(Point v1, Point v2, Point v3)
    {
        if (v2.uv.x > v3.uv.x) { Point t = v3; v3 = v2; v2 = t; }

        float invslope1 = (v2.uv.x - v1.uv.x) / (v2.uv.y - v1.uv.y);
        float invslope2 = (v3.uv.x - v1.uv.x) / (v3.uv.y - v1.uv.y);

        float curx1 = v1.uv.x;
        float curx2 = v1.uv.x;

        for (float scanlineY = v1.uv.y; scanlineY <= v2.uv.y; scanlineY++)
        {
            drawLine((int)curx1, (int)curx2, (int)scanlineY, v1, v2, v3);
            curx1 += invslope1;
            curx2 += invslope2;
        }
    }

    void fillTopFlatTriangle(Point v1, Point v2, Point v3)
    {
        if (v1.uv.x > v2.uv.x) { Point t = v1; v1 = v2; v2 = t; }

        float invslope1 = (v3.uv.x - v1.uv.x) / (v3.uv.y - v1.uv.y);
        float invslope2 = (v3.uv.x - v2.uv.x) / (v3.uv.y - v2.uv.y);

        float curx1 = v1.uv.x;
        float curx2 = v2.uv.x;

        for (float scanlineY = v1.uv.y; scanlineY <= v3.uv.y; scanlineY++)
        {
            drawLine((int)curx1, (int)curx2, (int)scanlineY, v1, v2, v3);
            curx1 += invslope1;
            curx2 += invslope2;
        }
    }

    void drawTriangle(Point vt1, Point vt2, Point vt3)
    {
        if (vt2.uv.y == vt3.uv.y)
        {
            fillBottomFlatTriangle(vt1, vt2, vt3);
        }
        /* check for trivial case of top-flat triangle */
        else if (vt1.uv.y == vt2.uv.y)
        {
            fillTopFlatTriangle(vt1, vt2, vt3);
        }
        else
        {
            /* general case - split the triangle in a topflat and bottom-flat one */
            float factor = ((float)(vt2.uv.y - vt1.uv.y) / (float)(vt3.uv.y - vt1.uv.y));
            Point v4 = new Point(vt1.p + (vt3.p - vt1.p) * factor, vt1.n + (vt3.n - vt1.n) * factor, vt1.uv + (vt3.uv - vt1.uv) * factor);
            fillBottomFlatTriangle(vt1, vt2, v4);
            fillTopFlatTriangle(vt2, v4, vt3);
        }
    }

    float sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    bool PointInTriangle(Vector2 pt, Vector2 v1, Vector2 v2, Vector2 v3)
    {
        float d1, d2, d3;
        bool has_neg, has_pos;

        d1 = sign(pt, v1, v2);
        d2 = sign(pt, v2, v3);
        d3 = sign(pt, v3, v1);

        has_neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        has_pos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(has_neg && has_pos);
    }

    void drawTriangleSimple(Point p1, Point p2, Point p3)
    {
        /* get the bounding box of the triangle */
        float maxX = Mathf.Max(p1.uv.x, Mathf.Max(p2.uv.x, p3.uv.x));
        maxX = Mathf.Ceil(maxX);
        float minX = Mathf.Min(p1.uv.x, Mathf.Min(p2.uv.x, p3.uv.x));
        minX = Mathf.Floor(minX);
        float maxY = Mathf.Max(p1.uv.y, Mathf.Max(p2.uv.y, p3.uv.y));
        maxY = Mathf.Ceil(maxY);
        float minY = Mathf.Min(p1.uv.y, Mathf.Min(p2.uv.y, p3.uv.y));
        minY = Mathf.Floor(minY);

        for (float x = minX; x <= maxX; x++)
        {
            for (float y = minY; y <= maxY; y++)
            {
                Vector2 f = new Vector2(x + 0.5f, y + 0.5f);
                if (PointInTriangle(f, p1.uv, p2.uv, p3.uv))
                {
                    // calculate vectors from point f to vertices p1, p2 and p3:
                    var f1 = p1.uv - f;
                    var f2 = p2.uv - f;
                    var f3 = p3.uv - f;

                    // calculate the areas and factors (order of parameters doesn't matter):
                    var a = Vector3.Cross(p1.uv - p2.uv, p1.uv - p3.uv).magnitude; // main triangle area a
                    var a1 = Vector3.Cross(f2, f3).magnitude / a; // p1's triangle area / a
                    var a2 = Vector3.Cross(f3, f1).magnitude / a; // p2's triangle area / a 
                    var a3 = Vector3.Cross(f1, f2).magnitude / a; // p3's triangle area / a

                    var pos = p1.p * a1 + p2.p * a2 + p3.p * a3;
                    var nor = p1.n * a1 + p2.n * a2 + p3.n * a3;

                    float occlusion = 0;
                    // Main loop, take samples up to the limit
                    for (int j = 0; j < rayCount; j++)
                    {

                        Vector3 ray = Random.onUnitSphere;
                        if (Vector3.Dot(ray, nor) < 0) ray = -ray;

                        if (!Physics.Linecast(pos, pos + ray * range))
                        {
                            if (enablePBR)
                                occlusion += Vector3.Dot(nor, ray) * 2;
                            else
                                occlusion++;
                        }
                    }
                    float c = (float)occlusion / rayCount;
                    Color color = new Color(c, 0, 0, 1);

                    aoTex[aoIndex].SetPixel((int)x, (int)y, color);
                }
            }
        }
    }

    void FixSeam()
    {
        for (int i = 0; i < aoTex.Length; i++)
        {
            Color[] srcColors = aoTex[i].GetPixels();
            Color[] dstColors = aoTex[i].GetPixels();

            for (int m = 0; m < resolution; m++)
            {
                for (int n = 0; n < resolution; n++)
                {
                    Color c = srcColors[m * resolution + n];
                    if (c.g > 0.5)
                    {
                        for (int j = -1; j <= 1; j++)
                        {
                            for (int k = -1; k <= 1; k++)
                            {
                                int x = Mathf.Clamp(m + j, 0, resolution - 1);
                                int y = Mathf.Clamp(n + k, 0, resolution - 1);

                                c.r = Mathf.Min(c.r, srcColors[x * resolution + y].r);
                            }
                        }
                    }
                    if (useBlur)
                        dstColors[m * resolution + n].r = c.r;
                    else
                    {
                        dstColors[m * resolution + n].r = c.r;
                        dstColors[m * resolution + n].g = c.r;
                        dstColors[m * resolution + n].b = c.r;
                    }
                }
            }

            aoTex[i].SetPixels(dstColors);
        }
    }

    void BlurAO()
    {
        for (int i = 0; i < aoTex.Length; i++)
        {
            Color[] srcColors = aoTex[i].GetPixels();
            Color[] dstColors = aoTex[i].GetPixels();

            for (int m = 0; m < resolution; m++)
            {
                for (int n = 0; n < resolution; n++)
                {
                    Color c = srcColors[m * resolution + n];
                    if (c.g < 0.5)
                    {
                        c = Color.black;
                        for (int j = -1; j <= 1; j++)
                        {
                            for (int k = -1; k <= 1; k++)
                            {
                                int x = Mathf.Clamp(m + j, 0, resolution - 1);
                                int y = Mathf.Clamp(n + k, 0, resolution - 1);

                                c.r += srcColors[x * resolution + y].r;
                            }
                        }
                        c = c / 9;
                    }
                    dstColors[m * resolution + n].r = c.r;
                    dstColors[m * resolution + n].g = c.r;
                    dstColors[m * resolution + n].b = c.r;
                }
            }

            aoTex[i].SetPixels(dstColors);
        }
    }

    void StartBake()
    {
        // find all mf
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] rootGOes = scene.GetRootGameObjects();

        List<MeshFilter> meshFilters = new List<MeshFilter>();

        foreach (var item in rootGOes)
        {
            MeshFilter[] findMFs = item.GetComponentsInChildren<MeshFilter>();
            foreach (var mf in findMFs)
            {
                var haveLightMap = (GameObjectUtility.GetStaticEditorFlags(mf.gameObject) & StaticEditorFlags.ContributeGI) != 0;
                if (haveLightMap)
                    meshFilters.Add(mf);
            }
        }

        // create ao tex
        CreateAOTex();

        for (int i = 0; i < meshFilters.Count; i++)
        {
            Mesh mesh = meshFilters[i].sharedMesh;
            Transform transform = meshFilters[i].transform;

            // Store vertices
            Vector3[] verts = mesh.vertices;
            for (int j = 0; j < verts.Length; j++)
            {
                verts[j] = transform.TransformPoint(verts[j]);
            }

            // Store normals
            Vector3[] normals = new Vector3[mesh.normals.Length];
            if (normals.Length == 0)
                mesh.RecalculateNormals();
            normals = mesh.normals;
            for (int j = 0; j < normals.Length; j++)
            {
                normals[j] = transform.TransformDirection(normals[j]);
            }

            // store triangles
            int[] triangles = mesh.triangles;

            // store uv
            MeshRenderer meshRenderer = meshFilters[i].GetComponent<MeshRenderer>();
            aoIndex = meshRenderer.lightmapIndex;
            Vector4 scaleOffset = meshRenderer.lightmapScaleOffset;
            Vector2[] uvs = mesh.uv2;
            if (uvs == null || uvs.Length == 0) uvs = mesh.uv;
            for (int j = 0; j < uvs.Length; j++)
            {
                uvs[j].x = uvs[j].x * scaleOffset.x + scaleOffset.z;
                uvs[j].y = uvs[j].y * scaleOffset.y + scaleOffset.w;
                uvs[j] *= resolution;
            }

            int triangleCount = triangles.Length / 3;
            for (int j = 0; j < triangleCount; j++)
            {
                int id0 = triangles[j * 3];
                int id1 = triangles[j * 3 + 1];
                int id2 = triangles[j * 3 + 2];

                Point[] p = new Point[3];

                p[0] = new Point(verts[id0], normals[id0], uvs[id0]);
                p[1] = new Point(verts[id1], normals[id1], uvs[id1]);
                p[2] = new Point(verts[id2], normals[id2], uvs[id2]);

                // sort by uv.y
                for (int m = 0; m < 3; m++)
                {
                    for (int n = 2; n > m; n--)
                    {
                        if (p[n - 1].uv.y > p[n].uv.y) { Point t = p[n - 1]; p[n - 1] = p[n]; p[n] = t; }
                    }
                }

                drawTriangleSimple(p[0], p[1], p[2]);
                EditorUtility.DisplayProgressBar("Bake prograss", "Prograss:" + (j * 100 / triangleCount).ToString("f0"), (float)j / triangleCount);
            }


            EditorUtility.DisplayProgressBar("Bake prograss", "Prograss:" + (i * 100 / meshFilters.Count).ToString("f0"), (float)i / meshFilters.Count);
        }

        FixSeam();

        if (useBlur)
            BlurAO();

        SaveAOTex();

        EditorUtility.ClearProgressBar();
    }

}