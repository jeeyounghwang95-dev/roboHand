using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// 전체 UI를 런타임에 생성·관리하는 싱글턴.
///
/// 패널 구성
///   - IntroPanel   : 첫 화면 (게임 설명 + 조작법 + 시작 버튼)
///   - MissionPopup : 미션 출제 시 중앙 팝업
///   - HUD          : 플레이 중 상단/하단 정보 바
///   - ResultOverlay: 성공/실패 brief 피드백
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // ── 패널 루트 ────────────────────────────────────────────────────────────
    private GameObject introPanel;
    private GameObject missionPopup;
    private GameObject hudPanel;
    private GameObject resultOverlay;

    // ── HUD 요소 ─────────────────────────────────────────────────────────────
    private TextMeshProUGUI hudScoreText;
    private TextMeshProUGUI hudTimerText;
    private TextMeshProUGUI hudMissionText;
    private Image           gripForceBar;       // Image.Type.Filled
    private TextMeshProUGUI gripForceLabel;

    // ── 팝업 요소 ────────────────────────────────────────────────────────────
    private TextMeshProUGUI popupCommandText;
    private TextMeshProUGUI popupHintText;

    // ── 결과 오버레이 ────────────────────────────────────────────────────────
    private TextMeshProUGUI resultText;
    private Image           resultBg;

    // ── 한글 폰트 ────────────────────────────────────────────────────────────
    [Header("한글 폰트 (Assets/Resources/Fonts/Pretendard SDF)")]
    [SerializeField] private TMP_FontAsset koreanFont;

    // ── 색상 상수 ────────────────────────────────────────────────────────────
    static readonly Color C_BG_DARK   = new Color(0.04f, 0.05f, 0.10f, 0.93f);
    static readonly Color C_BG_MID    = new Color(0.10f, 0.12f, 0.18f, 0.96f);
    static readonly Color C_ACCENT    = new Color(0.30f, 0.68f, 1.00f, 1.00f);
    static readonly Color C_BTN       = new Color(0.22f, 0.58f, 0.95f, 1.00f);
    static readonly Color C_SUCCESS   = new Color(0.18f, 0.78f, 0.30f, 0.88f);
    static readonly Color C_FAIL      = new Color(0.85f, 0.20f, 0.18f, 0.88f);
    static readonly Color C_TEXT      = new Color(0.94f, 0.95f, 1.00f, 1.00f);
    static readonly Color C_DIM       = new Color(0.65f, 0.72f, 0.82f, 1.00f);

    // ─────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 한글 폰트 자동 로드 (Assets/Resources/Fonts/Pretendard SDF)
        if (koreanFont == null)
            koreanFont = Resources.Load<TMP_FontAsset>("Fonts/Pretendard SDF");

        EnsureEventSystem();

        // 메인 캔버스 (팝업·인트로용, sortingOrder=100)
        Canvas canvas = BuildCanvas();
        BuildIntroPanel(canvas.transform);
        BuildMissionPopup(canvas.transform);
        BuildResultOverlay(canvas.transform);

        // HUD 전용 캔버스 (sortingOrder=200) — 팝업 오버레이 위에 항상 표시
        Canvas hudCanvas = BuildHUDCanvas();
        BuildHUD(hudCanvas.transform);
    }

    private void Start()
    {
        introPanel.SetActive(true);
        // hudPanel은 sortingOrder=200 전용 캔버스에 있으므로 처음부터 표시
        // 인트로 중에도 상단/하단 HUD 바가 보임 (Score: 0 / Time: 30s)
        hudPanel.SetActive(true);
        missionPopup.SetActive(false);
        resultOverlay.SetActive(false);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region EventSystem & Canvas 셋업

    private void EnsureEventSystem()
    {
        var existing = FindObjectOfType<EventSystem>();
        if (existing != null)
        {
            // StandaloneInputModule이 있으면 New Input System에서 클릭이 안 됨 → 교체
            var oldModule = existing.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                Destroy(oldModule);
                try { existing.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(); }
                catch { Debug.LogWarning("[UIManager] InputSystemUIInputModule 추가 실패 — 버튼 클릭이 안 될 수 있음"); }
            }
            return;
        }
        // EventSystem 없으면 새로 생성
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        try   { esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(); }
        catch { esGo.AddComponent<StandaloneInputModule>(); }
    }

    private Canvas BuildCanvas()
    {
        var go = new GameObject("UICanvas");
        go.layer = LayerMask.NameToLayer("UI");

        var c = go.AddComponent<Canvas>();
        c.renderMode  = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return c;
    }

    /// <summary>
    /// HUD 전용 캔버스 — sortingOrder=200으로 팝업·인트로 오버레이 위에 항상 렌더링.
    /// 이렇게 해야 미션 팝업의 어두운 전체화면 오버레이가 HUD를 가리지 않는다.
    /// </summary>
    private Canvas BuildHUDCanvas()
    {
        var go = new GameObject("HUDCanvas");
        go.layer = LayerMask.NameToLayer("UI");

        var c = go.AddComponent<Canvas>();
        c.renderMode   = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 200;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return c;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 인트로 화면 빌드

    private void BuildIntroPanel(Transform canvasRoot)
    {
        introPanel = Panel(canvasRoot, C_BG_DARK, fullscreen: true, "IntroPanel");

        // 중앙 카드 (글자 크기 +15%에 맞춰 확대)
        var card = Panel(introPanel.transform, C_BG_MID, false, "Card");
        AnchorCenter(card, 970, 820);

        float y = 360f;

        // 타이틀
        var title = Txt(card.transform, "ROBO HAND", 67, C_ACCENT, bold: true, center: true);
        AnchorCenter(title, 900, 80, 0, y); y -= 72f;

        var sub = Txt(card.transform, "피지컬 AI 로봇 팔 체험 게임", 25, C_DIM, center: true);
        AnchorCenter(sub, 900, 40, 0, y); y -= 56f;

        Divider(card.transform, y); y -= 36f;

        // 게임 설명
        string desc =
            "당신은 피지컬 AI 로봇 팔입니다.\n" +
            "<b>자연어 명령</b>을 받아 올바른 물체를 집어 지정된 그릇에 넣으세요.\n" +
            "비슷하게 생긴 물체·그릇들 사이에서 <b>올바른 선택과 힘 조절</b>이 핵심입니다!";
        var descT = Txt(card.transform, desc, 23, C_TEXT, center: true);
        AnchorCenter(descT, 880, 100, 0, y-20); y -= 116f;

        // 조작법 박스 (진한 파란색 테두리 + 채움)
        var ctrlBox = Panel(card.transform, new Color(0.06f, 0.14f, 0.32f, 0.98f), false, "CtrlBox");
        AnchorCenter(ctrlBox, 880, 200, 0, y - 60); y -= 218f;

        string ctrl =
            "<b>[ 조작법 ]</b>\n" +
            "  <color=#88ccff>← → ↑ ↓</color>  방향키    로봇 팔 이동 (인형뽑기처럼 위에서!)\n" +
            "  <color=#88ccff>Z</color>                   그리퍼 힘 <b>감소</b>  ▶  너무 약하면 미끄러짐!\n" +
            "  <color=#88ccff>X</color>                   그리퍼 힘 <b>증가</b>  ▶  너무 강하면 찌그러짐!\n" +
            "  <color=#88ccff>Enter</color>             팔을 내려서 집기 / 다시 내려서 놓기";
        var ctrlT = Txt(ctrlBox.transform, ctrl, 23, C_TEXT);
        AnchorCenter(ctrlT, 820, 180, 30, -5); // width, height, xOff, yOff

        // 주의사항
        string warn = "<color=#ffcc44>주의</color>   비슷한 오브젝트 구별  •  힘 조절 필수  •  제한 시간 30초";
        var warnT = Txt(card.transform, warn, 22, C_DIM, center: true);
        AnchorCenter(warnT, 880, 38, 0, y); y -= 60f;

        // 시작 버튼
        Btn(card.transform, "게임 시작!", y, () =>
        {
            HideIntro();
            GameManager.Instance?.StartGame();
        });
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 미션 팝업 빌드

    private void BuildMissionPopup(Transform canvasRoot)
    {
        // 어두운 배경 오버레이
        missionPopup = Panel(canvasRoot, new Color(0, 0, 0, 0.62f), true, "MissionPopup");

        // 팝업 카드
        var card = Panel(missionPopup.transform, C_BG_MID, false, "PopupCard");
        AnchorCenter(card, 720, 380);

        // 헤더 바
        var hdr = Panel(card.transform, C_ACCENT, false, "Header");
        var hRt = hdr.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0, 1); hRt.anchorMax = Vector2.one;
        hRt.pivot = new Vector2(0.5f, 1);
        hRt.sizeDelta = new Vector2(0, 58); hRt.anchoredPosition = Vector2.zero;
        var hTxt = Txt(hdr.transform, "새 미션!", 28, Color.white, bold: true, center: true);
        Stretch(hTxt, 0, 0);

        // 명령 텍스트
        var cmdGo = Txt(card.transform, "", 31, C_TEXT, bold: true, center: true);
        popupCommandText = cmdGo.GetComponent<TextMeshProUGUI>();
        popupCommandText.fontStyle = FontStyles.Italic;
        var cRt = cmdGo.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.04f, 0.38f); cRt.anchorMax = new Vector2(0.96f, 0.77f);
        cRt.sizeDelta = Vector2.zero; cRt.anchoredPosition = Vector2.zero;

        // 힌트 텍스트 (성공/실패 후 VLA 설명)
        var hintGo = Txt(card.transform, "", 17, new Color(1f, 0.85f, 0.35f), center: true);
        popupHintText = hintGo.GetComponent<TextMeshProUGUI>();
        var hRt2 = hintGo.GetComponent<RectTransform>();
        hRt2.anchorMin = new Vector2(0.04f, 0.16f); hRt2.anchorMax = new Vector2(0.96f, 0.38f);
        hRt2.sizeDelta = Vector2.zero; hRt2.anchoredPosition = Vector2.zero;

        // 시작 버튼
        Btn(card.transform, "알겠어요! 도전!", -152f, HideMissionPopup);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region HUD 빌드

    /// <summary>
    /// 우측 상단에 떠 있는 독립 타이머 박스 — TopBar 위에 별도로 그려져 가려지지 않는다.
    /// </summary>
    private void BuildFloatingTimer(Transform canvasRoot)
    {
        var box = new GameObject("FloatingTimer");
        box.transform.SetParent(canvasRoot, false);
        box.layer = LayerMask.NameToLayer("UI");
        var rt = box.AddComponent<RectTransform>();
        // 우측 상단 앵커 — 화면 비율 변해도 항상 우측 상단 고정
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(280, 90);
        rt.anchoredPosition = new Vector2(-30, -20);  // 우측 30, 위쪽 20 여백

        var bg = box.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.78f);
        bg.raycastTarget = false;

        // 노란 테두리 강조 — Image 자식으로 표현
        var border = new GameObject("Border");
        border.transform.SetParent(box.transform, false);
        var brt = border.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.sizeDelta = new Vector2(6, 6);
        brt.anchoredPosition = Vector2.zero;
        var bImg = border.AddComponent<Image>();
        bImg.color = new Color(1f, 0.82f, 0.3f, 0.35f);
        bImg.raycastTarget = false;

        var label = Txt(box.transform, "TIME", 18, new Color(0.95f, 0.95f, 1f), bold: true, center: true);
        var lrt = label.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0, 0.62f); lrt.anchorMax = new Vector2(1, 1f);
        lrt.sizeDelta = Vector2.zero; lrt.anchoredPosition = Vector2.zero;

        var num = Txt(box.transform, "20", 48, new Color(1f, 0.82f, 0.3f), bold: true, center: true);
        hudTimerText = num.GetComponent<TextMeshProUGUI>();
        var nrt = num.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0, 0f); nrt.anchorMax = new Vector2(1, 0.65f);
        nrt.sizeDelta = Vector2.zero; nrt.anchoredPosition = Vector2.zero;
    }

    private void BuildHUD(Transform canvasRoot)
    {
        hudPanel = Panel(canvasRoot, Color.clear, true, "HUD");

        // ── 상단 바 ──────────────────────────────────────────────────────────
        var top = Panel(hudPanel.transform, new Color(0, 0, 0, 0.58f), false, "TopBar");
        var tRt = top.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0, 1); tRt.anchorMax = Vector2.one;
        tRt.pivot = new Vector2(0.5f, 1);
        tRt.sizeDelta = new Vector2(0, 62); tRt.anchoredPosition = Vector2.zero;

        // 점수 (왼쪽)
        var scoreGo = Txt(top.transform, "Score: 0", 24, C_TEXT, bold: true);
        hudScoreText = scoreGo.GetComponent<TextMeshProUGUI>();
        StretchRegion(scoreGo, 0f, 0f, 0.22f, 1f, 20, 0);

        // 현재 미션 요약 (가운데)
        var misGo = Txt(top.transform, "미션 대기 중...", 17, C_DIM, center: true);
        hudMissionText = misGo.GetComponent<TextMeshProUGUI>();
        hudMissionText.alignment = TextAlignmentOptions.Midline;
        StretchRegion(misGo, 0.24f, 0f, 0.74f, 1f);

        // 타이머는 TopBar 와 무관하게 캔버스 우측 상단에 독립 배치 (가려짐 방지)
        BuildFloatingTimer(canvasRoot);

        // ── 하단 바 ──────────────────────────────────────────────────────────
        var bot = Panel(hudPanel.transform, new Color(0, 0, 0, 0.58f), false, "BotBar");
        var bRt = bot.GetComponent<RectTransform>();
        bRt.anchorMin = Vector2.zero; bRt.anchorMax = new Vector2(1, 0);
        bRt.pivot = new Vector2(0.5f, 0);
        bRt.sizeDelta = new Vector2(0, 68); bRt.anchoredPosition = Vector2.zero;

        // 그리퍼 힘 레이블
        var gLbl = Txt(bot.transform, "그리퍼 힘", 16, C_DIM);
        PivotLeft(gLbl, 130, 28, 18, 14);

        // 힘 값 텍스트
        var gVal = Txt(bot.transform, "5.0 / 10", 20, C_TEXT, bold: true);
        gripForceLabel = gVal.GetComponent<TextMeshProUGUI>();
        PivotLeft(gVal, 130, 28, 18, -14);

        // 게이지 배경
        var barBg = Panel(bot.transform, new Color(0.1f, 0.1f, 0.15f, 1f), false, "GripBG");
        PivotLeft(barBg, 300, 18, 160, 0);

        // 게이지 Fill (Image.Filled)
        var barFill = Panel(barBg.transform, new Color(0.3f, 0.85f, 0.3f), false, "GripFill");
        gripForceBar = barFill.GetComponent<Image>();
        gripForceBar.type = Image.Type.Filled;
        gripForceBar.fillMethod = Image.FillMethod.Horizontal;
        gripForceBar.fillAmount = 0.5f;
        Stretch(barFill, 0, 0);

        // 조작법 힌트 (오른쪽)
        string hint = "← → ↑ ↓  이동   |   Z  힘↓   |   X  힘↑   |   Space  집기 / 놓기";
        var hintGo = Txt(bot.transform, hint, 17, new Color(0.58f, 0.65f, 0.78f), center: true);
        StretchRegion(hintGo, 0.38f, 0f, 1f, 1f, -20, 0);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 결과 오버레이 빌드

    private void BuildResultOverlay(Transform canvasRoot)
    {
        resultOverlay = Panel(canvasRoot, Color.clear, true, "ResultOverlay");
        resultBg = resultOverlay.GetComponent<Image>();

        var rGo = Txt(resultOverlay.transform, "", 64, Color.white, bold: true, center: true);
        resultText = rGo.GetComponent<TextMeshProUGUI>();
        StretchRegion(rGo, 0f, 0.38f, 1f, 0.68f);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 공개 API

    // ── 인트로 ───────────────────────────────────────────────────────────────
    public void ShowIntro() { introPanel.SetActive(true); }
    public void HideIntro() { introPanel.SetActive(false); }

    // ── 미션 팝업 ─────────────────────────────────────────────────────────────
    public void ShowMissionPopup(MissionManager.Mission mission)
    {
        if (popupCommandText != null) popupCommandText.text = $"\"{mission.commandText}\"";
        if (popupHintText    != null) popupHintText.text    = "";
        if (hudMissionText   != null) hudMissionText.text   = $"명령: {mission.commandText}";
        missionPopup.SetActive(true);
    }

    /// <summary>
    /// 조명/환경 변화 등 게임 진행 중 환경 이벤트 팝업.
    /// 미션 팝업 UI를 재활용해 title/message 만 갈아끼움. 3.5초 후 자동 닫힘.
    /// </summary>
    public void ShowEnvironmentPopup(string title, string message)
    {
        if (popupCommandText != null)
        {
            popupCommandText.text = title;
            popupCommandText.fontStyle = FontStyles.Bold;
        }
        if (popupHintText != null)
            popupHintText.text = message;
        missionPopup.SetActive(true);
        CancelInvoke(nameof(HideInsightOnly));
        Invoke(nameof(HideInsightOnly), 3.5f);
    }

    public void ShowVLAInsight(string explanation)
    {
        if (popupHintText != null)
            popupHintText.text = $"VLA 인사이트:\n{explanation}";
        missionPopup.SetActive(true);
        // HideInsightOnly 사용 — OnMissionPopupClosed() 호출 안 함 (타이머 이미 종료됨)
        CancelInvoke(nameof(HideInsightOnly));
        Invoke(nameof(HideInsightOnly), 4.0f);
    }

    /// <summary>
    /// VLA 인사이트 팝업만 닫는다.
    /// HideMissionPopup과 달리 GameManager.OnMissionPopupClosed()를 호출하지 않으므로
    /// 이미 종료된 타이머를 재시작하지 않는다.
    /// </summary>
    private void HideInsightOnly()
    {
        missionPopup?.SetActive(false);
    }

    public void HideMissionPopup()
    {
        CancelInvoke(nameof(HideInsightOnly));  // 혹시 대기 중이던 인사이트 자동 닫기 취소
        missionPopup?.SetActive(false);
        // 플레이어가 직접 버튼을 눌렀을 때만 타이머 시작
        GameManager.Instance?.OnMissionPopupClosed();
    }

    // ── 결과 피드백 ───────────────────────────────────────────────────────────
    public void ShowResultFeedback(bool success, string msg)
    {
        resultBg.color  = success ? C_SUCCESS : C_FAIL;
        resultText.text = success ? $"성공!\n{msg}" : $"실패\n{msg}";
        resultOverlay.SetActive(true);
        Invoke(nameof(HideResult), 1.6f);
    }

    private void HideResult() { resultOverlay.SetActive(false); }

    // ── HUD 갱신 ─────────────────────────────────────────────────────────────
    public void UpdateScore(int score)
    {
        if (hudScoreText != null) hudScoreText.text = $"Score: {score}";
    }

    public void UpdateTimer(float timeLeft)
    {
        if (hudTimerText == null) return;
        int s = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
        hudTimerText.text  = s.ToString();
        hudTimerText.color = timeLeft < 5f
            ? new Color(1f, 0.30f, 0.25f)
            : (timeLeft < 10f ? new Color(1f, 0.55f, 0.20f)
                              : new Color(1f, 0.82f, 0.30f));
    }

    public void UpdateGripForce(float gripForce, float maxGrip)
    {
        float r = Mathf.Clamp01(gripForce / maxGrip);
        if (gripForceBar   != null) gripForceBar.fillAmount = r;
        if (gripForceBar   != null) gripForceBar.color =
            r < 0.25f ? Color.yellow : r > 0.80f ? Color.red : new Color(0.3f, 0.85f, 0.3f);
        if (gripForceLabel != null) gripForceLabel.text = $"{gripForce:F1} / {maxGrip:F0}";
    }

    // ── 게임 오버 ────────────────────────────────────────────────────────────
    public void ShowGameOver(int finalScore)
    {
        HideMissionPopup();
        resultBg.color  = new Color(0.04f, 0.05f, 0.14f, 0.96f);
        resultText.text = $"게임 종료!\n\n최종 점수: <color=#88ccff>{finalScore}</color>\n\n" +
                          "VLA 로봇이 겪는 어려움을\n직접 느껴보셨나요? 🤖";
        resultText.fontSize = 40;
        resultOverlay.SetActive(true);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region UI 빌드 헬퍼

    // 풀스크린 또는 일반 패널
    private GameObject Panel(Transform p, Color col, bool fullscreen, string name = "Panel")
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.AddComponent<RectTransform>();
        if (fullscreen) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero; }
        var img = go.AddComponent<Image>();
        img.color = col;
        img.raycastTarget = false;  // 패널 배경은 클릭 차단 안 함 — 버튼만 raycastTarget=true
        return go;
    }

    // 텍스트
    private GameObject Txt(Transform p, string text, int size, Color col, bool bold = false, bool center = false)
    {
        var go = new GameObject("Txt");
        go.transform.SetParent(p, false);
        go.layer = LayerMask.NameToLayer("UI");
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = col;
        tmp.enableWordWrapping = true; tmp.richText = true;
        tmp.alignment = center ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        if (koreanFont != null) tmp.font = koreanFont;  // 한글 폰트 적용
        return go;
    }

    // 버튼
    private void Btn(Transform p, string label, float yOff, System.Action cb)
    {
        var go = new GameObject("Btn_" + label);
        go.transform.SetParent(p, false);
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(280, 58); rt.anchoredPosition = new Vector2(0, yOff);

        var img = go.AddComponent<Image>();
        img.color = C_BTN;
        img.raycastTarget = true;   // 버튼은 반드시 클릭 감지 활성화
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var cols = btn.colors;
        cols.highlightedColor = new Color(0.38f, 0.72f, 1f);
        cols.pressedColor     = new Color(0.14f, 0.44f, 0.82f);
        btn.colors = cols;
        btn.onClick.AddListener(() => cb?.Invoke());

        var tGo = Txt(go.transform, label, 24, Color.white, bold: true, center: true);
        Stretch(tGo, 0, 0);
    }

    // ── RectTransform 유틸 ──────────────────────────────────────────────────
    private void AnchorCenter(GameObject go, float w, float h, float x = 0, float y = 0)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, y);
    }

    private void Stretch(GameObject go, float padX, float padY)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.sizeDelta = new Vector2(-padX * 2, -padY * 2); rt.anchoredPosition = Vector2.zero;
    }

    private void StretchRegion(GameObject go, float ax, float ay, float bx, float by, float ox = 0, float oy = 0)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(ax, ay); rt.anchorMax = new Vector2(bx, by);
        rt.sizeDelta = Vector2.zero; rt.anchoredPosition = new Vector2(ox, oy);
    }

    private void PivotLeft(GameObject go, float w, float h, float x, float y)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, y);
    }

    private void Divider(Transform p, float y)
    {
        var go = Panel(p, new Color(0.3f, 0.45f, 0.7f, 0.45f), false, "Divider");
        AnchorCenter(go, 760, 2, 0, y);
    }

    #endregion
}
