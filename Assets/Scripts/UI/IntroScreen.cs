using UnityEngine;
using UnityEngine.UI;

namespace Lockdown {

/// <summary>
/// Instruction page shown once at launch. The arena is frozen behind it (GameState.Intro)
/// until the player presses a key.
///
/// Deliberately plain: title, what each thing in the arena looks like (drawn with the
/// game's own sprites, exactly as they appear in play), then CONTROLS / RULES / GOAL as text
/// in simple outlined boxes. The only motion is a fade in and a fade out.
///
/// Built in code so the scene file never needs hand-editing.
/// </summary>
public class IntroScreen : MonoBehaviour {
    static readonly Color Ink    = new(0.93f, 0.95f, 0.98f);
    static readonly Color Muted  = new(0.62f, 0.66f, 0.74f);
    static readonly Color Blue   = new(0.40f, 0.72f, 1.00f);
    static readonly Color Red    = new(1.00f, 0.36f, 0.36f);
    static readonly Color Green  = new(0.40f, 1.00f, 0.68f);
    static readonly Color Gold   = new(1.00f, 0.84f, 0.36f);
    static readonly Color Pink   = new(1.00f, 0.20f, 0.67f);   // the crawler
    static readonly Color Turret = new(1.00f, 0.33f, 0.20f);   // the turrets

    const float FadeIn = 0.6f, ReadyAt = 0.8f, FadeOut = 0.3f;

    Font _font;
    Sprite _circle, _ring, _tri;                       // squares need no sprite: a plain Image
    readonly System.Collections.Generic.List<Texture2D> _made = new();
    GameObject _canvas;
    CanvasGroup _group;
    float _t0, _closingAt = -1f;

    void Start() {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        MakeSprites();
        Build();
        _group.alpha = 0f;
        _t0 = Time.unscaledTime;
    }

    void Update() {
        if (_closingAt < 0f) {
            float t = Time.unscaledTime - _t0;
            _group.alpha = Mathf.Clamp01(t / FadeIn);
            if (t > ReadyAt && Input.anyKeyDown) {
                _closingAt = Time.unscaledTime;
                if (LevelManager.I != null) LevelManager.I.BeginRun();
            }
        } else {
            float k = Mathf.Clamp01((Time.unscaledTime - _closingAt) / FadeOut);
            _group.alpha = 1f - k;
            if (k >= 1f) { Destroy(_canvas); Destroy(this); }
        }
    }

    // ------------------------------------------------------------------ layout

