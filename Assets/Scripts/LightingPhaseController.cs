using System.Collections;
using UnityEngine;

/// <summary>
/// 게임 중간에 조명을 점차 변화시켜 색 구별을 어렵게 만드는 환경 이벤트 시스템.
/// 미션 사이에 GameManager 가 호출하면 팝업 메시지와 함께 조명을 페이드 전환한다.
///
/// 페이즈 흐름:
///   Day(낮)        — 중성 따뜻 (기본)
///   Evening(저녁)  — 황금빛
///   Sunset(석양)   — 짙은 주황·붉은빛
///   Dusk(노을지나)  — 어두운 파란빛 (저조도)
///   Night(밤)      — 어둠 + 약한 푸른빛
/// </summary>
public class LightingPhaseController : MonoBehaviour
{
    public static LightingPhaseController Instance { get; private set; }

    [System.Serializable]
    public struct Phase
    {
        public string popupTitle;
        public string popupMessage;
        public Color  mainColor;
        public float  mainIntensity;
        public Color  fillColor;
        public float  fillIntensity;
        public Color  ambient;
    }

    [Header("페이즈 시퀀스 (자동 초기화)")]
    [SerializeField] private Phase[] phases;

    [Header("씬 라이트 참조 (자동 탐색)")]
    [SerializeField] private Light mainLight;
    [SerializeField] private Light fillLight;

    [Header("전환 시간")]
    [SerializeField] private float fadeDuration = 2.0f;

    private int currentPhaseIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (phases == null || phases.Length == 0) InitDefaultPhases();
    }

    private void Start()
    {
        // 씬에 이미 생성된 조명 자동 연결
        var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
        {
            if (l.name == "FillLight") fillLight = l;
            else if (l.type == LightType.Directional) mainLight = l;
        }

        // 초기 페이즈를 즉시 적용 (페이드 없이)
        if (phases.Length > 0) ApplyPhaseInstant(phases[0]);
    }

    private void InitDefaultPhases()
    {
        phases = new Phase[]
        {
            new Phase {
                popupTitle = "낮", popupMessage = "",
                mainColor = new Color(1.00f, 0.92f, 0.75f), mainIntensity = 1.15f,
                fillColor = new Color(0.55f, 0.65f, 0.80f), fillIntensity = 0.35f,
                ambient   = new Color(0.20f, 0.20f, 0.25f)
            },
            new Phase {
                popupTitle = "저녁이 다가오고 있어요",
                popupMessage =
                    "햇빛이 노랗게 변하면서 색이 약간씩 달라 보일 수 있어요.\n" +
                    "비슷한 색 물체를 더 주의 깊게 구별하세요.",
                mainColor = new Color(1.00f, 0.78f, 0.52f), mainIntensity = 1.10f,
                fillColor = new Color(0.50f, 0.58f, 0.78f), fillIntensity = 0.30f,
                ambient   = new Color(0.22f, 0.18f, 0.20f)
            },
            new Phase {
                popupTitle = "석양이 지고 있어서 조명이 변하고 있어요",
                popupMessage =
                    "붉은 빛이 강해져 빨강·주황 계열 물체가 거의 같아 보입니다.\n" +
                    "VLA는 이런 조명 변화에서 색 구별이 크게 흔들립니다.",
                mainColor = new Color(1.00f, 0.55f, 0.32f), mainIntensity = 1.00f,
                fillColor = new Color(0.42f, 0.40f, 0.68f), fillIntensity = 0.25f,
                ambient   = new Color(0.22f, 0.14f, 0.16f)
            },
            new Phase {
                popupTitle = "해가 졌어요",
                popupMessage =
                    "푸른빛이 강해지면서 초록·파랑 계열이 헷갈리기 시작합니다.\n" +
                    "조명에 적응해 행동하는 능력이 시험에 들어갑니다.",
                mainColor = new Color(0.50f, 0.55f, 0.85f), mainIntensity = 0.75f,
                fillColor = new Color(0.30f, 0.40f, 0.72f), fillIntensity = 0.40f,
                ambient   = new Color(0.10f, 0.12f, 0.22f)
            },
        };
    }

    /// <summary>
    /// 다음 페이즈로 전환 (페이드). 이미 마지막 페이즈면 무시.
    /// 팝업 표시 → 페이드 완료 후 onComplete 콜백.
    /// </summary>
    public bool AdvanceToNextPhase(System.Action onComplete = null)
    {
        if (currentPhaseIndex + 1 >= phases.Length)
        {
            onComplete?.Invoke();
            return false;
        }

        currentPhaseIndex++;
        var phase = phases[currentPhaseIndex];

        UIManager.Instance?.ShowEnvironmentPopup(phase.popupTitle, phase.popupMessage);
        StartCoroutine(FadeToPhase(phase, onComplete));
        return true;
    }

    public bool HasMorePhases() => currentPhaseIndex + 1 < phases.Length;

    private void ApplyPhaseInstant(Phase p)
    {
        if (mainLight != null) { mainLight.color = p.mainColor; mainLight.intensity = p.mainIntensity; }
        if (fillLight != null) { fillLight.color = p.fillColor; fillLight.intensity = p.fillIntensity; }
        RenderSettings.ambientLight = p.ambient;
    }

    private IEnumerator FadeToPhase(Phase target, System.Action onComplete)
    {
        Color mc0 = mainLight != null ? mainLight.color : target.mainColor;
        float mi0 = mainLight != null ? mainLight.intensity : target.mainIntensity;
        Color fc0 = fillLight != null ? fillLight.color : target.fillColor;
        float fi0 = fillLight != null ? fillLight.intensity : target.fillIntensity;
        Color am0 = RenderSettings.ambientLight;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);
            if (mainLight != null) {
                mainLight.color     = Color.Lerp(mc0, target.mainColor, k);
                mainLight.intensity = Mathf.Lerp(mi0, target.mainIntensity, k);
            }
            if (fillLight != null) {
                fillLight.color     = Color.Lerp(fc0, target.fillColor, k);
                fillLight.intensity = Mathf.Lerp(fi0, target.fillIntensity, k);
            }
            RenderSettings.ambientLight = Color.Lerp(am0, target.ambient, k);
            yield return null;
        }

        onComplete?.Invoke();
    }
}
