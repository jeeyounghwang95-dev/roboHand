using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 애매한 자연어 미션을 관리한다.
/// 미션 출제 → UIManager.ShowMissionPopup → 플레이어 행동 → EvaluatePlacement.
///
/// 오브젝트 태그는 ObjectSpawner 가 생성하는 Initialize() 태그와 완전히 일치해야 한다.
/// </summary>
public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance { get; private set; }

    // ── 미션 데이터 구조 ─────────────────────────────────────────────────────
    [System.Serializable]
    public class Mission
    {
        public string commandText;            // 플레이어에게 표시할 자연어 명령
        public string targetObjectTag;        // 정답 오브젝트 태그
        public string targetBowlTag;          // 정답 그릇 태그
        public string educationalExplanation; // VLA 인사이트 해설
    }

    [Header("미션 풀 (Inspector에서 추가 가능)")]
    [SerializeField] private List<Mission> missionPool = new List<Mission>();

    private Mission      currentMission;
    private List<int>    issuedIndices = new List<int>();

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        InitDefaultMissionPool();
    }

    // ─────────────────────────────────────────────────────────────────────────
    #region 미션 풀 초기화
    //
    // 오브젝트 태그는 ObjectSpawner.SpawnInteractable 의 tag 인수와 일치.
    // 그릇 태그는 ObjectSpawner.SpawnBowl 의 bowlTag 인수와 일치.
    //
    private void InitDefaultMissionPool()
    {
        if (missionPool.Count > 0) return;

        // ── 빨간 계열 혼동 (사과/석류/망고) ──────────────────────────────────
        missionPool.Add(new Mission
        {
            commandText = "저기 빨간 과일 집어서 빨간 그릇에 넣어줘",
            targetObjectTag = "Apple",
            targetBowlTag   = "RedBowl",
            educationalExplanation =
                "'빨간 과일'이 사과인지 석류인지 망고인지 VLA는 구분하기 어렵고,\n" +
                "빨간 그릇과 주황 그릇도 따뜻한 조명 아래서는 헷갈립니다."
        });

        missionPool.Add(new Mission
        {
            commandText = "씨앗 많은 과일 주황 그릇에 담아줘",
            targetObjectTag = "Pomegranate",
            targetBowlTag   = "OrangeBowl",
            educationalExplanation =
                "석류는 외관상 사과와 비슷한 둥근 붉은 과일이지만\n" +
                "'씨앗 많은'이라는 내부 속성은 시각만으로는 판단 불가능합니다."
        });

        missionPool.Add(new Mission
        {
            commandText = "노랑-주황 빛 도는 그 과일 빨간 그릇에 넣어줄래?",
            targetObjectTag = "Mango",
            targetBowlTag   = "RedBowl",
            educationalExplanation =
                "망고는 빨강·노랑·주황이 섞여 있어 색 기반 분류가 흔들립니다.\n" +
                "같은 과일이라도 익은 정도에 따라 색이 크게 달라집니다."
        });

        // ── 주황 계열 혼동 (오렌지/한라봉) ──────────────────────────────────
        missionPool.Add(new Mission
        {
            commandText = "둥근 오렌지색 과일 파란 그릇에 좀 넣어줄래?",
            targetObjectTag = "Orange",
            targetBowlTag   = "BlueBowl",
            educationalExplanation =
                "오렌지와 한라봉은 색·형태가 유사해 '오렌지색'이라는 기준만으로는 부족합니다."
        });

        missionPool.Add(new Mission
        {
            commandText = "꼭지가 봉긋한 주황 과일 주황 그릇에 넣어봐",
            targetObjectTag = "Hallabong",
            targetBowlTag   = "OrangeBowl",
            educationalExplanation =
                "한라봉은 위쪽에 꼭지가 튀어나온 것이 오렌지와의 핵심 차이이지만,\n" +
                "각도·조명에 따라 그 특징이 잘 안 보일 수 있습니다."
        });

        // ── 초록 계열 혼동 (브로콜리/아티초크) ───────────────────────────────
        missionPool.Add(new Mission
        {
            commandText = "초록색 야채 파란 그릇에 옮겨줘",
            targetObjectTag = "Broccoli",
            targetBowlTag   = "BlueBowl",
            educationalExplanation =
                "브로콜리와 아티초크는 둘 다 초록색이라 '초록 야채'만으로는\n" +
                "VLA가 무엇을 가리키는지 결정하기 어렵습니다."
        });

        missionPool.Add(new Mission
        {
            commandText = "꽃잎처럼 겹친 초록 거 남색 그릇에 담아줘",
            targetObjectTag = "Artichoke",
            targetBowlTag   = "NavyBowl",
            educationalExplanation =
                "아티초크의 '꽃잎처럼 겹친' 외형은 사람에겐 직관적이지만\n" +
                "VLA에게는 미세한 텍스처 차이를 정확히 인식해야 하는 과제입니다."
        });

        // ── 컵/병 계열 혼동 ──────────────────────────────────────────────────
        missionPool.Add(new Mission
        {
            commandText = "파란 컵 파란 그릇에 넣어줘",
            targetObjectTag = "BlueCup",
            targetBowlTag   = "BlueBowl",
            educationalExplanation =
                "파란 컵과 청록 컵, 파란 그릇과 남색 그릇은 조명에 따라\n" +
                "색 차이가 미미해져 VLA의 색상 분류가 흔들립니다."
        });

        missionPool.Add(new Mission
        {
            commandText = "청록색 컵 남색 그릇 쪽으로 옮겨줄래?",
            targetObjectTag = "TealCup",
            targetBowlTag   = "NavyBowl",
            educationalExplanation =
                "'청록색'과 '파란색'은 조명·카메라 화이트밸런스에 따라\n" +
                "VLA 모델이 다르게 인식할 수 있습니다."
        });

        // ── 직육면체 계열 혼동 ───────────────────────────────────────────────
        missionPool.Add(new Mission
        {
            commandText = "네모난 거 하나 집어서 빨간 그릇에 담아줘",
            targetObjectTag = "YellowBlock",
            targetBowlTag   = "RedBowl",
            educationalExplanation =
                "노란 블록과 비누는 형태가 유사해 '네모난 거'라는\n" +
                "명령만으로는 VLA가 어느 것인지 알 수 없습니다."
        });

        missionPool.Add(new Mission
        {
            commandText = "미끄러울 것 같은 물건 집어서 주황 쪽에 넣어",
            targetObjectTag = "Soap",
            targetBowlTag   = "OrangeBowl",
            educationalExplanation =
                "VLA는 물체가 미끄러운지 시각만으로 판단할 수 없습니다.\n" +
                "촉각 센서 없이는 재질 추론에 한계가 있습니다."
        });

        // TODO: CSV/JSON 외부 파일 로드 지원 추가
        // TODO: 난이도 Easy/Hard 구분 — Hard는 색 힌트 없이 형태만 설명
    }
    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 미션 출제

    /// <summary>중복 없이 랜덤 미션을 출제하고 UIManager에 팝업을 요청한다.</summary>
    public void IssueRandomMission()
    {
        if (issuedIndices.Count >= missionPool.Count)
        {
            issuedIndices.Clear();
            Debug.Log("[MissionManager] 미션 풀 재사용");
        }

        int idx;
        do { idx = Random.Range(0, missionPool.Count); }
        while (issuedIndices.Contains(idx));

        issuedIndices.Add(idx);
        currentMission = missionPool[idx];

        Debug.Log($"[MissionManager] 출제: {currentMission.commandText}");
        UIManager.Instance?.ShowMissionPopup(currentMission);
        // 플레이어가 팝업의 '알겠어요!' 버튼을 누르면 UIManager → HideMissionPopup →
        // GameManager.OnMissionPopupClosed() 를 호출해 타이머 시작.
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 성공/실패 판정

    /// <summary>
    /// RobotArmController 가 물체를 그릇 위에 놓을 때 호출.
    /// 태그 매칭이 성공하면 GameManager.ReportSuccess, 실패하면 ReportFailure.
    /// </summary>
    public void EvaluatePlacement(string objectTag, string bowlTag,
                                   ObjectInteractable placedObj = null)
    {
        if (currentMission == null) return;

        bool objOk  = objectTag == currentMission.targetObjectTag
                      || currentMission.targetObjectTag == "AnyObject";
        bool bowlOk = bowlTag   == currentMission.targetBowlTag
                      || currentMission.targetBowlTag   == "AnyBowl";

        if (objOk && bowlOk)
        {
            // 물체를 그릇 안에 고정 — 튀어나가거나 굴러도 Placed 상태로 잠금
            placedObj?.OnPlaced();
            GameManager.Instance?.ReportSuccess();
            UIManager.Instance?.ShowVLAInsight(currentMission.educationalExplanation);
        }
        else
        {
            string reason = !objOk  ? "잘못된 물체를 집었어요"
                          : !bowlOk ? "잘못된 그릇에 넣었어요"
                          : "오답";
            GameManager.Instance?.ReportFailure(reason);
        }
    }

    public Mission GetCurrentMission() => currentMission;

    #endregion
}