    void Build() {
        _canvas = new GameObject("Intro Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        _canvas.transform.SetParent(transform, false);
        var canvas = _canvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;                                    // above the HUD
        canvas.pixelPerfect = true;                                   // crisper text and edges
        var scaler = _canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        _group = _canvas.GetComponent<CanvasGroup>();
        var root = (RectTransform)_canvas.transform;

        var bg = Img(root, "Backdrop", Vector2.zero, null, new Color(0.02f, 0.025f, 0.04f, 1f));
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one;
        bg.offsetMin = bg.offsetMax = Vector2.zero;

        Label(root, "Deadlock: Don’t Lose Your Move.", 88, new Vector2(0, 405), new Vector2(1800, 120), Ink, FontStyle.Bold);
        Label(root, "Get hit, lose a direction. Grab the orb to get it back.", 32,
              new Vector2(0, 318), new Vector2(1600, 50), Muted);

        BuildRoster(root);

        const float W = 540f, H = 350f, Y = -165f;
        Box(root, "CONTROLS", new Vector2(-575, Y), new Vector2(W, H), Blue,
            "<b>W A S D</b>  —  Move\n" +
            "<b>Mouse</b>  —  Aim\n" +
            "<b>Left click</b>  —  Fire\n\n" +
            "<color=#9ea6b5>Your gun unlocks after your first hit.</color>");
        Box(root, "RULES", new Vector2(0, Y), new Vector2(W, H), Red,
            "Each hit costs <b>1 heart</b>.\n\n" +
            "You lose the <b>direction</b> the shot came from.\n\n" +
            "An <b>orb</b> appears — grab it within <b>15 seconds</b> to get that direction back.");
        Box(root, "GOAL", new Vector2(575, Y), new Vector2(W, H), Green,
            "Survive <b>60 seconds</b>.\n\n" +
            "<b>5 hits</b> = game over.\n\n" +
            "Touching the <b>crawler</b> = game over.\n\n" +
            "Walls bounce you back to the centre.");

        var prompt = Outline(root, "Prompt", new Vector2(0, -430), new Vector2(640, 70), Gold);
        Label(prompt, "PRESS ANY KEY TO START", 32, Vector2.zero, new Vector2(640, 70), Gold, FontStyle.Bold);
    }

    /// <summary>What each thing looks like in the arena, drawn with the game's own sprites.</summary>
    void BuildRoster(RectTransform root) {
        float[] xs = { -450f, -150f, 150f, 450f };
        const float y = 195f;

        // You: white square with the four coloured fins (Up, Down, Left, Right).
        var you = Node(root, "You"); you.anchoredPosition = new Vector2(xs[0], y);
        Img(you, "Body", new Vector2(48, 48), null, Color.white);
        Vector2[] fin = { new(0, 32), new(0, -32), new(-32, 0), new(32, 0) };
        for (int i = 0; i < 4; i++)
            Img(you, "Fin", new Vector2(18, 18), null, OrbSpawner.OrbColors[i]).anchoredPosition = fin[i];

        // Shooter: the red turret with its core.
        var shooter = Node(root, "Shooter"); shooter.anchoredPosition = new Vector2(xs[1], y);
        Img(shooter, "Body", new Vector2(60, 60), null, Turret);
        Img(shooter, "Core", new Vector2(22, 22), _circle, new Color(1f, 0.53f, 0.33f));

        // Crawler: the pink triangle.
        var crawler = Node(root, "Crawler"); crawler.anchoredPosition = new Vector2(xs[2], y);
        Img(crawler, "Body", new Vector2(62, 62), _tri, Pink);

        // Orb: a ring in the colour of the lost direction (yellow = Left here).
        var orb = Node(root, "Orb"); orb.anchoredPosition = new Vector2(xs[3], y);
        Img(orb, "Ring", new Vector2(64, 64), _ring, OrbSpawner.OrbColors[(int)Direction.Left]);

        string[] names = { "You", "Shooter", "Crawler", "Orb" };
        string[] notes = { "fins = your 4 directions", "fires at you", "touch it = game over", "restores a direction" };
        for (int i = 0; i < 4; i++) {
            Label(root, names[i], 32, new Vector2(xs[i], y - 72), new Vector2(300, 40), Ink, FontStyle.Bold);
            Label(root, notes[i], 26, new Vector2(xs[i], y - 108), new Vector2(300, 34), Muted);
        }
    }

    void Box(RectTransform root, string title, Vector2 pos, Vector2 size, Color accent, string body) {
        var box = Outline(root, title, pos, size, accent);
        Label(box, title, 32, new Vector2(0, size.y / 2f - 42f), new Vector2(size.x, 44), accent, FontStyle.Bold);
        var text = Label(box, body, 28, new Vector2(0, -28f), new Vector2(size.x - 64f, size.y - 110f), Ink);
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.lineSpacing = 1.1f;
    }

    /// <summary>A plain rectangle outline, 2px, with a faint dark fill.</summary>
    RectTransform Outline(RectTransform parent, string name, Vector2 pos, Vector2 size, Color c) {
        var box = Img(parent, name, size, null, new Color(1f, 1f, 1f, 0.03f));
        box.anchoredPosition = pos;
        const float th = 2f;
        float w = size.x, h = size.y;
        Img(box, "Top",    new Vector2(w, th), null, c).anchoredPosition = new Vector2(0,  h / 2f);
        Img(box, "Bottom", new Vector2(w, th), null, c).anchoredPosition = new Vector2(0, -h / 2f);
        Img(box, "Left",   new Vector2(th, h), null, c).anchoredPosition = new Vector2(-w / 2f, 0);
        Img(box, "Right",  new Vector2(th, h), null, c).anchoredPosition = new Vector2( w / 2f, 0);
        return box;
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// The page's shapes, generated here at 256px with anti-aliased edges. The game's own
    /// sprites are 64px with hard edges - fine at arena size, jagged when blown up on this
    /// page. Same shapes, same colours, just drawn smooth.
    /// </summary>
    void MakeSprites() {
        const float aa = 1.5f / 128f;                          // ~1.5px soft edge at 256px
        _circle = Gen((u, v) => Edge(1f - Mathf.Sqrt(u * u + v * v), aa));
        _ring = Gen((u, v) => {
            float r = Mathf.Sqrt(u * u + v * v);
            return Mathf.Min(Edge(1f - r, aa), Edge(r - 0.72f, aa));   // same thickness as the orb
        });
        _tri = Gen((u, v) => {
            // Apex up, base at the bottom - matches the crawler sprite.
            float y = (v + 1f) / 2f;                           // 0 at base, 1 at apex
            float half = 1f - y;                               // half-width shrinks toward apex
            return Mathf.Min(Edge(half - Mathf.Abs(u), aa * 1.2f), Edge(y, aa), Edge(1f - y, aa));
        });
    }

    static float Edge(float d, float aa) => Mathf.Clamp01(d / aa + 0.5f);

    Sprite Gen(System.Func<float, float, float> alpha) {
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) {
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++) {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(u, v)) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        _made.Add(tex);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    void OnDestroy() { foreach (var t in _made) if (t != null) Destroy(t); }

    static RectTransform Node(Transform parent, string name) {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    static RectTransform Img(Transform parent, string name, Vector2 size, Sprite sprite, Color color) {
        var rt = Node(parent, name);
        rt.sizeDelta = size;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = sprite != null;
        img.raycastTarget = false;
        return rt;
    }

    Text Label(Transform parent, string text, int size, Vector2 pos, Vector2 box, Color color,
               FontStyle style = FontStyle.Normal) {
        var rt = Node(parent, "Text");
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = rt.gameObject.AddComponent<Text>();
        t.font = _font;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.text = text;
        t.alignment = TextAnchor.MiddleCenter;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }
}
}
