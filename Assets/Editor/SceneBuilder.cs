using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lockdown.EditorTools {

/// <summary>
/// Builds Level01 from code (plan v2 section 9/13). Hand-authoring Unity's scene and prefab
/// YAML is fragile; generating it is reproducible and runs headlessly.
/// Re-runnable: nuke the scene, rebuild, save.
/// </summary>
public static class SceneBuilder {
    const string ScenePath  = "Assets/Scenes/Level01.unity";
    const string PrefabDir  = "Assets/Prefabs";

    static readonly Color BG      = new(0.067f, 0.067f, 0.067f);
    static readonly Color WallCol = new(0.27f, 0.27f, 0.27f);

    [MenuItem("LOCKDOWN/Rebuild Level01")]
    public static void Build() {
        SpriteFactory.GenerateAll();
        ConfigurePhysics();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildCamera();
        BuildWalls();
        // No pillars: Harrison's level has none, so nothing builds them.

        Directory.CreateDirectory(PrefabDir);
        Bullet enemyBullet  = MakeBulletPrefab("EnemyBullet",  true);
        Bullet playerBullet = MakeBulletPrefab("PlayerBullet", false);
        Orb    orbPrefab    = MakeOrbPrefab();

        var player  = BuildPlayer(playerBullet);
        BuildTurrets(enemyBullet);
        BuildCrawler();
        var zones   = BuildZones();
        var systems = BuildSystems(orbPrefab, zones);
        BuildHUD(systems, player);

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LOCKDOWN] Level01 built.");
    }

    /// <summary>CLI entry: -executeMethod Lockdown.EditorTools.SceneBuilder.BuildFromCLI</summary>
    public static void BuildFromCLI() {
        Build();
        EditorApplication.Exit(0);
    }

    // ------------------------------------------------------------ physics matrix

    /// <summary>Plan v2 section 13. Two cells differ from v1: PlayerBullet x EnemyBullet
    /// (v1's matrix forbade what section 9 required) and PlayerBullet x Wall (decision C3).</summary>
    static void ConfigurePhysics() {
        int P = L("Player"), E = L("Enemy"), EB = L("EnemyBullet"),
            PB = L("PlayerBullet"), W = L("Wall"), O = L("Orb");

        (int a, int b, bool collide)[] pairs = {
            (P, P,  false), (P, E,  true ), (P, EB, true ), (P, PB, false), (P, W, true ), (P, O, true ),
            (E, E,  false), (E, EB, false), (E, PB, true ), (E, W,  true ), (E, O, false),
            (EB, EB, false), (EB, PB, true ), (EB, W, true ), (EB, O, false),
            (PB, PB, false), (PB, W, true ), (PB, O, false),
            (W, W, false), (W, O, false),
            (O, O, false),
        };
        foreach (var (a, b, c) in pairs) Physics2D.IgnoreLayerCollision(a, b, !c);

        Physics2D.gravity = Vector2.zero;   // top-down: no gravity anywhere
    }

    static int L(string n) => LayerMask.NameToLayer(n);

    // ------------------------------------------------------------ scene pieces

    static void BuildCamera() {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        // Camera sits ABOVE centre, so the arena renders lower in frame and leaves a wide
        // band across the top for the HUD. Bottom wall still clears the lower edge.
        go.transform.position = new Vector3(0, 0.7f, -10);
        var cam = go.AddComponent<Camera>();
        cam.orthographic     = true;
        // Half-height 7.2 against a 6-unit play area. Size 6 framed the PLAY AREA exactly,
        // which meant the walls - which sit outside it - were entirely off-screen.
        // Combined with the +0.7 offset: visible y is -6.5 .. 7.9, so the bottom wall clears
        // the edge by 0.1 and there is a 1.5-unit band above the top wall for the HUD.
        cam.orthographicSize = 7.2f;
        cam.backgroundColor  = BG;
        cam.clearFlags       = CameraClearFlags.SolidColor;
        go.AddComponent<CameraShake>();
        go.AddComponent<AudioListener>();
    }

    /// <summary>
    /// Walls sit just outside the 20x12 play area, with their INNER faces on the boundary
    /// (x = +/-10, y = +/-6).
    ///
    /// They OVERLAP at the corners on purpose - four strips meeting at a zero-width seam let a
    /// shallow-angle bullet slip straight through (plan v2 section 9).
    /// </summary>
    static void BuildWalls() {
        const float T = 0.4f;               // thickness: was 1.0, which read as a slab
        const float HX = 10f, HY = 6f;      // play-area half-extents
        var root = new GameObject("Walls").transform;
        Wall(root, "Top",    new Vector2(0,  HY + T/2), new Vector2(2*HX + 2*T, T));
        Wall(root, "Bottom", new Vector2(0, -HY - T/2), new Vector2(2*HX + 2*T, T));
        Wall(root, "Left",   new Vector2(-HX - T/2, 0), new Vector2(T, 2*HY + 2*T));
        Wall(root, "Right",  new Vector2( HX + T/2, 0), new Vector2(T, 2*HY + 2*T));
    }

    static void Wall(Transform parent, string name, Vector2 pos, Vector2 size) {
        var go = Quad(name, pos, size, WallCol, 2);
        go.transform.SetParent(parent);
        go.layer = L("Wall");
        go.AddComponent<BoxCollider2D>();
    }

    static PlayerController BuildPlayer(Bullet playerBullet) {
        var go = new GameObject("Player") { layer = L("Player") };
        go.transform.position = new Vector3(0, 0, 0);   // Harrison: start at the arena centre
        // Body size. Everything on the player - fins, aim dot, collider - is a child or a
        // local radius, so they all scale from this one number. Tune here, not in six places.
        const float PS = 0.6f;
        go.transform.localScale = Vector3.one * PS;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Load("square");
        sr.color = Color.white;
        sr.sortingOrder = 10;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;     // decision A4 - Kinematic walks through walls
        rb.gravityScale = 0f;                      // Unity's Dynamic default is 1
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        go.AddComponent<CircleCollider2D>().radius = 0.5f;   // 0.3 world radius at scale 0.6

        var fins = new SpriteRenderer[4];
        Vector2[] finPos = { new(0, 0.55f), new(0, -0.55f), new(-0.55f, 0), new(0.55f, 0) };
        for (int i = 0; i < 4; i++) {
            var f = Quad($"Fin{(Direction)i}", Vector2.zero, Vector2.one * 0.375f,
                         OrbSpawner.OrbColors[i], 11);
            f.transform.SetParent(go.transform);
            f.transform.localPosition = finPos[i];
            fins[i] = f.GetComponent<SpriteRenderer>();
        }

        var dot = Quad("AimDot", Vector2.zero, Vector2.one * 0.19f, new Color(0.2f, 0.87f, 1f), 12);
        dot.GetComponent<SpriteRenderer>().sprite = SpriteFactory.Load("circle");
        // worldPositionStays:false is load-bearing. Quad() creates this at world origin, and
        // the default SetParent PRESERVES world position - which left the aim dot sitting at
        // (0,0) in the middle of the arena instead of on the player.
        dot.transform.SetParent(go.transform, false);

        var fire = new GameObject("FirePoint");
        fire.transform.SetParent(go.transform);
        fire.transform.localPosition = Vector3.zero;

        var pc = go.AddComponent<PlayerController>();
        pc.fins = fins;
        pc.aimDot = dot.transform;

        var ps = go.AddComponent<PlayerShooting>();
        ps.bulletPrefab = playerBullet;
        ps.firePoint = fire.transform;

        go.AddComponent<PlayerHealth>();
        return pc;
    }

    static void BuildTurrets(Bullet enemyBullet) {
        // Turret body is 0.6 (was 1.0). Positions and firing offsets below are Harrison's.
        const float TS = 0.6f;
        var root = new GameObject("Turrets").transform;
        // Harrison's layout: three corners plus a fourth, bottom-right. T4's x is 7.89, not 8 -
        // that is where he dragged it in the editor, and it is kept as he set it.
        Vector2[] at  = { new(-8, 5), new(8, 5), new(-8, -5), new(7.89f, -5) };
        // Firing offsets so the four never volley together: T4 first, then T1, T2, T3 at 0.45s
        // steps. The largest must stay under the fire gap (floor 1.5s) or they re-sync.
        float[] phase = { 0.45f, 0.9f, 1.35f, 0f };
        for (int i = 0; i < at.Length; i++) {
            var go = Quad($"T{i + 1}", at[i], Vector2.one * TS, new Color(1f, 0.33f, 0.2f), 6);
            go.transform.SetParent(root);
            go.layer = L("Enemy");
            go.AddComponent<BoxCollider2D>();

            // The barrel is parented with world scale preserved, so this IS its world size:
            // 0.9 x 0.22, longer than the 0.6 body so the aim direction still reads.
            var barrel = Quad("Barrel", Vector2.zero, new Vector2(0.9f, 0.22f),
                              new Color(0.67f, 0.13f, 0f), 7);
            barrel.transform.SetParent(go.transform);
            barrel.transform.localPosition = Vector3.zero;

            var tip = new GameObject("BarrelTip");
            tip.transform.SetParent(barrel.transform);
            tip.transform.localPosition = new Vector3(1.1f, 0, 0);

            // The core IS the 0.5s telegraph. It is parented with worldPositionStays:false, so
            // this 0.45 is multiplied by the turret's 0.6 scale (about 0.27 in the world).
            var core = Quad("Core", Vector2.zero, Vector2.one * 0.45f, new Color(1f, 0.53f, 0.33f), 8);
            core.GetComponent<SpriteRenderer>().sprite = SpriteFactory.Load("circle");
            core.transform.SetParent(go.transform, false);   // see AimDot: keep local (0,0)

            var t = go.AddComponent<Turret>();
            t.bulletPrefab = enemyBullet;
            t.barrel    = barrel.transform;
            t.barrelTip = tip.transform;
            t.core      = core.GetComponent<SpriteRenderer>();
            t.body      = go.GetComponent<SpriteRenderer>();
            t.firePhase = phase[i];
        }
    }

    static void BuildCrawler() {
        // Starts at the left end of the full-arena S-curve (see Crawler.cs).
        var go = Quad("Crawler", new Vector2(-9, 0), Vector2.one * 0.8f, new Color(1f, 0.2f, 0.67f), 6);
        go.GetComponent<SpriteRenderer>().sprite = SpriteFactory.Load("triangle");
        go.layer = L("Enemy");
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;

        var c = go.AddComponent<Crawler>();
        c.body = go.GetComponent<SpriteRenderer>();
    }

    static Transform[] BuildZones() {
        var root = new GameObject("OrbSpawnZones").transform;
        Vector2[] at = { new(-8, 0), new(8, 0), new(-3, -4), new(3, -4),
                         new(-3, 4), new(3, 4), new(-8, -4), new(8, -4) };
        var zones = new Transform[8];
        for (int i = 0; i < at.Length; i++) {
            var z = new GameObject($"Z{i + 1}");
            z.transform.SetParent(root);
            z.transform.position = at[i];
            zones[i] = z.transform;
        }
        return zones;
    }

    static GameObject BuildSystems(Orb orbPrefab, Transform[] zones) {
        var go = new GameObject("Systems");
        go.AddComponent<LevelManager>();
        go.AddComponent<DirectionSystem>();
        var spawner = go.AddComponent<OrbSpawner>();
        spawner.orbPrefab = orbPrefab;
        spawner.zones = zones;
        return go;
    }

    static void BuildHUD(GameObject systems, PlayerController player) {
        var canvasGO = new GameObject("HUD Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

        var hud = systems.AddComponent<HUDController>();

        // No direction arrows: the coloured fins on the ship already show which directions
        // are live, and they are where the player is already looking.

        // The HUD lives in the band ABOVE the play area - the strip the player can never
        // enter, so it covers nothing that matters. Timer top-left, hearts top-right, matched
        // sizes so they read as one row.
        //
        // Each is anchored AND pivoted on its own corner, so the text grows inward from the
        // edge and cannot crop however the window is resized.
        const int hudSize = 48;
        const float hudY  = -34f, hudX = 48f;

        hud.hearts = Label(canvas.transform, "Hearts", "♥♥♥♥♥", hudSize,
                           new Vector2(1, 1), new Vector2(-hudX, hudY),
                           TextAnchor.UpperRight, Color.red, new Vector2(1, 1));
        hud.timer  = Label(canvas.transform, "Timer", "1:00", hudSize,
                           new Vector2(0, 1), new Vector2(hudX, hudY),
                           TextAnchor.UpperLeft, Color.white, new Vector2(0, 1));
        hud.banner = Label(canvas.transform, "Banner", "", 120,
                           new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter, Color.white);
    }

    static Text Label(Transform parent, string name, string text, int size,
                      Vector2 anchor, Vector2 pos, TextAnchor align, Color color,
                      Vector2? pivot = null) {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        // Pivot defaults to centre, but a CORNER-anchored label must pivot on that same
        // corner. Otherwise half its box hangs past the screen edge and the text crops -
        // which is exactly what happened to the hearts when they were top-right before.
        rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(600, 140);
        return t;
    }

    // ------------------------------------------------------------ prefabs

    static Bullet MakeBulletPrefab(string name, bool enemy) {
        var go = Quad(name, Vector2.zero, Vector2.one * (enemy ? 0.15f : 0.15f),
                      enemy ? new Color(1f, 0.2f, 0.33f) : new Color(0.2f, 0.87f, 1f), 12);
        go.GetComponent<SpriteRenderer>().sprite = SpriteFactory.Load("circle");
        go.layer = enemy ? L("EnemyBullet") : L("PlayerBullet");

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;          // 0.075 world at scale 0.15
        col.isTrigger = true;

        var b = go.AddComponent<Bullet>();
        b.isEnemyBullet = enemy;
        if (!enemy) { b.freshSpeed = 10f; b.bouncedSpeed = 9f; b.freshLifetime = 3f; b.bouncedLifetime = 2f; }

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
        Object.DestroyImmediate(go);
        return prefab.GetComponent<Bullet>();
    }

    static Orb MakeOrbPrefab() {
        var go = Quad("Orb", Vector2.zero, Vector2.one * 0.8f, Color.white, 8);
        go.GetComponent<SpriteRenderer>().sprite = SpriteFactory.Load("ring");
        go.layer = L("Orb");

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;
        col.isTrigger = true;

        var icon = Quad("Icon", Vector2.zero, Vector2.one * 0.45f, Color.white, 9);
        icon.GetComponent<SpriteRenderer>().sprite = SpriteFactory.Load("triangle");
        icon.transform.SetParent(go.transform);

        var ring = Quad("CountdownRing", Vector2.zero, Vector2.one * 1.15f, Color.white, 7);
        ring.GetComponent<SpriteRenderer>().sprite = SpriteFactory.Load("ring");
        ring.transform.SetParent(go.transform);

        var orb = go.AddComponent<Orb>();
        orb.countdownRing = ring.transform;
        orb.ringRenderer  = go.GetComponent<SpriteRenderer>();
        orb.iconRenderer  = icon.GetComponent<SpriteRenderer>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/Orb.prefab");
        Object.DestroyImmediate(go);
        return prefab.GetComponent<Orb>();
    }

    // ------------------------------------------------------------ helper

    static GameObject Quad(string name, Vector2 pos, Vector2 size, Color color, int order) {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.localScale = size;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Load("square");
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }
}
}
