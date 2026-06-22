using UnityEngine;

/// <summary>
/// 게임 전체 흐름을 관리하는 싱글턴.
/// 상태: Intro → MissionActive ↔ MissionSuccess / MissionFail → GameOver
/// UIManager, MissionManager와 협력하여 점수·타이머·미션 흐름을 제어한다.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ── 싱글턴 ──────────────────────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    // ── 게임 상태 ────────────────────────────────────────────────────────────
    public enum GameState { Intro, MissionActive, MissionSuccess, MissionFail, GameOver }
    public GameState CurrentState { get; private set; } = GameState.Intro;

    // ── Inspector 설정 ───────────────────────────────────────────────────────
    [Header("미션 설정")]
    [SerializeField] private int   totalMissions   = 9;
    [SerializeField] private float missionTimeLimit = 30f;  // 인트로 안내문과 일치 + 재시도 여유

    [Header("점수 설정")]
    [SerializeField] private int scorePerSuccess = 100;
    [SerializeField] private int scorePenaltyFail = 30;

    // ── 런타임 상태 ──────────────────────────────────────────────────────────
    private int   currentScore       = 0;
    private int   completedMissions  = 0;
    private float missionTimer       = 0f;
    private bool  timerRunning       = false;

    // ─────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 환경(조명) 페이즈 시스템 자동 부착
        if (LightingPhaseController.Instance == null)
            gameObject.AddComponent<LightingPhaseController>();
    }

    private void Start()
    {
        // UIManager가 Start에서 인트로를 표시하므로 여기서는 대기
        ChangeState(GameState.Intro);
    }

    private void Update()
    {
        if (!timerRunning) return;
        missionTimer -= Time.deltaTime;
        UIManager.Instance?.UpdateTimer(missionTimer);
        if (missionTimer <= 0f) ReportFailure("시간 초과");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 게임 흐름

    /// <summary>인트로 화면의 '게임 시작' 버튼에서 호출.</summary>
    public void StartGame()
    {
        currentScore      = 0;
        completedMissions = 0;
        StartNewMission();
    }

    /// <summary>다음 미션을 시작한다. 총 미션 수를 소진하면 게임 오버.</summary>
    public void StartNewMission()
    {
        if (completedMissions >= totalMissions) { TriggerGameOver(); return; }

        // 일정 미션마다 조명 페이즈 전환 (저녁→석양→밤)
        // completedMissions 가 3, 5, 7 일 때 다음 페이즈로
        bool triggerLightingEvent =
            completedMissions > 0 &&
            completedMissions % 2 == 1 &&
            LightingPhaseController.Instance != null &&
            LightingPhaseController.Instance.HasMorePhases();

        if (triggerLightingEvent)
        {
            // 페이즈 팝업이 닫힌 뒤(약 3.5s) 실제 미션 출제로 이어짐
            LightingPhaseController.Instance.AdvanceToNextPhase(() =>
                Invoke(nameof(IssueMissionAfterEvent), 0.8f)
            );
        }
        else
        {
            IssueMissionAfterEvent();
        }
    }

    /// <summary>조명 이벤트 팝업이 끝난 뒤 실제 미션을 출제한다.</summary>
    private void IssueMissionAfterEvent()
    {
        ObjectSpawner.Instance?.ResetObjects();
        missionTimer = missionTimeLimit;
        timerRunning = false;
        ChangeState(GameState.MissionActive);
        MissionManager.Instance?.IssueRandomMission();
    }

    /// <summary>
    /// MissionManager.IssueRandomMission 이 팝업을 표시한 뒤,
    /// 플레이어가 '알겠어요!' 버튼을 누르면 여기서 타이머를 시작한다.
    /// MissionActive 상태가 아닐 때 호출되어도 안전하도록 가드 처리.
    /// </summary>
    public void OnMissionPopupClosed()
    {
        if (CurrentState == GameState.MissionActive)
            timerRunning = true;
    }

    /// <summary>미션 성공.</summary>
    public void ReportSuccess()
    {
        if (CurrentState != GameState.MissionActive) return;

        timerRunning = false;
        currentScore += scorePerSuccess;
        completedMissions++;
        ChangeState(GameState.MissionSuccess);

        UIManager.Instance?.ShowResultFeedback(true, $"+{scorePerSuccess} 점!");
        UIManager.Instance?.UpdateScore(currentScore);

        // VLA 인사이트 팝업(4 s) 표시 후 여유를 두고 다음 미션 시작
        Invoke(nameof(StartNewMission), 4.5f);
    }

    /// <summary>
    /// 집기 힘 실수(미끄러짐/찌그러짐) — 미션을 실패시키지 않고 재시도하게 한다.
    /// 상태·점수를 바꾸지 않고 짧은 힌트만 표시하므로 제한 시간 안에서 다시 도전 가능.
    /// </summary>
    public void ReportGripRetry(string hint)
    {
        if (CurrentState != GameState.MissionActive) return;
        UIManager.Instance?.ShowRetryFeedback(hint);
        Debug.Log($"[GameManager] 재시도: {hint}");
    }

    /// <summary>미션 실패.</summary>
    public void ReportFailure(string reason)
    {
        if (CurrentState != GameState.MissionActive) return;

        timerRunning = false;
        currentScore = Mathf.Max(0, currentScore - scorePenaltyFail);
        ChangeState(GameState.MissionFail);

        UIManager.Instance?.ShowResultFeedback(false, reason);
        UIManager.Instance?.UpdateScore(currentScore);

        Debug.Log($"[GameManager] 실패: {reason}");
        Invoke(nameof(StartNewMission), 2.2f);
    }

    private void TriggerGameOver()
    {
        timerRunning = false;
        ChangeState(GameState.GameOver);
        UIManager.Instance?.ShowGameOver(currentScore);

        // TODO: 재시작 버튼 연결
    }

    private void ChangeState(GameState s)
    {
        CurrentState = s;
        Debug.Log($"[GameManager] → {s}");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 공개 유틸

    public int   GetScore()            => currentScore;
    public int   GetCompletedMissions()=> completedMissions;
    public int   GetTotalMissions()    => totalMissions;
    public float GetMissionTimer()     => missionTimer;

    #endregion
}
