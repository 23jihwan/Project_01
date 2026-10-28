using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 외부 이미지 없이 코드로 도트 스프라이트를 만드는 유틸리티.
/// (사자 / 생닭 / 깡통 / 풀 / UI용 둥근 사각형 등)
/// </summary>
public static class PixelArt
{
    static readonly Color32 Outline = Hex32("281910");

    public static Color32 Hex32(string hex, byte alpha = 255)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        Color32 c32 = c;
        c32.a = alpha;
        return c32;
    }

    // ───────────────────────── 캐릭터 / 오브젝트 ─────────────────────────

    /// 사자 (정면 얼굴 + 갈기). chomp = 입 벌린 프레임(생닭 먹을 때)
    public static Sprite Lion(bool chomp)
    {
        string[] rows =
        {
            "....MMMMMMMM....",
            "..MMMmmmmmmMMM..",
            ".MMmmEFFFFEmmMM.",
            ".MmmFFFFFFFFmmM.",
            "MMmFFFFFFFFFFmMM",
            "MmmFFKFFFFKFFmmM",
            "MmmFFKFFFFKFFmmM",
            "MmmFFFFFFFFFFmmM",
            "MmmFFFWKKWFFFmmM",
            "MMmFFWWKKWWFFmMM",
            chomp ? ".MmmFWKKKKWFmmM." : ".MmmFWWWWWWFmmM.",
            chomp ? ".MMmmFWKKWFmmMM." : ".MMmmFFWWFFmmMM.",
            "..MMmmFFFFmmMM..",
            "...MMMmmmmMMM...",
            "....MMMMMMMM....",
        };
        var pal = new Dictionary<char, Color32>
        {
            { 'M', Hex32("B5651D") }, // 갈기 진한색
            { 'm', Hex32("D9832B") }, // 갈기
            { 'E', Hex32("8B4A16") }, // 귀
            { 'F', Hex32("F5C451") }, // 얼굴
            { 'W', Hex32("FFF3D6") }, // 주둥이
            { 'K', Hex32("3A2A1A") }, // 눈/코/입
        };
        return FromRows(rows, pal, 20f, true);
    }

    /// 생닭 (날고기 닭다리)
    public static Sprite Chicken()
    {
        string[] rows =
        {
            "...PPPPP......",
            ".PPLLPPPPP....",
            "PPLLPPPPPPP...",
            "PPLPPPPPPPpP..",
            "PPPPPPPPPPpP..",
            "PPPPPPPPPppP..",
            ".PPPPPPPPpP...",
            "..PPPPPPppP...",
            "...ppPPPpB....",
            ".....ppBBB....",
            "......BBBb.BB.",
            ".......BBbBBBb",
            "........bBBbb.",
        };
        var pal = new Dictionary<char, Color32>
        {
            { 'P', Hex32("F4A6A6") },
            { 'p', Hex32("E07A80") },
            { 'L', Hex32("FFD0CC") },
            { 'B', Hex32("FFF8EE") },
            { 'b', Hex32("D9CFC0") },
        };
        return FromRows(rows, pal, 22f, true);
    }

    /// 깡통 (라벨 색상만 바꿔서 4종)
    public static Sprite Can(Color label)
    {
        string[] rows =
        {
            "..SSSSSS..",
            ".SssssssS.",
            ".SSSSSSSS.",
            ".RHRRRRRr.",
            ".RHRRRRRr.",
            ".WWWWWWWW.",
            ".RHRRRRRr.",
            ".RHRRRRRr.",
            ".WWWWWWWW.",
            ".RHRRRRRr.",
            ".RHRRRRRr.",
            ".SSSSSSSS.",
            "..ssssss..",
        };
        var pal = new Dictionary<char, Color32>
        {
            { 'S', Hex32("C9CED6") },
            { 's', Hex32("9AA3AE") },
            { 'R', label },
            { 'r', Color.Lerp(label, Color.black, 0.3f) },
            { 'H', Color.Lerp(label, Color.white, 0.45f) },
            { 'W', Hex32("FFFFFF") },
        };
        return FromRows(rows, pal, 22f, true);
    }

    /// 배경 풀 (흰색으로 만들고 SpriteRenderer.color 로 색 입힘)
    public static Sprite Tuft()
    {
        string[] rows =
        {
            "G..G..G",
            ".G.G.G.",
            ".GGGGG.",
        };
        var pal = new Dictionary<char, Color32> { { 'G', new Color32(255, 255, 255, 255) } };
        return FromRows(rows, pal, 20f, false);
    }

    /// 파티클용 작은 사각 점
    public static Sprite Dot()
    {
        string[] rows = { "WW", "WW" };
        var pal = new Dictionary<char, Color32> { { 'W', new Color32(255, 255, 255, 255) } };
        return FromRows(rows, pal, 20f, false);
    }

    // ───────────────────────── 범용 생성기 ─────────────────────────

    public static Sprite FromRows(string[] rows, Dictionary<char, Color32> palette, float ppu, bool outline)
    {
        int h = rows.Length, w = 0;
        foreach (var r in rows) w = Mathf.Max(w, r.Length);
        int pad = outline ? 1 : 0;
        int W = w + pad * 2, H = h + pad * 2;
        var px = new Color32[W * H];

        for (int y = 0; y < h; y++)
        {
            string row = rows[h - 1 - y]; // 텍스처는 아래쪽이 y=0
            for (int x = 0; x < w; x++)
            {
                char ch = x < row.Length ? row[x] : '.';
                if (palette.TryGetValue(ch, out var col))
                    px[(y + pad) * W + (x + pad)] = col;
            }
        }

        if (outline)
        {
            var src = (Color32[])px.Clone();
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (src[i].a != 0) continue;
                bool near =
                    (x > 0 && src[i - 1].a != 0) || (x < W - 1 && src[i + 1].a != 0) ||
                    (y > 0 && src[i - W].a != 0) || (y < H - 1 && src[i + W].a != 0);
                if (near) px[i] = Outline;
            }
        }

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "PixelArt"
        };
        tex.SetPixels32(px);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
    }

    /// UI용 둥근 사각형 (9-slice 테두리 포함)
    public static Sprite RoundedRect(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "RoundedRect"
        };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float fx = x + 0.5f, fy = y + 0.5f;
            float cx = Mathf.Clamp(fx, radius, size - radius);
            float cy = Mathf.Clamp(fy, radius, size - radius);
            float d = Vector2.Distance(new Vector2(fx, fy), new Vector2(cx, cy));
            float a = Mathf.Clamp01(radius - d + 0.5f);
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    /// 그림자용 부드러운 타원
    public static Sprite Ellipse(int w, int h, float ppu)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "Ellipse"
        };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float nx = (x + 0.5f) / w * 2f - 1f;
            float ny = (y + 0.5f) / h * 2f - 1f;
            float d = Mathf.Sqrt(nx * nx + ny * ny);
            float a = Mathf.Clamp01((1f - d) * 4f);
            px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
    }
}
