using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 사자의 도망 (Lion Run) - 메인 게임 매니저
///
/// 기획서 「포켓몬볼 피하기」 기반 변경점
///  - 주인공 : 메타몽 → 사자
///  - 장애물 : 몬스터볼 → 깡통 (빨강/파랑/노랑/보라 4종, 시간에 따라 해금)
///  - 점수   : 회피 +1 → 생닭을 먹으면 +1
///  - 조작   : TAP(한 칸 이동) / HOLD(계속 이동) / SWIPE(대시)  ※ 조이스틱(Stick) 배제
///  - 난이도 : 0~20초 적응 / 20~40초 가속 / 40초~ 한계(생성 간격 단축 + 동시 낙하)
///
/// 씬에 이 컴포넌트 하나만 있으면 카메라·배경·캐릭터·UI·사운드를 전부 코드로 생성합니다.
/// (씬에 없어도 Play 시 자동 생성됨)
/// </summary>
public class LionGame : MonoBehaviour
{
    // ───────────────────────── 튜닝 값 ─────────────────────────
    const float DesignHalfWidth = 2.4f;   // 480px 기준 (100px = 1유닛)
    const float DesignHalfHeight = 4.27f; // 854px 기준
    const float LionBottomMargin = 1.1f;

    const float LionRadius = 0.30f;
    const float CanRadius = 0.24f;
    const float ChickenRadius = 0.32f;

    const float TapStep = 0.8f;    // TAP 한 번에 이동 거리
    const float TapSpeed = 11f;
    const float SwipeDash = 2.0f;  // SWIPE 대시 거리
    const float DashSpeed = 24f;
    const float HoldSpeed = 5.2f;  // HOLD 이동 속도

    const string BestKey = "LionRun_Best";

    static readonly string[] StageNames = { "★☆☆ 적응 구간", "★★☆ 가속 구간", "★★★ 한계 구간" };
    static readonly Color[] StageColors = { Hex("10B981"), Hex("F59E0B"), Hex("EF4444") };

    // 깡통 4종 : 빨강(기본) / 파랑(빠름) / 노랑(매우 빠름) / 보라(극상)
    static readonly Color[] CanColors = { Hex("E53935"), Hex("3B82F6"), Hex("EAB308"), Hex("8E44AD") };
    static readonly float[] CanSpeedMul = { 1f, 1.25f, 1.5f, 1.85f };
    static readonly float[] CanUnlock = { 0f, 20f, 40f, 60f };
    static readonly int[] CanWeight = { 6, 3, 2, 1 };

    static readonly Color Grass = Hex("A8D08D");
    static readonly Color Accent = Hex("C9741F");
    static readonly Color DarkText = Hex("1F2937");

    // ───────────────────────── 내부 타입 ─────────────────────────
    enum State { Title, Playing, Dying, GameOver }

    class Falling
    {
        public GameObject go; public Transform tr; public SpriteRenderer sr;
        public bool active, chicken;
        public float speed, rotSpeed, radius, wobble;
        public Color tint;
    }

    class Particle
    {
        public GameObject go; public Transform tr; public SpriteRenderer sr;
        public bool active, shrink;
        public Vector2 vel;
        public float life, maxLife, gravity, spin;
        public Color color;
        public Vector3 scale;
    }

    class Popup
    {
        public Text text; public RectTransform rt;
        public Vector3 world; public float life; public bool active;
    }

    // ───────────────────────── 상태 ─────────────────────────
    State state;
    readonly GameInput input = new GameInput();
    SoundFx sfx;

    Camera cam;
    Vector3 camBasePos;
    float halfW, halfH;
    int screenW, screenH;
    float shake;

    Sprite lionSprite, lionChompSprite, chickenSprite, shadowSprite, dotSprite, roundSprite, tuftSprite;
    Sprite[] canSprites;

    Transform world;
    Transform lion, lionShadow;
    SpriteRenderer lionSr;
    float lionX, lionTargetX, approachSpeed, lionVX, chompTimer, eatPop, ghostTimer;
    bool wasHolding;

    float elapsed, canTimer, chickenTimer, stateTimer;
    int stage, score, best;
    bool newRecord;

    readonly List<Falling> items = new List<Falling>();
    readonly List<Particle> particles = new List<Particle>();
    readonly List<Popup> popups = new List<Popup>();

