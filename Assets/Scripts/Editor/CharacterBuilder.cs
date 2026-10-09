using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CharacterBuilder
{
    const string MatDir = "Assets/Generated/Materials";

    [MenuItem("Mystra/Replace Player With Aarav")]
    public static void ReplacePlayer()
    {
        var player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("No Player found. Open the Library scene first.");
            return;
        }
        var old = player.transform.Find("PlaceholderBody");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var old2 = player.transform.Find("AaravModel");
        if (old2 != null) Object.DestroyImmediate(old2.gameObject);

        var model = MakePerson("AaravModel",
            new Color(0.2f, 0.45f, 0.8f),   // shirt
            new Color(0.15f, 0.15f, 0.25f), // pants
            new Color(0.85f, 0.65f, 0.5f),  // skin
            new Color(0.08f, 0.05f, 0.04f), // hair
            0.9f, true);
        model.transform.SetParent(player.transform, false);
        model.transform.localPosition = Vector3.zero;
        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log("Aarav is ready! Press Play.");
    }

    [MenuItem("Mystra/Add Friends To Scene")]
    public static void AddFriends()
    {
        var old = GameObject.Find("Friends");
        if (old != null) Object.DestroyImmediate(old);
        var parent = new GameObject("Friends").transform;

        AddFriend(parent, "Friend1", new Vector3(-1.5f, 0f, -5f), 160f,
            new Color(0.75f, 0.15f, 0.15f), new Color(0.2f, 0.2f, 0.3f),
            new Color(0.8f, 0.6f, 0.45f), new Color(0.05f, 0.04f, 0.03f));
        AddFriend(parent, "Friend2", new Vector3(-6f, 0f, -0.5f), 270f,
            new Color(0.15f, 0.55f, 0.25f), new Color(0.25f, 0.2f, 0.15f),
            new Color(0.9f, 0.7f, 0.55f), new Color(0.3f, 0.18f, 0.08f));
        AddFriend(parent, "Friend3", new Vector3(2.5f, 0f, -5f), 200f,
            new Color(0.9f, 0.75f, 0.2f), new Color(0.15f, 0.2f, 0.35f),
            new Color(0.7f, 0.5f, 0.38f), new Color(0.05f, 0.04f, 0.03f));

        EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
        Debug.Log("Friends added! Press Play.");
    }

    static void AddFriend(Transform parent, string name, Vector3 pos, float rotY,
        Color shirt, Color pants, Color skin, Color hair)
    {
        var p = MakePerson(name, shirt, pants, skin, hair, 0.9f, false);
        p.transform.SetParent(parent, false);
        p.transform.position = pos;
        p.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
    }

    static GameObject MakePerson(string name, Color shirt, Color pants, Color skin,
        Color hair, float scale, bool backpack)
    {
        var root = new GameObject(name);
        var t = root.transform;

        var mShirt = Mat("C_" + name + "_Shirt", shirt);
        var mPants = Mat("C_" + name + "_Pants", pants);
        var mSkin = Mat("C_" + name + "_Skin", skin);
        var mHair = Mat("C_" + name + "_Hair", hair);
        var mShoe = Mat("C_Shoe", new Color(0.12f, 0.1f, 0.1f));
        var mEye = Mat("C_Eye", Color.black);
        var mPack = Mat("C_Backpack", new Color(0.6f, 0.2f, 0.1f));

        // legs and shoes
        foreach (float x in new[] { -0.1f, 0.1f })
        {
            Part(PrimitiveType.Cylinder, "Leg", t, new Vector3(x, 0.38f, 0), new Vector3(0.16f, 0.32f, 0.16f), mPants);
            Part(PrimitiveType.Cube, "Shoe", t, new Vector3(x, 0.04f, 0.04f), new Vector3(0.18f, 0.08f, 0.28f), mShoe);
        }
        // torso
        Part(PrimitiveType.Capsule, "Torso", t, new Vector3(0, 1.0f, 0), new Vector3(0.38f, 0.3f, 0.24f), mShirt);
        // arms and hands
        foreach (float x in new[] { -0.27f, 0.27f })
        {
            Part(PrimitiveType.Cylinder, "Arm", t, new Vector3(x, 1.02f, 0), new Vector3(0.1f, 0.26f, 0.1f), mShirt);
            Part(PrimitiveType.Sphere, "Hand", t, new Vector3(x, 0.74f, 0), new Vector3(0.1f, 0.1f, 0.1f), mSkin);
        }
        // head, hair, eyes
        Part(PrimitiveType.Sphere, "Head", t, new Vector3(0, 1.5f, 0), new Vector3(0.3f, 0.32f, 0.3f), mSkin);
        Part(PrimitiveType.Sphere, "Hair", t, new Vector3(0, 1.58f, -0.015f), new Vector3(0.325f, 0.24f, 0.33f), mHair);
        foreach (float x in new[] { -0.055f, 0.055f })
            Part(PrimitiveType.Sphere, "Eye", t, new Vector3(x, 1.52f, 0.135f), new Vector3(0.04f, 0.04f, 0.04f), mEye);

        if (backpack)
            Part(PrimitiveType.Cube, "Backpack", t, new Vector3(0, 1.0f, -0.2f), new Vector3(0.3f, 0.4f, 0.15f), mPack);

        t.localScale = Vector3.one * scale;
        return root;
    }

    static GameObject Part(PrimitiveType type, string name, Transform parent,
        Vector3 pos, Vector3 size, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static Material Mat(string name, Color c)
    {
        Directory.CreateDirectory(MatDir);
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}