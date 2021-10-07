using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections;
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
    bool groupEnabled;
    int rayCount = 512;
    float range = 1.5f;
    bool useBlur = false;
    float blurRadius = 1f;
    bool averageNormals = false;
    Texture2D[] aoTex = null;


    // Add menu named "My Window" to the Window menu
    [MenuItem("Window/AOBakeWindow")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        AOBakeWindow window = (AOBakeWindow)EditorWindow.GetWindow(typeof(AOBakeWindow));
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Base Settings", EditorStyles.boldLabel);

        groupEnabled = EditorGUILayout.BeginToggleGroup("Optional Settings", groupEnabled);
        rayCount = EditorGUILayout.IntSlider("Ray Count", rayCount, 0, 1024);
        range = EditorGUILayout.Slider("Range", range, 0, 3);
        useBlur = EditorGUILayout.Toggle("Use Blur", useBlur);
        averageNormals = EditorGUILayout.Toggle("Average Normals", averageNormals);
        blurRadius = EditorGUILayout.Slider("Range", blurRadius, 0, 3);

        if (GUILayout.Button("Bake Start"))
        {
            StartBake();
        }

        EditorGUILayout.EndToggleGroup();
    }

    void CreateAOTex(int width)
    {

        LightmapData[] lightmaps = LightmapSettings.lightmaps;
        int len = lightmaps.Length;
        aoTex = new Texture2D[len];
        for (int i = 0; i < len; i++)
        {
            aoTex[i] = new Texture2D(width, width, TextureFormat.RGB24, false);
        }
    }

    void SaveAOTex(Texture2D[] aoTex)
    {
        string path = SceneManager.GetActiveScene().path;

        // todo save ao tex
        Debug.Log(path);
    }

    void drawLine(float x1, float y1, float x2, float y2, Point p1, Point p2, Point p3)
    {
        for (float x = x1; x1 < x2; x1++)
        {
            Vector2 f = new Vector2(x, y1);
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
            drawLine(curx1, scanlineY, curx2, scanlineY, v1, v2, v3);
            curx1 += invslope1;
            curx2 += invslope2;
        }
    }

    void fillTopFlatTriangle(Point v1, Point v2, Point v3)
    {
        if (v1.uv.x > v2.uv.x) { Point t = v3; v3 = v2; v2 = t; }

        float invslope1 = (v3.uv.x - v1.uv.x) / (v3.uv.y - v1.uv.y);
        float invslope2 = (v3.uv.x - v2.uv.x) / (v3.uv.y - v2.uv.y);

        float curx1 = v3.uv.x;
        float curx2 = v3.uv.x;

        for (float scanlineY = v3.uv.x; scanlineY > v1.uv.x; scanlineY--)
        {
            drawLine(curx1, scanlineY, curx2, scanlineY, v1, v2, v3);
            curx1 -= invslope1;
            curx2 -= invslope2;
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

    void StartBake()
    {
        int resolution = 1024;
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
        CreateAOTex(resolution);

        for (int i = 0; i < meshFilters.Count; i++)
        {
            Mesh mesh = meshFilters[i].sharedMesh;

            // Store vertices
            Vector3[] verts = mesh.vertices;

            // Store normals
            Vector3[] normals = new Vector3[mesh.normals.Length];
            if (normals.Length == 0)
                mesh.RecalculateNormals();
            if (averageNormals)
            {
                Mesh clonemesh = new Mesh();
                clonemesh.vertices = mesh.vertices;
                clonemesh.normals = mesh.normals;
                clonemesh.tangents = mesh.tangents;
                clonemesh.triangles = mesh.triangles;
                clonemesh.RecalculateBounds();
                clonemesh.RecalculateNormals();
                normals = clonemesh.normals;
                Object.DestroyImmediate(clonemesh);
            }
            else
            {
                normals = mesh.normals;
            }

            // store triangles
            int[] triangles = mesh.triangles;

            // store uv
            MeshRenderer meshRenderer = meshFilters[i].GetComponent<MeshRenderer>();
            Vector4 scaleOffset = meshRenderer.lightmapScaleOffset;
            Vector2[] uvs = mesh.uv2;
            for (int j = 0; j < uvs.Length; j++)
            {
                uvs[j].x = uvs[j].x * scaleOffset.x + scaleOffset.z;
                uvs[j].y = uvs[j].y * scaleOffset.y + scaleOffset.w;
                uvs[j] *= resolution;
            }

            for (int j = 0; j < triangles.Length / 3; j++)
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
                    for (int n = m + 1; n < 3; n++)
                    {
                        if (p[m].uv.y < p[n].uv.y) { Point t = p[m]; p[m] = p[n]; p[n] = t; }
                    }
                }

                drawTriangle(p[0], p[1], p[2]);
            }
           
           
            EditorUtility.DisplayProgressBar("Bake prograss", "进度:" + ((float)i / meshFilters.Count).ToString("f2"), (float)i / meshFilters.Count);
        }
        EditorUtility.ClearProgressBar();
    }
}