    // UI
    Font font;
    Transform uiRoot;
    GameObject titleRoot, hudRoot, gameOverRoot;
    RectTransform startButton, retryButton, titleLion, scorePill, gameOverPanel;
    Image stagePill;
    Text scoreText, stageText, hudBestText, gestureText, bannerText, titleBestText;
    Text goScoreText, goInfoText, goBestText, newRecordText;
    float scorePop, bannerTimer, gestureTimer;

    float LionY => -halfH + LionBottomMargin;

    // ───────────────────────── 부트스트랩 ─────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBoot()
    {
        if (FindFirstObjectByType<LionGame>() == null)
            new GameObject("LionGame").AddComponent<LionGame>();
    }

    void Awake()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
#if !UNITY_EDITOR
        Screen.orientation = ScreenOrientation.Portrait;
#endif
        best = PlayerPrefs.GetInt(BestKey, 0);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildSprites();
        SetupCamera();
        BuildWorld();
        BuildUI();
        sfx = gameObject.AddComponent<SoundFx>();
        EnterTitle();
    }

    void Update()
    {
        if (Screen.width != screenW || Screen.height != screenH) FitCamera();

        float dt = Time.deltaTime;
        input.Update();

        switch (state)
        {
            case State.Title: UpdateTitle(); break;
            case State.Playing: UpdatePlaying(dt); break;
            case State.Dying: UpdateDying(dt); break;
            case State.GameOver: UpdateGameOver(dt); break;
        }

        UpdateParticles(dt);
        UpdatePopups(dt);
        UpdateUIAnim(dt);

        if (shake > 0f)
        {
            shake = Mathf.Max(0f, shake - dt);
            cam.transform.position = camBasePos + (Vector3)(Random.insideUnitCircle * shake * 0.35f);
        }
        else cam.transform.position = camBasePos;
    }

    // ───────────────────────── 상태 전환 ─────────────────────────
    void EnterTitle()
    {
        state = State.Title;
        titleRoot.SetActive(true);
        hudRoot.SetActive(false);
        gameOverRoot.SetActive(false);
        SetWorldVisible(false);
        ClearItems();
        ClearParticles();
        titleBestText.text = "BEST : " + best;
    }

    void StartGame()
    {
        state = State.Playing;
        titleRoot.SetActive(false);
        hudRoot.SetActive(true);
        gameOverRoot.SetActive(false);
        ClearItems();
        ClearParticles();
        ClearPopups();
        SetWorldVisible(true);

        score = 0;
        elapsed = 0f;
        stage = -1;
        canTimer = 0.9f;
        chickenTimer = 0.5f;
        lionX = lionTargetX = 0f;
        approachSpeed = TapSpeed;
        lionVX = chompTimer = eatPop = 0f;
        wasHolding = false;
        newRecord = false;
        lionSr.color = Color.white;
        lion.rotation = Quaternion.identity;

        UpdateScoreUI();
        sfx.Click();
    }

    void ShowGameOver()
    {
        state = State.GameOver;
        stateTimer = 0f;
        gameOverRoot.SetActive(true);
        goScoreText.text = "SCORE : " + score;
        goInfoText.text = $"생닭 {score}마리 · {elapsed:0.0}초 생존";
        goBestText.text = "BEST : " + best;
        newRecordText.gameObject.SetActive(newRecord);
        gameOverPanel.localScale = Vector3.one * 0.6f;
    }

    // ───────────────────────── 타이틀 ─────────────────────────
    void UpdateTitle()
    {
        bool down = input.IsPressed && Contains(startButton, input.Position);
        startButton.localScale = Vector3.one * (down ? 0.94f : 1f + Mathf.Sin(Time.time * 4f) * 0.03f);

        float jump = Mathf.Abs(Mathf.Sin(Time.time * 3f));
        titleLion.anchoredPosition = new Vector2(0f, 120f + jump * 16f);
        float sq = 1f - jump; // 바닥에 닿을 때 살짝 찌그러짐
        titleLion.localScale = new Vector3(1f + sq * 0.08f, 1f - sq * 0.08f, 1f);

        if ((input.ReleasedThisFrame && Contains(startButton, input.ReleasePosition)) || input.ConfirmPressed)
            StartGame();
    }

    // ───────────────────────── 플레이 ─────────────────────────
    void UpdatePlaying(float dt)
    {
        elapsed += dt;

        int newStage = elapsed < 20f ? 0 : (elapsed < 40f ? 1 : 2);
        if (newStage != stage)
        {
            stage = newStage;
            stageText.text = StageNames[stage];
            stagePill.color = StageColors[stage];
            ShowBanner(stage == 0 ? "READY... GO!" : StageNames[stage] + "!", StageColors[stage]);
            if (stage > 0) sfx.Stage();
        }

        UpdateLionMovement(dt);
        UpdateLionVisual(dt);

        // 깡통 생성
        canTimer -= dt;
        if (canTimer <= 0f)
        {
            float x1 = SpawnCan(float.NaN);
            if (stage >= 2 && Random.value < Mathf.Min(0.55f, 0.25f + (elapsed - 40f) * 0.008f))
                SpawnCan(x1); // 한계 구간 : 동시 등장
            canTimer = CanInterval();
        }

        // 생닭 생성
        chickenTimer -= dt;
        if (chickenTimer <= 0f)
        {
            SpawnChicken();
            chickenTimer = Random.Range(1.1f, 1.9f);
        }

        // 낙하 & 충돌
        Vector2 lp = new Vector2(lionX, LionY);
        for (int i = 0; i < items.Count; i++)
        {
            var f = items[i];
            if (!f.active) continue;

            Vector3 p = f.tr.position;
            p.y -= f.speed * dt;
            f.tr.position = p;

            if (f.chicken) f.tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 5f + f.wobble) * 14f);
            else f.tr.Rotate(0f, 0f, f.rotSpeed * dt);

            float d = Vector2.Distance(lp, new Vector2(p.x, p.y));
            if (f.chicken)
            {
                if (d < LionRadius + f.radius + 0.06f) { Eat(f); continue; }
            }
            else if (d < LionRadius + f.radius)
            {
                Die(f);
                return;
            }

            if (p.y < -halfH - 1f) Deactivate(f);
        }
    }

    void UpdateLionMovement(float dt)
    {
        float prev = lionX;

        if (input.SwipeDir != 0)
        {
            // SWIPE : 대시
            lionTargetX = ClampX(lionX + input.SwipeDir * SwipeDash);
            approachSpeed = DashSpeed;
            sfx.Swipe();
            ShowGesture(input.SwipeDir < 0 ? "<<< SWIPE" : "SWIPE >>>");
        }
        else if (input.TapDir != 0)
        {
            // TAP : 한 칸 이동
            lionTargetX = ClampX(lionTargetX + input.TapDir * TapStep);
            approachSpeed = TapSpeed;
            sfx.Tap();
            ShowGesture(input.TapDir < 0 ? "< TAP" : "TAP >");
        }

        if (input.HoldDir != 0)
        {
            // HOLD : 계속 이동
            lionX = ClampX(lionX + input.HoldDir * HoldSpeed * dt);
            lionTargetX = lionX;
            if (!wasHolding) ShowGesture(input.HoldDir < 0 ? "<< HOLD" : "HOLD >>");
            gestureTimer = Mathf.Max(gestureTimer, 0.3f);
        }
        else
        {
            lionX = Mathf.MoveTowards(lionX, lionTargetX, approachSpeed * dt);
        }
        wasHolding = input.HoldDir != 0;

        lionVX = dt > 0f ? (lionX - prev) / dt : 0f;

        // 대시 잔상
        if (approachSpeed >= DashSpeed && Mathf.Abs(lionTargetX - lionX) > 0.05f)
        {
            ghostTimer -= dt;
            if (ghostTimer <= 0f)
            {
                ghostTimer = 0.03f;
                SpawnParticle(lionSprite, lion.position, Vector2.zero, 0.22f,
                    new Color(1f, 0.85f, 0.5f, 0.45f), 1f, 0f, 9, 0f, false);
            }
        }
    }

    void UpdateLionVisual(float dt)
    {
        chompTimer -= dt;
        eatPop = Mathf.MoveTowards(eatPop, 0f, dt * 5f);

        float bob = Mathf.Sin(Time.time * 6f) * 0.03f;
        float stretch = Mathf.Clamp(Mathf.Abs(lionVX) * 0.03f, 0f, 0.22f); // 이동 시 스쿼시/스트레치
        float pop = 1f + eatPop * 0.18f;

        lion.position = new Vector3(lionX, LionY, 0f);
        lion.localScale = new Vector3((1f + stretch) * pop, (1f - stretch * 0.6f + bob) * pop, 1f);
        lion.rotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(-lionVX * 2f, -14f, 14f));
        lionSr.sprite = chompTimer > 0f ? lionChompSprite : lionSprite;

        lionShadow.position = new Vector3(lionX, LionY - 0.45f, 0f);
        lionShadow.localScale = new Vector3(0.85f + stretch, 0.45f, 1f);
    }

    void Eat(Falling f)
    {
        Vector3 pos = f.tr.position;
        Deactivate(f);

        score++;
        scorePop = 1f;
        chompTimer = 0.2f;
        eatPop = 1f;
        UpdateScoreUI();
        sfx.Eat();
        ShowPopup(pos + Vector3.up * 0.3f, "+1", Hex("FFF59D"));

        for (int i = 0; i < 8; i++)
        {
            Vector2 v = Random.insideUnitCircle * 2.2f + Vector2.up * 1.6f;
            Color c = Random.value < 0.5f ? Hex("F4A6A6") : Color.white;
            SpawnParticle(dotSprite, pos, v, Random.Range(0.35f, 0.55f), c, Random.Range(0.8f, 1.4f), 7f, 12);
        }
    }

    void Die(Falling f)
    {
        state = State.Dying;
        stateTimer = 0f;
        shake = 0.45f;

        Vector3 pos = f.tr.position;
        sfx.Hit();
        ShowPopup(pos, "깡!", Color.white);

        for (int i = 0; i < 16; i++)
        {
            Vector2 v = Random.insideUnitCircle.normalized * Random.Range(2f, 4.5f);
            Color c = Random.value < 0.5f ? f.tint : Hex("E5E7EB");
            SpawnParticle(dotSprite, pos, v, Random.Range(0.4f, 0.7f), c, Random.Range(1f, 1.8f), 4f, 12);
        }

        if (score > best)
        {
            best = score;
            newRecord = true;
            PlayerPrefs.SetInt(BestKey, best);
            PlayerPrefs.Save();
        }
        UpdateScoreUI();
    }

    void UpdateDying(float dt)
    {
        stateTimer += dt;
        bool flash = Mathf.Repeat(stateTimer, 0.16f) < 0.08f;
        lionSr.color = flash ? new Color(1f, 0.45f, 0.45f) : Color.white;
        lion.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(stateTimer * 40f) * 10f * Mathf.Max(0f, 1f - stateTimer));

        if (stateTimer >= 0.9f)
        {
            lionSr.color = new Color(1f, 0.7f, 0.7f);
            ShowGameOver();
        }
    }

    // ───────────────────────── 게임 오버 ─────────────────────────
    void UpdateGameOver(float dt)
    {
        stateTimer += dt;
        gameOverPanel.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(Mathf.Clamp01(stateTimer / 0.3f)));

        bool down = input.IsPressed && Contains(retryButton, input.Position);
        retryButton.localScale = Vector3.one * (down ? 0.94f : 1f);

        if (stateTimer < 0.4f) return; // 실수로 바로 재시작 방지
        if ((input.ReleasedThisFrame && Contains(retryButton, input.ReleasePosition)) || input.ConfirmPressed)
            StartGame();
    }

    // ───────────────────────── 난이도 ─────────────────────────
    float BaseFallSpeed()
    {
        if (elapsed < 20f) return 2.6f + elapsed * 0.015f;              // 적응 : 느린 낙하
        if (elapsed < 40f) return 2.9f + (elapsed - 20f) * 0.08f;       // 가속 : 지속 증가
        return Mathf.Min(7.5f, 4.5f + (elapsed - 40f) * 0.04f);         // 한계
    }

    float CanInterval()
    {
        if (elapsed < 40f) return Mathf.Lerp(1.05f, 0.8f, elapsed / 40f);
        return Mathf.Max(0.38f, 0.8f - (elapsed - 40f) * 0.01f);        // 한계 : 생성 간격 단축
    }

    int PickCanType()
    {
        int total = 0;
        for (int i = 0; i < CanWeight.Length; i++) if (elapsed >= CanUnlock[i]) total += CanWeight[i];
        int r = Random.Range(0, total);
        for (int i = 0; i < CanWeight.Length; i++)
        {
            if (elapsed < CanUnlock[i]) continue;
            if (r < CanWeight[i]) return i;
            r -= CanWeight[i];
        }
        return 0;
    }

    // ───────────────────────── 낙하물 ─────────────────────────
    float RandomX() => Random.Range(-halfW + 0.35f, halfW - 0.35f);
    float ClampX(float x) => Mathf.Clamp(x, -halfW + 0.42f, halfW - 0.42f);

    float SpawnCan(float avoidX)
    {
        float x = RandomX();
        if (!float.IsNaN(avoidX))
            for (int k = 0; k < 8 && Mathf.Abs(x - avoidX) < 1.1f; k++) x = RandomX();

        int type = PickCanType();
        var f = GetItem();
        f.chicken = false;
        f.sr.sprite = canSprites[type];
        f.sr.sortingOrder = 6;
        f.tint = CanColors[type];
        f.speed = BaseFallSpeed() * CanSpeedMul[type];
        f.radius = CanRadius;
        f.rotSpeed = Random.Range(-160f, 160f);
        Activate(f, x);
        return x;
    }

    void SpawnChicken()
    {
        float x = RandomX();
        // 방금 생성된 깡통 바로 위에 겹치지 않도록
        for (int k = 0; k < 6; k++)
        {
            bool overlap = false;
            foreach (var it in items)
                if (it.active && !it.chicken && it.tr.position.y > halfH - 1.5f && Mathf.Abs(it.tr.position.x - x) < 0.6f)
                { overlap = true; break; }
            if (!overlap) break;
            x = RandomX();
        }

        var f = GetItem();
        f.chicken = true;
        f.sr.sprite = chickenSprite;
        f.sr.sortingOrder = 5;
        f.tint = Hex("F4A6A6");
        f.speed = Mathf.Min(4.5f, 2.3f + elapsed * 0.02f);
        f.radius = ChickenRadius;
        f.rotSpeed = 0f;
        f.wobble = Random.Range(0f, 6.28f);
        Activate(f, x);
    }

    Falling GetItem()
    {
        foreach (var f in items) if (!f.active) return f;
        var n = new Falling { go = new GameObject("FallingItem") };
        n.tr = n.go.transform;
        n.tr.SetParent(world, false);
        n.sr = n.go.AddComponent<SpriteRenderer>();
        items.Add(n);
        return n;
    }

    void Activate(Falling f, float x)
    {
        f.tr.position = new Vector3(x, halfH + 0.6f, 0f);
        f.tr.rotation = Quaternion.identity;
        f.sr.color = Color.white;
        f.active = true;
        f.go.SetActive(true);
    }

    void Deactivate(Falling f)
    {
        f.active = false;
        f.go.SetActive(false);
    }

    void ClearItems()
    {
        foreach (var f in items) Deactivate(f);
    }

    // ───────────────────────── 파티클 / 팝업 ─────────────────────────
    void SpawnParticle(Sprite s, Vector2 pos, Vector2 vel, float life, Color color, float scale,
        float gravity, int order, float spin = 0f, bool shrink = true)
    {
        Particle p = null;
        foreach (var q in particles) if (!q.active) { p = q; break; }
        if (p == null)
        {
            p = new Particle { go = new GameObject("Particle") };
            p.tr = p.go.transform;
            p.tr.SetParent(world, false);
            p.sr = p.go.AddComponent<SpriteRenderer>();
            particles.Add(p);
        }
        p.active = true;
        p.go.SetActive(true);
        p.sr.sprite = s;
        p.sr.sortingOrder = order;
        p.tr.position = pos;
        p.tr.rotation = Quaternion.identity;
        p.vel = vel;
        p.life = p.maxLife = life;
        p.color = color;
        p.sr.color = color;
        p.scale = Vector3.one * scale;
        p.tr.localScale = p.scale;
        p.gravity = gravity;
        p.spin = spin;
        p.shrink = shrink;
    }

    void UpdateParticles(float dt)
    {
        foreach (var p in particles)
        {
            if (!p.active) continue;
            p.life -= dt;
            if (p.life <= 0f) { p.active = false; p.go.SetActive(false); continue; }
            p.vel.y -= p.gravity * dt;
            p.tr.position += (Vector3)(p.vel * dt);
            if (p.spin != 0f) p.tr.Rotate(0f, 0f, p.spin * dt);
            float k = p.life / p.maxLife;
            var c = p.color; c.a *= k; p.sr.color = c;
            if (p.shrink) p.tr.localScale = p.scale * (0.4f + 0.6f * k);
        }
    }

    void ClearParticles()
    {
        foreach (var p in particles) { p.active = false; p.go.SetActive(false); }
    }

    void ShowPopup(Vector3 worldPos, string s, Color c)
    {
        Popup p = null;
        foreach (var q in popups) if (!q.active) { p = q; break; }
        if (p == null) return;
        p.text.text = s;
        p.text.color = c;
        p.world = worldPos;
        p.life = 0.8f;
        p.active = true;
        p.rt.gameObject.SetActive(true);
    }

    void UpdatePopups(float dt)
    {
        foreach (var p in popups)
        {
            if (!p.active) continue;
            p.life -= dt;
            if (p.life <= 0f) { p.active = false; p.rt.gameObject.SetActive(false); continue; }
            float t = 1f - p.life / 0.8f;
            p.rt.position = cam.WorldToScreenPoint(p.world + Vector3.up * t * 0.8f);
            p.rt.localScale = Vector3.one * (t < 0.15f ? 1f + (0.15f - t) * 3f : 1f);
            var c = p.text.color; c.a = Mathf.Clamp01(p.life / 0.32f); p.text.color = c;
        }
    }

    void ClearPopups()
    {
        foreach (var p in popups) { p.active = false; p.rt.gameObject.SetActive(false); }
    }

    // ───────────────────────── UI 연출 ─────────────────────────
    void UpdateScoreUI()
    {
        scoreText.text = "SCORE : " + score;
        hudBestText.text = "BEST " + Mathf.Max(best, score);
    }

    void ShowBanner(string s, Color c)
    {
        bannerText.text = s;
        bannerText.color = c;
        bannerTimer = 1.6f;
    }

    void ShowGesture(string s)
    {
        gestureText.text = s;
        gestureTimer = 0.6f;
    }

    void UpdateUIAnim(float dt)
    {
        scorePop = Mathf.MoveTowards(scorePop, 0f, dt * 5f);
        scorePill.localScale = Vector3.one * (1f + scorePop * 0.18f);

        if (bannerTimer > 0f)
        {
            bannerTimer -= dt;
            float inT = 1.6f - bannerTimer;
            var c = bannerText.color; c.a = Mathf.Clamp01(bannerTimer / 0.4f); bannerText.color = c;
            bannerText.rectTransform.localScale = Vector3.one * (inT < 0.2f ? Mathf.Lerp(1.4f, 1f, inT / 0.2f) : 1f);
        }
        else if (bannerText.text.Length > 0) bannerText.text = "";

        gestureTimer = Mathf.Max(0f, gestureTimer - dt);
        var g = gestureText.color; g.a = Mathf.Clamp01(gestureTimer / 0.3f); gestureText.color = g;
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    static bool Contains(RectTransform rt, Vector2 screenPos) =>
        RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null);

    // ───────────────────────── 생성 : 스프라이트 / 카메라 / 월드 ─────────────────────────
    void BuildSprites()
    {
        lionSprite = PixelArt.Lion(false);
        lionChompSprite = PixelArt.Lion(true);
        chickenSprite = PixelArt.Chicken();
        canSprites = new Sprite[CanColors.Length];
        for (int i = 0; i < CanColors.Length; i++) canSprites[i] = PixelArt.Can(CanColors[i]);
        tuftSprite = PixelArt.Tuft();
        shadowSprite = PixelArt.Ellipse(32, 16, 32f);
        dotSprite = PixelArt.Dot();
        roundSprite = PixelArt.RoundedRect(64, 24);
    }

    void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
        }
        if (FindFirstObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Grass;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.transform.rotation = Quaternion.identity;
        camBasePos = cam.transform.position;
        FitCamera();
    }

    /// 480x854 기준 화면 폭(4.8유닛)을 항상 보장. 더 긴 화면은 위아래가 늘어남.
    void FitCamera()
    {
        screenW = Screen.width;
        screenH = Screen.height;
        float aspect = (float)Mathf.Max(1, screenW) / Mathf.Max(1, screenH);
        float size = Mathf.Max(DesignHalfHeight, DesignHalfWidth / aspect);
        cam.orthographicSize = size;
        halfH = size;
        halfW = size * aspect;
    }

    void BuildWorld()
    {
        world = new GameObject("World").transform;

        // 풀밭 장식
        var rnd = new System.Random(2023137010);
        for (int i = 0; i < 46; i++)
        {
            var sr = new GameObject("Grass").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(world, false);
            sr.sprite = tuftSprite;
            sr.sortingOrder = -10;
            sr.color = Color.Lerp(Hex("8DBF6E"), Hex("9BCB7F"), (float)rnd.NextDouble());
            sr.transform.position = new Vector3((float)(rnd.NextDouble() * 9.0 - 4.5), (float)(rnd.NextDouble() * 16.0 - 8.0), 0f);
        }
        for (int i = 0; i < 16; i++)
        {
            var sr = new GameObject("Flower").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(world, false);
            sr.sprite = dotSprite;
            sr.sortingOrder = -9;
            sr.color = rnd.NextDouble() < 0.5 ? new Color(1f, 1f, 1f, 0.85f) : Hex("FDE68A");
            sr.transform.position = new Vector3((float)(rnd.NextDouble() * 9.0 - 4.5), (float)(rnd.NextDouble() * 16.0 - 8.0), 0f);
        }

        // 사자 그림자
        var shadow = new GameObject("LionShadow").AddComponent<SpriteRenderer>();
        shadow.transform.SetParent(world, false);
        shadow.sprite = shadowSprite;
        shadow.color = new Color(0f, 0f, 0f, 0.18f);
        shadow.sortingOrder = 1;
        lionShadow = shadow.transform;

        // 사자
        lionSr = new GameObject("Lion").AddComponent<SpriteRenderer>();
        lionSr.transform.SetParent(world, false);
        lionSr.sprite = lionSprite;
        lionSr.sortingOrder = 10;
        lion = lionSr.transform;
    }

    void SetWorldVisible(bool visible)
    {
        lion.gameObject.SetActive(visible);
        lionShadow.gameObject.SetActive(visible);
        if (visible)
        {
            lion.position = new Vector3(0f, LionY, 0f);
            lionShadow.position = new Vector3(0f, LionY - 0.45f, 0f);
        }
    }

    // ───────────────────────── 생성 : UI ─────────────────────────
    void BuildUI()
    {
        var canvasGo = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(480f, 854f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        uiRoot = canvasGo.transform;

        Vector2 C = new Vector2(0.5f, 0.5f), TOP = new Vector2(0.5f, 1f), BOTTOM = new Vector2(0.5f, 0f);
        Color soft = Hex("374151");

        // ── HUD ──
        hudRoot = Stretch("HUD", uiRoot).gameObject;
        var H = hudRoot.transform;
        scorePill = Img("ScorePill", H, TOP, new Vector2(0f, -46f), new Vector2(200f, 50f), roundSprite, new Color(1f, 1f, 1f, 0.92f), true).rectTransform;
        scoreText = Txt("Score", scorePill, C, Vector2.zero, "SCORE : 0", 24, DarkText);
        stagePill = Img("StagePill", H, TOP, new Vector2(0f, -94f), new Vector2(150f, 30f), roundSprite, StageColors[0], true, 1.6f);
        stageText = Txt("Stage", stagePill.transform, C, Vector2.zero, StageNames[0], 15, Color.white);
        hudBestText = Txt("HudBest", H, new Vector2(1f, 1f), new Vector2(-48f, -46f), "BEST 0", 15, DarkText, FontStyle.Bold, TextAnchor.MiddleCenter, 100f);
        gestureText = Txt("Gesture", H, BOTTOM, new Vector2(0f, 26f), "", 16, Color.white);
        AddOutline(gestureText);
        bannerText = Txt("Banner", H, C, new Vector2(0f, 150f), "", 32, Color.white);
        AddOutline(bannerText);

        // ── 타이틀 ──
        titleRoot = Stretch("Title", uiRoot).gameObject;
        var T = titleRoot.transform;
        var titlePill = Img("TitlePill", T, C, new Vector2(0f, 290f), new Vector2(250f, 62f), roundSprite, new Color(1f, 1f, 1f, 0.92f), true);
        Txt("TitleText", titlePill.transform, C, Vector2.zero, "사자의 도망", 32, DarkText);
        Txt("SubTitle", T, C, new Vector2(0f, 240f), "LION RUN", 18, Accent);
        titleLion = Img("TitleLion", T, C, new Vector2(0f, 120f), new Vector2(150f, 150f), lionSprite, Color.white).rectTransform;

        var card = Img("Guide", T, C, new Vector2(0f, -95f), new Vector2(400f, 220f), roundSprite, new Color(1f, 1f, 1f, 0.88f), true, 1.2f).transform;
        Txt("GuideTitle", card, C, new Vector2(0f, 82f), "조작법 · 3가지 터치", 19, DarkText);
        Txt("G1", card, C, new Vector2(0f, 46f), "<b>TAP</b>      화면 좌/우를 톡 → 한 칸 이동", 16, soft, FontStyle.Normal, TextAnchor.MiddleLeft, 350f);
        Txt("G2", card, C, new Vector2(0f, 18f), "<b>HOLD</b>    화면 좌/우를 꾹 → 계속 이동", 16, soft, FontStyle.Normal, TextAnchor.MiddleLeft, 350f);
        Txt("G3", card, C, new Vector2(0f, -10f), "<b>SWIPE</b>  좌/우로 쓸기 → 빠른 대시", 16, soft, FontStyle.Normal, TextAnchor.MiddleLeft, 350f);
        Img("Divider", card, C, new Vector2(0f, -34f), new Vector2(350f, 2f), null, Hex("E5E7EB"));
        Img("ChickenIcon", card, C, new Vector2(-155f, -68f), new Vector2(38f, 38f), chickenSprite, Color.white);
        Txt("ChickenTxt", card, C, new Vector2(-70f, -68f), "생닭 = +1점", 16, soft, FontStyle.Bold, TextAnchor.MiddleLeft, 120f);
        Img("CanIcon", card, C, new Vector2(30f, -68f), new Vector2(34f, 38f), canSprites[0], Color.white);
        Txt("CanTxt", card, C, new Vector2(115f, -68f), "깡통 = 게임 오버", 16, soft, FontStyle.Bold, TextAnchor.MiddleLeft, 120f);

        startButton = Img("StartButton", T, C, new Vector2(0f, -290f), new Vector2(200f, 62f), roundSprite, Color.white, true).rectTransform;
        Txt("StartText", startButton, C, Vector2.zero, "[ START ]", 26, Accent);
        titleBestText = Txt("TitleBest", T, C, new Vector2(0f, -350f), "BEST : 0", 18, DarkText);

        // ── 게임 오버 ──
        gameOverRoot = Stretch("GameOver", uiRoot).gameObject;
        var G = gameOverRoot.transform;
        var dim = Stretch("Dim", G).gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.28f);
        dim.raycastTarget = false;
        gameOverPanel = Img("Panel", G, C, new Vector2(0f, 50f), new Vector2(300f, 230f), roundSprite, Color.white, true, 1.1f).rectTransform;
        Txt("GameOverTitle", gameOverPanel, C, new Vector2(0f, 72f), "GAME OVER", 36, Hex("EF4444"));
        goScoreText = Txt("GoScore", gameOverPanel, C, new Vector2(0f, 24f), "SCORE : 0", 28, DarkText);
        goInfoText = Txt("GoInfo", gameOverPanel, C, new Vector2(0f, -12f), "", 15, Hex("6B7280"), FontStyle.Normal);
        goBestText = Txt("GoBest", gameOverPanel, C, new Vector2(0f, -48f), "BEST : 0", 22, Accent);
        newRecordText = Txt("NewRecord", gameOverPanel, C, new Vector2(0f, -84f), "NEW RECORD!", 17, Hex("10B981"));
        retryButton = Img("RetryButton", G, C, new Vector2(0f, -130f), new Vector2(210f, 60f), roundSprite, Color.white, true).rectTransform;
        Txt("RetryText", retryButton, C, Vector2.zero, "[ 다시 하기 ]", 24, DarkText);

        // ── 점수 팝업 풀 ──
        var popupRoot = Stretch("Popups", uiRoot);
        for (int i = 0; i < 10; i++)
        {
            var t = Txt("Popup", popupRoot, C, Vector2.zero, "+1", 28, Color.white);
            AddOutline(t);
            t.gameObject.SetActive(false);
            popups.Add(new Popup { text = t, rt = t.rectTransform });
        }
    }

    RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    RectTransform Stretch(string name, Transform parent)
    {
        var rt = MakeRect(name, parent, Vector2.zero, Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    Image Img(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Sprite sprite, Color color,
        bool sliced = false, float ppuMultiplier = 1f)
    {
        var img = MakeRect(name, parent, anchor, pos, size).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sliced)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ppuMultiplier;
        }
        else if (sprite != null) img.preserveAspect = true;
        return img;
    }

    Text Txt(string name, Transform parent, Vector2 anchor, Vector2 pos, string s, int fontSize, Color color,
        FontStyle style = FontStyle.Bold, TextAnchor align = TextAnchor.MiddleCenter, float width = 420f)
    {
        var t = MakeRect(name, parent, anchor, pos, new Vector2(width, fontSize * 1.6f)).gameObject.AddComponent<Text>();
        t.font = font;
        t.text = s;
        t.fontSize = fontSize;
        t.color = color;
        t.fontStyle = style;
        t.alignment = align;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void AddOutline(Text t)
    {
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.55f);
        o.effectDistance = new Vector2(2f, -2f);
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
