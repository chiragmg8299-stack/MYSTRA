using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class GothicLibraryBuilder
{
    const float R = 9f;        // hall radius
    const float LEVEL = 4f;    // height of one floor
    const int SEG = 36;        // segments around the circle
    static Transform root;

    [MenuItem("Mystra/Build Gothic Library Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        System.IO.Directory.CreateDirectory("Assets/Generated/Materials");
        Random.InitState(1520);

        root = new GameObject("GothicLibrary").transform;

        BuildFloorAndCeiling();
        BuildWalls();
        BuildBalconies();
        BuildStairs();
        BuildCenterTower();
        BuildFurniture();
        BuildLighting();
        BuildBookAndPlayer();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Scenes/GothicLibrary.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("Gothic library built! Press Play.");
    }

    // ---------- helpers ----------
    static Material Mat(string name, Color c, bool emissive = false, float smooth = 0.2f)
    {
        string path = "Assets/Generated/Materials/G_" + name + ".mat";
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

    static Vector3 Polar(float r, float deg, float y)
    {
        float a = deg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
    }

    // size = (radial, height, tangential) when rotY = -angle
    static GameObject Box(string name, Vector3 pos, Vector3 size, Material mat, float rotY = 0f, bool collider = true, Transform parent = null)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent ? parent : root, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0, rotY, 0);
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static GameObject Cyl(string name, Vector3 pos, Vector3 size, Material mat, bool collider = true)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        g.transform.SetParent(root, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size; // cylinder: x,z = diameter, y = half height
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
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

    // ---------- floor and ceiling ----------
    static void BuildFloorAndCeiling()
    {
        var floor = Mat("Floor", new Color(0.35f, 0.25f, 0.18f), false, 0.4f);
        var ring = Mat("FloorRing", new Color(0.55f, 0.45f, 0.3f), false, 0.4f);
        var ceil = Mat("Ceiling", new Color(0.55f, 0.42f, 0.2f));
        var glass = Mat("DomeGlass", new Color(0.7f, 0.75f, 1f), true);
        var spoke = Mat("Spoke", new Color(0.1f, 0.1f, 0.12f));

        Cyl("Floor", new Vector3(0, -0.1f, 0), new Vector3(19f, 0.1f, 19f), floor);
        Cyl("FloorRing", new Vector3(0, 0.005f, 0), new Vector3(6f, 0.01f, 6f), ring, false);

        float top = LEVEL * 3f;
        Cyl("Ceiling", new Vector3(0, top + 0.15f, 0), new Vector3(19.6f, 0.15f, 19.6f), ceil);
        Cyl("DomeGlass", new Vector3(0, top - 0.02f, 0), new Vector3(8f, 0.05f, 8f), glass, false);
        for (int i = 0; i < 16; i++)
        {
            float deg = i * 22.5f;
            Box("Spoke", Polar(2f, deg, top - 0.06f), new Vector3(4f, 0.05f, 0.1f), spoke, -deg, false);
        }
        // coloured panels in the dome
        Color[] cols = { new Color(1f, 0.5f, 0.7f), new Color(1f, 0.9f, 0.4f), new Color(0.4f, 0.7f, 1f) };
        for (int i = 0; i < 24; i++)
        {
            float deg = i * 15f + 7f;
            var m = Mat("DomePanel" + (i % 3), cols[i % 3], true);
            Box("DomePanel", Polar(3f, deg, top - 0.08f), new Vector3(1.2f, 0.03f, 0.5f), m, -deg, false);
        }
    }

    // ---------- curved wall of bookshelves, pillars and windows ----------
    static void BuildWalls()
    {
        var stone = Mat("Stone", new Color(0.55f, 0.55f, 0.55f));
        var wood = Mat("ShelfWood", new Color(0.3f, 0.17f, 0.08f), false, 0.3f);
        var gold = Mat("Brass", new Color(0.85f, 0.65f, 0.25f), true, 0.7f);
        var bookMats = new Material[]
        {
            Mat("BookRed1", new Color(0.45f, 0.1f, 0.08f)),
            Mat("BookRed2", new Color(0.55f, 0.15f, 0.1f)),
            Mat("BookRed3", new Color(0.35f, 0.06f, 0.08f)),
            Mat("BookBrown", new Color(0.4f, 0.22f, 0.1f)),
            Mat("BookBlue", new Color(0.15f, 0.18f, 0.4f)),
        };
        Color[] winCols = { new Color(1f, 0.6f, 0.75f), new Color(1f, 0.9f, 0.45f), new Color(0.5f, 0.75f, 1f) };

        float wallWidth = 2f * Mathf.PI * 8.75f / SEG * 1.04f;

        for (int level = 0; level < 3; level++)
        {
            float baseY = level * LEVEL;
            for (int i = 0; i < SEG; i++)
            {
                float deg = i * 360f / SEG;
                bool isDoor = (level == 0 && i == 27);
                bool isWindow = (i % 6 == 3);

                if (isDoor)
                {
                    Box("DoorWall", Polar(8.75f, deg, baseY + 1.85f), new Vector3(0.5f, 3.7f, wallWidth), stone, -deg);
                    Box("Gate", Polar(8.45f, deg, baseY + 1.3f), new Vector3(0.1f, 2.6f, 1.2f), gold, -deg, false);
                    continue;
                }

                if (isWindow)
                {
                    Box("PillarWall", Polar(8.75f, deg, baseY + 1.85f), new Vector3(0.5f, 3.7f, wallWidth), stone, -deg);
                    var gm = Mat("Win" + level, winCols[level], true);
                    Box("Window", Polar(8.45f, deg, baseY + 1.9f), new Vector3(0.05f, 1.9f, 0.9f), gm, -deg, false);
                    Box("WindowTop", Polar(8.45f, deg, baseY + 2.95f), new Vector3(0.05f, 0.4f, 0.5f), gm, -deg, false);
                    continue;
                }

                Box("ShelfBack", Polar(8.75f, deg, baseY + 1.85f), new Vector3(0.5f, 3.7f, wallWidth), wood, -deg);
                for (int k = 0; k < 5; k++)
                {
                    var bm = bookMats[Random.Range(0, bookMats.Length)];
                    Box("BookRow", Polar(8.45f, deg, baseY + 0.45f + k * 0.65f), new Vector3(0.3f, 0.5f, wallWidth * 0.95f), bm, -deg, false);
                    Box("Plank", Polar(8.45f, deg, baseY + 0.15f + k * 0.65f), new Vector3(0.34f, 0.04f, wallWidth), wood, -deg, false);
                }
            }
        }
    }

    // ---------- curved balconies with railings ----------
    static void BuildBalconies()
    {
        var wood = Mat("BalconyWood", new Color(0.4f, 0.22f, 0.1f), false, 0.35f);
        float tang = 2f * Mathf.PI * 7.9f / SEG * 1.06f;

        for (int level = 1; level <= 2; level++)
        {
            float y = level * LEVEL;
            for (int i = 0; i < SEG; i++)
            {
                float deg = i * 360f / SEG;
                Box("Balcony", Polar(7.9f, deg, y - 0.15f), new Vector3(2.2f, 0.3f, tang), wood, -deg);

                bool gap = (level == 1 && i == 9) || (level == 2 && i == 18);
                if (gap) continue;

                Box("RailTop", Polar(6.85f, deg, y + 1.0f), new Vector3(0.12f, 0.07f, tang), wood, -deg, false);
                for (int b = -1; b <= 1; b++)
                {
                    float d2 = deg + b * 3f;
                    Box("Baluster", Polar(6.85f, d2, y + 0.5f), new Vector3(0.05f, 1.0f, 0.05f), wood, -d2, true);
                }
            }
        }
    }

    // ---------- curved staircase: ground -> floor 1 -> floor 2 ----------
    static void BuildStairs()
    {
        var wood = Mat("StairWood", new Color(0.42f, 0.24f, 0.11f), false, 0.35f);
        Stairs(0f, 90f, 0f, LEVEL, 24, wood);
        Stairs(90f, 180f, LEVEL, LEVEL * 2f, 24, wood);
    }

    static void Stairs(float startDeg, float endDeg, float y0, float y1, int steps, Material mat)
    {
        float radius = 6.2f;
        float tread = (2f * Mathf.PI * radius * (endDeg - startDeg) / 360f) / steps * 1.06f;
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float deg = Mathf.Lerp(startDeg, endDeg, t - 0.5f / steps);
            float top = Mathf.Lerp(y0, y1, t);
            float thick = Mathf.Min(top, 0.3f);
            Box("Step", Polar(radius, deg, top - thick / 2f), new Vector3(1.2f, thick, tread), mat, -deg);
        }
    }

    // ---------- tall bookshelf tower in the middle ----------
    static void BuildCenterTower()
    {
        var books = Mat("TowerBooks", new Color(0.45f, 0.12f, 0.08f));
        var wood = Mat("ShelfWood", new Color(0.3f, 0.17f, 0.08f), false, 0.3f);
        Cyl("BookTower", new Vector3(0, 5.5f, 0), new Vector3(2.6f, 5.5f, 2.6f), books);
        for (int i = 0; i < 8; i++)
            Cyl("TowerBand", new Vector3(0, 0.3f + i * 1.4f, 0), new Vector3(2.75f, 0.04f, 2.75f), wood, false);
        Cyl("TowerCap", new Vector3(0, 11f, 0), new Vector3(3.2f, 0.1f, 3.2f), wood);
    }

    // ---------- tables and chairs ----------
    static void BuildFurniture()
    {
        var wood = Mat("TableWood", new Color(0.35f, 0.2f, 0.1f), false, 0.4f);
        var chair = Mat("RedChair", new Color(0.6f, 0.1f, 0.12f));

        foreach (float sx in new[] { -3.3f, 3.3f })
        {
            Box("TableTop", new Vector3(sx, 0.8f, -1f), new Vector3(1.1f, 0.1f, 3.2f), wood);
            foreach (float lx in new[] { -0.45f, 0.45f })
                foreach (float lz in new[] { -1.4f, 1.4f })
                    Box("TableLeg", new Vector3(sx + lx, 0.4f, -1f + lz), new Vector3(0.1f, 0.8f, 0.1f), wood);

            for (int c = 0; c < 3; c++)
            {
                float z = -2f + c * 1f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Box("Seat", new Vector3(sx + side * 0.95f, 0.45f, z), new Vector3(0.45f, 0.06f, 0.45f), chair);
                    Box("ChairBack", new Vector3(sx + side * 1.17f, 0.85f, z), new Vector3(0.05f, 0.8f, 0.45f), chair);
                }
            }
        }
    }

    // ---------- lighting ----------
    static void BuildLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.28f, 0.22f, 0.2f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.15f, 0.12f, 0.1f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.01f;

        var sun = new GameObject("DomeLight").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.92f, 0.8f);
        sun.intensity = 0.7f;
        sun.shadows = LightShadows.None;
        sun.transform.rotation = Quaternion.Euler(80, 0, 0);
        sun.transform.SetParent(root, false);

        // lights on each floor
        for (int level = 0; level < 3; level++)
            for (int i = 0; i < 4; i++)
                PointLight("FloorLight", Polar(6f, i * 90f + 45f, level * LEVEL + 3f), new Color(1f, 0.8f, 0.55f), 5f, 11f);

        // chandeliers
        var gold = Mat("Brass", new Color(0.85f, 0.65f, 0.25f), true, 0.7f);
        var chain = Mat("Chain", new Color(0.15f, 0.12f, 0.08f));
        foreach (float deg in new[] { 30f, 150f, 270f })
        {
            Vector3 p = Polar(4.5f, deg, 0);
            Cyl("ChandelierChain", new Vector3(p.x, 11f, p.z), new Vector3(0.03f, 1.0f, 0.03f), chain, false);
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Chandelier";
            ball.transform.SetParent(root, false);
            ball.transform.localPosition = new Vector3(p.x, 9.8f, p.z);
            ball.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
            ball.GetComponent<Renderer>().sharedMaterial = gold;
            Object.DestroyImmediate(ball.GetComponent<Collider>());
            PointLight("ChandelierLight", new Vector3(p.x, 9.6f, p.z), new Color(1f, 0.8f, 0.5f), 6f, 12f);
        }
    }

    // ---------- glowing book + player ----------
    static void BuildBookAndPlayer()
    {
        var stone = Mat("Pedestal", new Color(0.55f, 0.55f, 0.55f));
        var cover = Mat("AncientCover", new Color(0.3f, 0.1f, 0.05f), false, 0.5f);
        var pages = Mat("AncientPages", new Color(0.9f, 0.82f, 0.6f));
        var gold = Mat("Brass", new Color(0.85f, 0.65f, 0.25f), true, 0.7f);

        var bookRoot = new GameObject("MysteriousBook");
        bookRoot.transform.SetParent(root, false);
        bookRoot.transform.localPosition = new Vector3(0, 0, 4.8f);

        Cyl("Pedestal", new Vector3(0, 0.55f, 0), new Vector3(0.7f, 0.55f, 0.7f), stone).transform.SetParent(bookRoot.transform, false);
        Cyl("PedestalTop", new Vector3(0, 1.12f, 0), new Vector3(0.9f, 0.04f, 0.9f), stone).transform.SetParent(bookRoot.transform, false);

        var vis = new GameObject("BookVisual").transform;
        vis.SetParent(bookRoot.transform, false);
        vis.localPosition = new Vector3(0, 1.45f, 0);
        Box("CoverBottom", new Vector3(0, -0.04f, 0), new Vector3(0.45f, 0.03f, 0.6f), cover, 0, false, vis);
        Box("Pages", new Vector3(0, 0, 0), new Vector3(0.42f, 0.06f, 0.57f), pages, 0, false, vis);
        Box("CoverTop", new Vector3(0, 0.04f, 0), new Vector3(0.45f, 0.03f, 0.6f), cover, 0, false, vis);
        Box("Spine", new Vector3(-0.22f, 0, 0), new Vector3(0.04f, 0.12f, 0.6f), cover, 0, false, vis);
        Box("Emblem", new Vector3(0, 0.06f, 0), new Vector3(0.18f, 0.01f, 0.18f), gold, 45, false, vis);

        var glow = PointLight("BookGlow", new Vector3(0, 1.7f, 0), new Color(1f, 0.8f, 0.3f), 3f, 6f, bookRoot.transform);

        var trigger = bookRoot.AddComponent<BoxCollider>();
        trigger.isTrigger = true; trigger.center = new Vector3(0, 1f, 0); trigger.size = new Vector3(1, 2, 1);

        // player
        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = new Vector3(0, 0.1f, -6f);
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

        var bi = bookRoot.AddComponent<BookInteraction>();
        bi.player = player.transform; bi.glow = glow; bi.bookVisual = vis;
    }
}