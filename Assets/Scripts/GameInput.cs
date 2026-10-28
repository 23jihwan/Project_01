using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 3가지 조작(TAP / HOLD / SWIPE)을 판별하는 입력 처리기.
///  - TAP   : 화면 왼쪽/오른쪽을 짧게 톡 → 그 방향으로 한 칸 이동
///  - HOLD  : 화면 왼쪽/오른쪽을 꾹 누르고 있기 → 그 방향으로 계속 이동
///  - SWIPE : 손가락을 좌/우로 빠르게 쓸기 → 그 방향으로 대시
/// 터치(모바일)와 마우스(에디터) 모두 지원. 신/구 Input 시스템 모두 컴파일됨.
/// 에디터 테스트용 키보드: ←/→ 또는 A/D = HOLD, Space/Enter = 버튼 확인
/// </summary>
public class GameInput
{
    public const float HoldDelay = 0.18f;   // 이 시간 이상 누르면 HOLD
    public const float SwipeMaxTime = 0.35f; // 이 시간 안에 밀어야 SWIPE
    public const float SwipeDistFrac = 0.07f; // 화면 너비 대비 스와이프 최소 거리

    public bool IsPressed { get; private set; }
    public Vector2 Position { get; private set; }
    public bool PressedThisFrame { get; private set; }
    public bool ReleasedThisFrame { get; private set; }
    public Vector2 ReleasePosition { get; private set; }

    /// 이번 프레임에 발생한 제스처 (-1 왼쪽, +1 오른쪽, 0 없음)
    public int TapDir { get; private set; }
    public int SwipeDir { get; private set; }
    /// 현재 HOLD 중인 방향 (매 프레임 유지)
    public int HoldDir { get; private set; }
    public bool ConfirmPressed { get; private set; }

    enum Mode { None, Hold, Swiped }
    Mode mode;
    float pressTime;
    Vector2 startPos;

    public void Update()
    {
        TapDir = SwipeDir = HoldDir = 0;
        PressedThisFrame = ReleasedThisFrame = false;

        ReadPointer(out bool pressed, out Vector2 pos);
        if (pressed) Position = pos;

        float now = Time.unscaledTime;
        float center = Screen.width * 0.5f;
        float swipeDist = Mathf.Max(24f, Screen.width * SwipeDistFrac);

        if (pressed && !IsPressed)
        {
            PressedThisFrame = true;
            mode = Mode.None;
            pressTime = now;
            startPos = pos;
        }

        if (pressed)
        {
            Vector2 d = pos - startPos;
            float held = now - pressTime;

            if (mode == Mode.None && held <= SwipeMaxTime &&
                Mathf.Abs(d.x) >= swipeDist && Mathf.Abs(d.x) > Mathf.Abs(d.y) * 1.2f)
            {
                mode = Mode.Swiped;
                SwipeDir = d.x > 0 ? 1 : -1;
            }
            else if (mode == Mode.None && held >= HoldDelay && Mathf.Abs(d.x) < swipeDist)
            {
                mode = Mode.Hold;
            }

            if (mode == Mode.Hold)
                HoldDir = pos.x < center ? -1 : 1; // 누르고 있는 쪽으로 계속 이동
        }

        if (!pressed && IsPressed)
        {
            ReleasedThisFrame = true;
            ReleasePosition = Position;
            if (mode == Mode.None)
                TapDir = startPos.x < center ? -1 : 1;
        }

        IsPressed = pressed;

        // 키보드 (에디터 테스트용)
        ReadKeyboard(out int keyDir, out bool confirm);
        if (HoldDir == 0 && keyDir != 0) HoldDir = keyDir;
        ConfirmPressed = confirm;
    }

    static void ReadPointer(out bool pressed, out Vector2 pos)
    {
        pressed = false;
        pos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.isPressed)
        {
            pressed = true;
            pos = ts.primaryTouch.position.ReadValue();
            return;
        }
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            pressed = true;
            pos = mouse.position.ReadValue();
        }
#else
        if (Input.touchCount > 0)
        {
            pressed = true;
            pos = Input.GetTouch(0).position;
            return;
        }
        if (Input.GetMouseButton(0))
        {
            pressed = true;
            pos = Input.mousePosition;
        }
#endif
    }

    static void ReadKeyboard(out int dir, out bool confirm)
    {
        dir = 0;
        confirm = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) dir -= 1;
        if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) dir += 1;
        confirm = kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame;
#else
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) dir -= 1;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) dir += 1;
        confirm = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
    }
}
