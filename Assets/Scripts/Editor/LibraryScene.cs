using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class LibrarySceneBuilder
{
    const float W = 14f, D = 18f, H = 4.5f; // room size
    const string TexDir = "Assets/Generated/Textures";
    const string MatDir = "Assets/Generated/Materials";

    static Transform root;
    static Transform heroRoot, heroVisual;
    static Light heroGlow;

    delegate void Gen(int n, float[] h, Color[] c);

    [MenuItem("Mystra/Build Library Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Directory.CreateDirectory(MatDir);
        Directory.CreateDirectory(TexDir);
        Random.InitState(1520);
        heroRoot = null; heroVisual = null; heroGlow = null;

        MakeAllTextures();

        root = new GameObject("Library").transform;

        BuildRoom();
        BuildShelves();
        BuildFurniture();
        BuildLighting();
        BuildPlayerAndBook();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Scenes/Library.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("Library scene built! Press Play.");
    }

    // =====================================================
    //  PROCEDURAL TEXTURES
    // =====================================================
    static float Hash(int a, int b)
    {
        float s = Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f;
        return s - Mathf.Floor(s);
    }

    // tileable noise (no visible seams when repeated)
    static float TN(float u, float v, float sx, float sy, float off)
    {
        float a = Mathf.PerlinNoise(100f + off + u * sx, 100f + off + v * sy);
        float b = Mathf.PerlinNoise(100f + off + (u - 1f) * sx, 100f + off + v * sy);
        float c = Mathf.PerlinNoise(100f + off + u * sx, 100f + off + (v - 1f) * sy);
        float d = Mathf.PerlinNoise(100f + off + (u - 1f) * sx, 100f + off + (v - 1f) * sy);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static float F(float u, float v, float s, float off)
    {
        return TN(u, v, s, s, off) * 0.6f + TN(u, v, s * 3f, s * 3f, off + 11f) * 0.4f;
    }

    static void MakeAllTextures()
    {
        MakeTex("Stone", 512, GenStone, 6f);
        MakeTex("Planks", 512, GenPlanks, 5f);
        MakeTex("DarkWood", 256, GenWood, 4f);
        MakeTex("Leather", 128, GenLeather, 3f);
        MakeTex("Rug", 256, GenRug, 0f);
        AssetDatabase.Refresh();
    }

    static void MakeTex(string name, int n, Gen gen, float bump)
    {
        string p = TexDir + "/R_" + name + ".png";
        if (File.Exists(p)) return;

        var h = new float[n * n];
        var c = new Color[n * n];
        gen(n, h, c);
        SavePng(p, n, c, false);

        if (bump > 0f)
        {
            var nc = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float hl = h[y * n + (x - 1 + n) % n];
                    float hr = h[y * n + (x + 1) % n];
                    float hd = h[((y - 1 + n) % n) * n + x];
                    float hu = h[((y + 1) % n) * n + x];
                    Vector3 v = new Vector3((hl - hr) * bump, (hd - hu) * bump, 1f).normalized;
                    nc[y * n + x] = new Color(v.x * 0.5f + 0.5f, v.y * 0.5f + 0.5f, v.z * 0.5f + 0.5f, 1f);
                }
            SavePng(TexDir + "/R_" + name + "_n.png", n, nc, true);
        }
    }

    static void SavePng(string path, int n, Color[] px, bool normal)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.SetPixels(px);
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        if (normal)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
        }
    }

    // stone blocks with mortar
    static void GenStone(int n, float[] h, Color[] c)
    {
        int rows = 8, cols = 4;
        int bh = n / rows, bw = n / cols, mortar = 5;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (float)x / n, v = (float)y / n;
                int row = y / bh;
                int xo = (x + (row % 2) * (bw / 2)) % n;
                int colIdx = xo / bw;
                int lx = xo % bw, ly = y % bh;
                float edge = Mathf.Min(Mathf.Min(lx, bw - 1 - lx), Mathf.Min(ly, bh - 1 - ly));
                float tone = 0.62f + (Hash(colIdx, row) - 0.5f) * 0.22f;
                float noise = F(u, v, 8f, 3f);
                float fine = F(u, v, 40f, 9f);
                float hh; Color cc;
                if (edge < mortar)
                {
                    hh = 0.1f + fine * 0.1f;
                    float m = 0.36f + fine * 0.15f;
                    cc = new Color(m, m * 0.95f, m * 0.88f);
                }
                else
                {
                    float round = Mathf.Clamp01((edge - mortar) / 6f);
                    hh = 0.55f + round * 0.2f + noise * 0.25f + fine * 0.05f;
                    float t = tone * (0.75f + noise * 0.5f) * (0.9f + fine * 0.2f);
                    cc = new Color(t, t * 0.94f, t * 0.86f);
                }
                h[y * n + x] = hh;
                c[y * n + x] = cc;
            }
    }

    // wooden floor planks
    static void GenPlanks(int n, float[] h, Color[] c)
    {
        int rows = 16, bh = n / rows;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (float)x / n, v = (float)y / n;
                int row = y / bh, ly = y % bh;
                int jx = (int)(Hash(row, 5) * n);
                int seg = x < jx ? 0 : 1;
                float tone = 0.55f + (Hash(row, seg) - 0.5f) * 0.3f;
                float g = TN(u, v, 3f, 150f, 40f) * 0.6f + TN(u, v, 12f, 300f, 70f) * 0.4f;
                bool gap = ly < 1 || ly >= bh - 1 || Mathf.Abs(x - jx) < 1;
                if (gap)
                {
                    h[y * n + x] = 0.05f;
                    c[y * n + x] = new Color(0.05f, 0.03f, 0.02f);
                }
                else
                {
                    float t = tone * (0.7f + g * 0.6f);
                    h[y * n + x] = 0.7f + g * 0.2f;
                    c[y * n + x] = new Color(t, t * 0.68f, t * 0.42f);
                }
            }
    }

    // dark wood grain
    static void GenWood(int n, float[] h, Color[] c)
    {
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (float)x / n, v = (float)y / n;
                float g = TN(u, v, 2f, 24f, 60f) * 0.6f + TN(u, v, 6f, 70f, 90f) * 0.4f;
                float ring = Mathf.Sin((v * 8f + g * 3f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                float t = 0.35f + ring * 0.25f + g * 0.25f;
                h[y * n + x] = ring * 0.4f + g * 0.3f;
                c[y * n + x] = new Color(t, t * 0.62f, t * 0.38f);
            }
    }

    // book leather (grey, gets tinted by material colour)
    static void GenLeather(int n, float[] h, Color[] c)
    {
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (float)x / n, v = (float)y / n;
                float f = F(u, v, 12f, 5f);
                float t = 0.55f + f * 0.4f;
                h[y * n + x] = f;
                c[y * n + x] = new Color(t, t, t);
            }
    }

    // woven rug with border and diamonds
    static void GenRug(int n, float[] h, Color[] c)
    {
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (float)x / n, v = (float)y / n;
                float d = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                Color col;
                if (d < 0.04f) col = new Color(0.75f, 0.58f, 0.22f);
                else if (d < 0.08f) col = new Color(0.1f, 0.12f, 0.3f);
                else if (d < 0.10f) col = new Color(0.75f, 0.58f, 0.22f);
                else
                {
                    float a = Mathf.Abs(Mathf.Repeat(u * 4f, 1f) - 0.5f) + Mathf.Abs(Mathf.Repeat(v * 4f, 1f) - 0.5f);
                    col = a < 0.28f ? new Color(0.62f, 0.13f, 0.1f) : new Color(0.38f, 0.05f, 0.07f);
                }
                float fine = F(u, v, 60f, 21f);
                float k = 0.75f + fine * 0.5f;
                c[y * n + x] = new Color(col.r * k, col.g * k, col.b * k);
                h[y * n + x] = 0.5f;
            }
    }

    // =====================================================
    //  HELPERS
    // =====================================================
    static Material Mat(string name, Color c, bool emissive = false, float smooth = 0.2f)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 2.5f);
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material TMat(string name, string tex, Color tint, Vector2 tiling, float smooth, float bump = 1f)
    {
        string path = MatDir + "/R_" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;

        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/R_" + tex + ".png");
        var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/R_" + tex + "_n.png");

        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh);
        m.color = tint;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);

        if (albedo != null)
        {
            if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", albedo); m.SetTextureScale("_BaseMap", tiling); }
            if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", albedo); m.SetTextureScale("_MainTex", tiling); }
        }
        if (nrm != null && m.HasProperty("_BumpMap"))
        {
            m.SetTexture("_BumpMap", nrm);
            m.SetTextureScale("_BumpMap", tiling);
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", bump);
            m.EnableKeyword("_NORMALMAP");
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject Box(string name, Vector3 pos, Vector3 size, Material mat, Transform parent = null, bool collider = true)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent ? parent : root, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static GameObject Cyl(string name, Vector3 pos, Vector3 size, Material mat, Transform parent = null)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        g.transform.SetParent(parent ? parent : root, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

    static Light PointLight(string name, Vector3 pos, Color c, float intensity, float range, Transform parent = null)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent ? parent : root, false);
        g.transform.localPosition = pos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    // =====================================================
    //  ROOM
    // =====================================================
    static void BuildRoom()
    {
        var stoneTint = new Color(0.95f, 0.9f, 0.82f);
        var floor = TMat("Floor", "Planks", Color.white, new Vector2(W / 3f, D / 3f), 0.35f, 1f);
        var ceil = TMat("Ceil", "Planks", new Color(0.45f, 0.38f, 0.33f), new Vector2(W / 3f, D / 3f), 0.2f, 1f);
        var wallLR = TMat("WallLR", "Stone", stoneTint, new Vector2(D / 3f, H / 3f), 0.1f, 1.2f);
        var wallFB = TMat("WallFB", "Stone", stoneTint, new Vector2((W + 0.6f) / 3f, H / 3f), 0.1f, 1.2f);
        var trim = TMat("Trim", "DarkWood", new Color(0.75f, 0.62f, 0.55f), new Vector2(2f, 1f), 0.35f, 1f);
        var panelLR = TMat("PanelLR", "DarkWood", new Color(0.8f, 0.65f, 0.55f), new Vector2(D / 2f, 0.6f), 0.4f, 1f);
        var panelFB = TMat("PanelFB", "DarkWood", new Color(0.8f, 0.65f, 0.55f), new Vector2(W / 2f, 0.6f), 0.4f, 1f);
        var rug = TMat("Rug", "Rug", Color.white, Vector2.one, 0.15f, 1f);
        var stoneTrim = TMat("StoneTrim", "Stone", new Color(0.85f, 0.8f, 0.72f), Vector2.one, 0.1f, 1f);

        Box("Floor", new Vector3(0, -0.1f, 0), new Vector3(W, 0.2f, D), floor);
        Box("Ceiling", new Vector3(0, H + 0.1f, 0), new Vector3(W, 0.2f, D), ceil);
        Box("Wall_Left", new Vector3(-W / 2 - 0.15f, H / 2, 0), new Vector3(0.3f, H, D), wallLR);
        Box("Wall_Right", new Vector3(W / 2 + 0.15f, H / 2, 0), new Vector3(0.3f, H, D), wallLR);
        Box("Wall_Back", new Vector3(0, H / 2, D / 2 + 0.15f), new Vector3(W + 0.6f, H, 0.3f), wallFB);
        Box("Wall_Front", new Vector3(0, H / 2, -D / 2 - 0.15f), new Vector3(W + 0.6f, H, 0.3f), wallFB);

        // wooden wall paneling (lower part of walls)
        Box("Panel_L", new Vector3(-W / 2 + 0.03f, 0.55f, 0), new Vector3(0.06f, 1.1f, D), panelLR, null, false);
        Box("Panel_R", new Vector3(W / 2 - 0.03f, 0.55f, 0), new Vector3(0.06f, 1.1f, D), panelLR, null, false);
        Box("Panel_B", new Vector3(0, 0.55f, D / 2 - 0.03f), new Vector3(W, 1.1f, 0.06f), panelFB, null, false);
        Box("Panel_F", new Vector3(0, 0.55f, -D / 2 + 0.03f), new Vector3(W, 1.1f, 0.06f), panelFB, null, false);
        // panel top rail
        Box("Rail_L", new Vector3(-W / 2 + 0.05f, 1.12f, 0), new Vector3(0.1f, 0.06f, D), trim, null, false);
        Box("Rail_R", new Vector3(W / 2 - 0.05f, 1.12f, 0), new Vector3(0.1f, 0.06f, D), trim, null, false);
        Box("Rail_B", new Vector3(0, 1.12f, D / 2 - 0.05f), new Vector3(W, 0.06f, 0.1f), trim, null, false);
        Box("Rail_F", new Vector3(0, 1.12f, -D / 2 + 0.05f), new Vector3(W, 0.06f, 0.1f), trim, null, false);
        // baseboards
        Box("Base_L", new Vector3(-W / 2 + 0.06f, 0.12f, 0), new Vector3(0.12f, 0.24f, D), trim, null, false);
        Box("Base_R", new Vector3(W / 2 - 0.06f, 0.12f, 0), new Vector3(0.12f, 0.24f, D), trim, null, false);
        Box("Base_B", new Vector3(0, 0.12f, D / 2 - 0.06f), new Vector3(W, 0.24f, 0.12f), trim, null, false);
        Box("Base_F", new Vector3(0, 0.12f, -D / 2 + 0.06f), new Vector3(W, 0.24f, 0.12f), trim, null, false);
        // crown molding
        Box("Crown_L", new Vector3(-W / 2 + 0.09f, H - 0.1f, 0), new Vector3(0.18f, 0.2f, D), trim, null, false);
        Box("Crown_R", new Vector3(W / 2 - 0.09f, H - 0.1f, 0), new Vector3(0.18f, 0.2f, D), trim, null, false);
        Box("Crown_B", new Vector3(0, H - 0.1f, D / 2 - 0.09f), new Vector3(W, 0.2f, 0.18f), trim, null, false);
        Box("Crown_F", new Vector3(0, H - 0.1f, -D / 2 + 0.09f), new Vector3(W, 0.2f, 0.18f), trim, null, false);

        // ceiling beams
        for (float z = -7.5f; z <= 7.6f; z += 3f)
            Box("Beam", new Vector3(0, H - 0.18f, z), new Vector3(W, 0.36f, 0.4f), trim, null, false);

        // rug
        Box("Rug", new Vector3(0, 0.012f, 0), new Vector3(6f, 0.02f, 8f), rug, null, false);

        // windows on the right wall
        var glass = Mat("Window", new Color(0.4f, 0.55f, 0.9f), true);
        for (float z = -5f; z <= 5.1f; z += 5f)
        {
            float x = W / 2;
            Box("WindowGlass", new Vector3(x - 0.02f, 2.5f, z), new Vector3(0.05f, 2f, 1.4f), glass, null, false);
            Box("MullionV", new Vector3(x - 0.06f, 2.5f, z), new Vector3(0.08f, 2.1f, 0.07f), trim, null, false);
            Box("MullionH", new Vector3(x - 0.06f, 2.5f, z), new Vector3(0.08f, 0.07f, 1.5f), trim, null, false);
            Box("SillStone", new Vector3(x - 0.14f, 1.46f, z), new Vector3(0.28f, 0.12f, 1.9f), stoneTrim, null, false);
            Box("Lintel", new Vector3(x - 0.1f, 3.58f, z), new Vector3(0.2f, 0.16f, 1.9f), stoneTrim, null, false);
            Box("SurroundA", new Vector3(x - 0.1f, 2.5f, z - 0.82f), new Vector3(0.2f, 2.2f, 0.16f), stoneTrim, null, false);
            Box("SurroundB", new Vector3(x - 0.1f, 2.5f, z + 0.82f), new Vector3(0.2f, 2.2f, 0.16f), stoneTrim, null, false);

            // cool moonlight coming in
            var sp = new GameObject("MoonBeam");
            sp.transform.SetParent(root, false);
            sp.transform.position = new Vector3(x + 1.5f, 3.2f, z);
            sp.transform.rotation = Quaternion.LookRotation(new Vector3(-1f, -0.45f, 0f));
            var l = sp.AddComponent<Light>();
            l.type = LightType.Spot; l.spotAngle = 70f; l.range = 12f; l.intensity = 10f;
            l.color = new Color(0.55f, 0.65f, 1f); l.shadows = LightShadows.None;
        }

        // door on front wall
        var doorMat = TMat("Door", "DarkWood", new Color(0.7f, 0.5f, 0.4f), new Vector2(1f, 1f), 0.3f, 1f);
        Box("DoorFrame", new Vector3(0, 1.15f, -D / 2 + 0.06f), new Vector3(1.9f, 2.4f, 0.08f), trim, null, false);
        Box("Door", new Vector3(0, 1.1f, -D / 2 + 0.11f), new Vector3(1.6f, 2.2f, 0.1f), doorMat, null, false);
        Box("DoorHandle", new Vector3(0.55f, 1.05f, -D / 2 + 0.2f), new Vector3(0.08f, 0.08f, 0.08f), Mat("Brass", new Color(0.8f, 0.6f, 0.2f), false, 0.7f), null, false);
    }

    // =====================================================
    //  BOOKSHELVES
    // =====================================================
    static void BuildShelves()
    {
        // left wall (front faces +x)
        for (int i = 0; i < 6; i++)
            Shelf(new Vector3(-W / 2 + 0.2f, 0, -7f + i * 2.8f), 90);
        // back wall (front faces -z). The hero book is in the third shelf.
        for (int i = 0; i < 4; i++)
            Shelf(new Vector3(-4.2f + i * 2.8f, 0, D / 2 - 0.2f), 180, i == 2);
        // right wall, between the windows (front faces -x)
        Shelf(new Vector3(W / 2 - 0.2f, 0, -7.6f), -90);
        Shelf(new Vector3(W / 2 - 0.2f, 0, -2.5f), -90);
        Shelf(new Vector3(W / 2 - 0.2f, 0, 2.5f), -90);
        Shelf(new Vector3(W / 2 - 0.2f, 0, 7.6f), -90);
        // free standing rows
        Shelf(new Vector3(-4f, 0, -2.5f), 0);
        Shelf(new Vector3(4f, 0, -2.5f), 0);
    }

    static void Shelf(Vector3 pos, float rotY, bool hero = false)
    {
        var wood = TMat("ShelfWood", "DarkWood", new Color(0.8f, 0.65f, 0.55f), Vector2.one, 0.3f, 1f);
        var gold = Mat("GoldTrim", new Color(0.8f, 0.6f, 0.2f), false, 0.8f);
        var g = new GameObject("Bookshelf");
        g.transform.SetParent(root, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0, rotY, 0);
        var t = g.transform;

        float w = 2.6f, h = 3.6f, d = 0.5f;
        Box("Back", new Vector3(0, h / 2, -d / 2 + 0.02f), new Vector3(w, h, 0.05f), wood, t);
        Box("SideL", new Vector3(-w / 2, h / 2, 0), new Vector3(0.06f, h, d), wood, t);
        Box("SideR", new Vector3(w / 2, h / 2, 0), new Vector3(0.06f, h, d), wood, t);
        Box("Top", new Vector3(0, h, 0), new Vector3(w + 0.12f, 0.08f, d + 0.06f), wood, t);
        Box("Cornice", new Vector3(0, h + 0.08f, 0.02f), new Vector3(w + 0.2f, 0.08f, d + 0.12f), wood, t, false);

        Color[] cols = {
            new Color(0.55f,0.1f,0.1f), new Color(0.12f,0.2f,0.45f), new Color(0.1f,0.35f,0.18f),
            new Color(0.4f,0.25f,0.12f), new Color(0.35f,0.05f,0.12f), new Color(0.7f,0.6f,0.4f), new Color(0.12f,0.1f,0.1f)
        };
        var bookMats = new Material[cols.Length];
        for (int i = 0; i < cols.Length; i++)
            bookMats[i] = TMat("Book" + i, "Leather", cols[i], Vector2.one, 0.45f, 0.6f);

        bool heroPlaced = false;
        int levels = 5;
        for (int l = 0; l < levels; l++)
        {
            float y = 0.1f + l * 0.7f;
            Box("Plank", new Vector3(0, y, 0), new Vector3(w, 0.05f, d), wood, t);
            float x = -w / 2 + 0.1f;
            while (x < w / 2 - 0.12f)
            {
                // the special book, pulled out a little from the shelf
                if (hero && l == 2 && !heroPlaced && x >= -0.1f)
                {
                    PlaceHeroBook(t, x, y);
                    heroPlaced = true;
                    x += 0.12f;
                    continue;
                }

                if (Random.value < 0.06f) { x += 0.15f; continue; } // small gap
                float bw = Random.Range(0.04f, 0.1f);
                float bh = Random.Range(0.35f, 0.58f);
                float bd = Random.Range(0.26f, 0.32f);
                int c = Random.Range(0, cols.Length);
                float zOff = 0.02f + Random.Range(-0.02f, 0.03f);
                var b = Box("Book", new Vector3(x + bw / 2, y + 0.025f + bh / 2, zOff), new Vector3(bw, bh, bd), bookMats[c], t, false);
                if (Random.value < 0.04f) b.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-15f, 15f));
                else if (Random.value < 0.3f)
                {
                    // gold bands on the spine
                    float zf = zOff + bd / 2 + 0.002f;
                    Box("Band", new Vector3(x + bw / 2, y + 0.025f + bh * 0.8f, zf), new Vector3(bw * 1.02f, 0.015f, 0.005f), gold, t, false);
                    Box("Band", new Vector3(x + bw / 2, y + 0.025f + bh * 0.25f, zf), new Vector3(bw * 1.02f, 0.015f, 0.005f), gold, t, false);
                }
                x += bw + 0.005f;
            }
        }

        // shelf collider so the player can't walk through
        var col = g.AddComponent<BoxCollider>();
        col.center = new Vector3(0, h / 2, 0); col.size = new Vector3(w, h, d);
    }

    static void PlaceHeroBook(Transform shelf, float x, float plankY)
    {
        var cover = TMat("HeroCover", "Leather", new Color(0.3f, 0.05f, 0.06f), Vector2.one, 0.5f, 0.8f);
        var glowGold = Mat("HeroGold", new Color(1f, 0.75f, 0.25f), true, 0.8f);

        float hw = 0.1f, hh = 0.52f, hd = 0.34f;
        float hy = plankY + 0.025f + hh / 2f;

        heroRoot = new GameObject("MysteriousBook").transform;
        heroRoot.SetParent(shelf, false);
        heroRoot.localPosition = new Vector3(x + hw / 2f, hy, 0.12f); // sticks out in front of the other books

        Box("HeroBook", Vector3.zero, new Vector3(hw, hh, hd), cover, heroRoot, false);
        float zf = hd / 2f + 0.002f;
        Box("HeroBandTop", new Vector3(0, 0.17f, zf), new Vector3(hw * 1.04f, 0.025f, 0.006f), glowGold, heroRoot, false);
        Box("HeroBandBottom", new Vector3(0, -0.17f, zf), new Vector3(hw * 1.04f, 0.025f, 0.006f), glowGold, heroRoot, false);
        var emblem = Box("HeroEmblem", new Vector3(0, 0f, zf + 0.001f), new Vector3(0.05f, 0.05f, 0.006f), glowGold, heroRoot, false);
        emblem.transform.localRotation = Quaternion.Euler(0, 0, 45f);

        heroVisual = new GameObject("BookVisual").transform;
        heroVisual.SetParent(heroRoot, false);
        heroVisual.localPosition = Vector3.zero;

        heroGlow = PointLight("BookGlow", new Vector3(0, 0f, 0.6f), new Color(1f, 0.8f, 0.35f), 2.5f, 4f, heroRoot);

        var trig = heroRoot.gameObject.AddComponent<BoxCollider>();
        trig.isTrigger = true; trig.center = new Vector3(0, 0, 0.6f); trig.size = new Vector3(1.8f, 2f, 1.6f);
    }

    // =====================================================
    //  FURNITURE
    // =====================================================
    static void BuildFurniture()
    {
        var wood = TMat("TableWood", "DarkWood", new Color(0.85f, 0.7f, 0.6f), Vector2.one, 0.4f, 1f);
        var chair = TMat("ChairWood", "DarkWood", new Color(0.7f, 0.55f, 0.5f), Vector2.one, 0.3f, 1f);
        var paper = Mat("Paper", new Color(0.85f, 0.78f, 0.6f));
        var gold = Mat("Brass", new Color(0.8f, 0.6f, 0.2f), false, 0.7f);

        // reading tables
        foreach (var p in new[] { new Vector3(0, 0, -1.5f), new Vector3(0, 0, 1.8f) })
        {
            var tb = new GameObject("Table").transform; tb.SetParent(root, false); tb.localPosition = p;
            Box("Top", new Vector3(0, 0.8f, 0), new Vector3(2.4f, 0.1f, 1.1f), wood, tb);
            foreach (var lx in new[] { -1.1f, 1.1f })
                foreach (var lz in new[] { -0.45f, 0.45f })
                    Box("Leg", new Vector3(lx, 0.4f, lz), new Vector3(0.1f, 0.8f, 0.1f), wood, tb);
            Box("Paper", new Vector3(-0.5f, 0.86f, 0.1f), new Vector3(0.5f, 0.01f, 0.35f), paper, tb, false);
            Box("Paper2", new Vector3(0.4f, 0.86f, -0.1f), new Vector3(0.4f, 0.015f, 0.3f), paper, tb, false).transform.localRotation = Quaternion.Euler(0, 20, 0);
            // candlestick
            Cyl("Candle", new Vector3(0.9f, 0.95f, 0.3f), new Vector3(0.06f, 0.15f, 0.06f), Mat("Candle", new Color(0.95f, 0.9f, 0.7f)), tb);
            Cyl("Flame", new Vector3(0.9f, 1.13f, 0.3f), new Vector3(0.04f, 0.04f, 0.04f), Mat("Flame", new Color(1f, 0.7f, 0.2f), true), tb);
            PointLight("CandleLight", new Vector3(0.9f, 1.2f, 0.3f), new Color(1f, 0.65f, 0.3f), 2f, 5f, tb);
            // chairs
            foreach (var cz in new[] { -0.95f, 0.95f })
            {
                var ch = new GameObject("Chair").transform; ch.SetParent(tb, false); ch.localPosition = new Vector3(0, 0, cz);
                if (cz > 0) ch.localRotation = Quaternion.Euler(0, 180, 0);
                Box("Seat", new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.06f, 0.5f), chair, ch);
                Box("Back", new Vector3(0, 0.8f, -0.22f), new Vector3(0.5f, 0.7f, 0.05f), chair, ch);
                foreach (var lx in new[] { -0.2f, 0.2f })
                    foreach (var lz in new[] { -0.2f, 0.2f })
                        Box("Leg", new Vector3(lx, 0.22f, lz), new Vector3(0.05f, 0.44f, 0.05f), chair, ch);
            }
        }

        // globe
        var globe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        globe.name = "Globe"; globe.transform.SetParent(root, false);
        globe.transform.localPosition = new Vector3(5f, 1.1f, 5.5f); globe.transform.localScale = Vector3.one * 0.6f;
        globe.GetComponent<Renderer>().sharedMaterial = Mat("Globe", new Color(0.2f, 0.4f, 0.55f));
        Cyl("GlobeStand", new Vector3(5f, 0.5f, 5.5f), new Vector3(0.1f, 0.5f, 0.1f), gold);
        Cyl("GlobeBase", new Vector3(5f, 0.02f, 5.5f), new Vector3(0.6f, 0.02f, 0.6f), gold);
    }

    // =====================================================
    //  LIGHTING
    // =====================================================
    static void BuildLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.22f, 0.17f, 0.14f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.1f, 0.08f, 0.06f);
        RenderSettings.fogDensity = 0.015f;
        RenderSettings.fogMode = FogMode.ExponentialSquared;

        var moon = new GameObject("MoonLight").AddComponent<Light>();
        moon.type = LightType.Directional; moon.color = new Color(0.5f, 0.6f, 1f); moon.intensity = 0.4f;
        moon.transform.rotation = Quaternion.Euler(35, 90, 0);
        moon.transform.SetParent(root, false);

        var lampMat = Mat("LampGlow", new Color(1f, 0.75f, 0.4f), true);
        var chain = Mat("Chain", new Color(0.1f, 0.1f, 0.1f));
        foreach (var z in new[] { -6f, -1.5f, 3f, 7f })
        {
            Cyl("Chain", new Vector3(0, H - 0.4f, z), new Vector3(0.02f, 0.4f, 0.02f), chain);
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "Lamp"; lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(0, H - 0.9f, z); lamp.transform.localScale = Vector3.one * 0.3f;
            lamp.GetComponent<Renderer>().sharedMaterial = lampMat;
            Object.DestroyImmediate(lamp.GetComponent<Collider>());
            PointLight("LampLight", new Vector3(0, H - 1f, z), new Color(1f, 0.75f, 0.45f), 6f, 9f);
        }
    }

    // =====================================================
    //  PLAYER + BOOK INTERACTION
    // =====================================================
    static void BuildPlayerAndBook()
    {
        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = new Vector3(0, 0.1f, -7f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.7f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.85f, 0);

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "PlaceholderBody"; body.transform.SetParent(player.transform, false);
        body.transform.localPosition = new Vector3(0, 0.85f, 0); body.transform.localScale = new Vector3(0.6f, 0.85f, 0.6f);
        body.GetComponent<Renderer>().sharedMaterial = Mat("PlayerBlue", new Color(0.2f, 0.45f, 0.8f));
        Object.DestroyImmediate(body.GetComponent<Collider>());

        var pivot = new GameObject("CamPivot").transform;
        pivot.SetParent(player.transform, false); pivot.localPosition = new Vector3(0, 1.6f, 0);

        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        camGo.AddComponent<AudioListener>();

        var pc = player.AddComponent<PlayerController>();
        pc.camPivot = pivot; pc.cam = cam;

        if (heroRoot != null)
        {
            var bi = heroRoot.gameObject.AddComponent<BookInteraction>();
            bi.player = player.transform; bi.glow = heroGlow; bi.bookVisual = heroVisual;
        }
        else
        {
            Debug.LogWarning("Hero book was not placed!");
        }
    }
}