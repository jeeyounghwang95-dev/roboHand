using UnityEngine;

/// <summary>
/// 로봇 팔이 집을 수 있는 모든 오브젝트에 부착하는 공통 컴포넌트.
/// 물체의 속성(라벨, 무게, 힘 임계값)과 상태(집힘/미끄러짐/찌그러짐)를 관리한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ObjectInteractable : MonoBehaviour
{
    // ── Inspector 설정 ───────────────────────────────────────────────────────
    [Header("오브젝트 식별")]
    [Tooltip("미션 판정에 사용되는 태그 (MissionManager.Mission.targetObjectTag 와 매칭)")]
    [SerializeField] private string objectTag = "Untagged";

    [Tooltip("UI 및 로그에 표시할 이름")]
    [SerializeField] private string objectLabel = "물체";

    [Header("물리 속성")]
    [SerializeField] private float mass = 1f;                  // 무게 (kg 단위 가정)
    [SerializeField] private float friction = 0.5f;            // 표면 마찰 계수 (0~1)

    [Header("힘 임계값 오버라이드 (0이면 GripperPhysics 기본값 사용)")]
    [SerializeField] private float overrideSlipThreshold  = 0f;
    [SerializeField] private float overrideCrushThreshold = 0f;

    [Header("취약도")]
    [Tooltip("true면 찌그러질 수 있는 물체 (계란, 종이컵 등)")]
    [SerializeField] private bool isCrushable = true;

    [Tooltip("true면 미끄러질 수 있는 물체 (유리컵, 금속 물체 등)")]
    [SerializeField] private bool isSlippery = true;

    [Header("시각 노이즈 설정 (교육 목적)")]
    [Tooltip("조명에 따라 색이 달라 보이게 할 기본 색상")]
    [SerializeField] private Color baseColor = Color.white;

    // ── 런타임 상태 ──────────────────────────────────────────────────────────
    public enum ObjectState { Idle, Held, Slipped, Crushed, Placed }
    public ObjectState CurrentState { get; private set; } = ObjectState.Idle;

    private Rigidbody rb;
    private Renderer objectRenderer;
    private Vector3 originalScale;
    private Color originalColor;
    private GripperPhysics gripperPhysics;

    // ── 공개 프로퍼티 ────────────────────────────────────────────────────────
    public string ObjectTag   => objectTag;
    public string ObjectLabel => objectLabel;

    /// <summary>
    /// ObjectSpawner 등 코드에서 동적으로 속성을 설정할 때 사용.
    /// Awake 이후에 호출해야 rb 참조가 유효하다.
    /// </summary>
    public void Initialize(string tag, string label,
                           float slipThresh = 0f, float crushThresh = 0f,
                           bool crushable = true, bool slippery = true,
                           float objectMass = 0.4f)
    {
        objectTag              = tag;
        objectLabel            = label;
        overrideSlipThreshold  = slipThresh;
        overrideCrushThreshold = crushThresh;
        isCrushable            = crushable;
        isSlippery             = slippery;
        if (rb != null) rb.mass = objectMass;
    }
    public float  OverrideSlipThreshold  => overrideSlipThreshold;
    public float  OverrideCrushThreshold => overrideCrushThreshold;
    public bool   IsCrushable => isCrushable;
    public bool   IsSlippery  => isSlippery;

    // ─────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        objectRenderer = GetComponent<Renderer>();
        originalScale = transform.localScale;
        originalColor = objectRenderer != null ? objectRenderer.material.color : baseColor;

        // Rigidbody 기본 설정
        rb.mass = mass;

        // TODO: PhysicMaterial로 friction 값 적용
        // TODO: 충돌 감지 모드를 Continuous로 설정 (빠른 이동 시 터널링 방지)
    }

    private void Start()
    {
        // GripperPhysics를 씬에서 찾아 참조
        gripperPhysics = FindObjectOfType<GripperPhysics>();

        ApplyLightingColorNoise();

        // TODO: 풀링(Object Pooling) 시스템 연동으로 오브젝트 재사용
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 상태 전환 콜백 (RobotArmController에서 호출)

    /// <summary>집혔을 때 호출. Rigidbody를 Kinematic으로 전환해 물리 비활성화.</summary>
    public void OnGrabbed(float gripForce)
    {
        CurrentState = ObjectState.Held;
        rb.isKinematic = true;       // 그리퍼에 붙어 이동하도록
        rb.useGravity  = false;

        Debug.Log($"[ObjectInteractable] '{objectLabel}' 집힘 (힘: {gripForce:F1})");

        // TODO: 집힘 효과음 재생 (물체 종류별 다른 사운드)
        // TODO: 집힌 오브젝트 아웃라인 하이라이트 활성화
    }

    /// <summary>놓였을 때 호출. Rigidbody 물리 복원.</summary>
    public void OnReleased()
    {
        CurrentState = ObjectState.Idle;
        rb.isKinematic = false;
        rb.useGravity  = true;

        Debug.Log($"[ObjectInteractable] '{objectLabel}' 놓임");

        // TODO: 놓기 효과음
        // TODO: 하이라이트 비활성화
    }

    /// <summary>미끄러질 때 호출. GripperPhysics에게 효과 적용 위임.</summary>
    public void OnSlipped()
    {
        if (!isSlippery) return;

        CurrentState = ObjectState.Slipped;
        rb.isKinematic = false;
        rb.useGravity  = true;

        if (gripperPhysics != null)
            gripperPhysics.ApplySlipEffect(this);

        Debug.Log($"[ObjectInteractable] '{objectLabel}' 미끄러짐!");

        // TODO: 미끄러짐 경고 UI (화면 테두리 빨간 flash)
        // TODO: 슬로우모션 연출로 교육 효과 강조
    }

    /// <summary>찌그러질 때 호출. GripperPhysics에게 효과 적용 위임.</summary>
    public void OnCrushed()
    {
        if (!isCrushable) return;

        CurrentState = ObjectState.Crushed;

        if (gripperPhysics != null)
            gripperPhysics.ApplyCrushEffect(this);

        Debug.Log($"[ObjectInteractable] '{objectLabel}' 찌그러짐!");

        // TODO: 찌그러짐 피드백 UI
        // TODO: 찌그러진 후 일정 시간 뒤 오브젝트 교체/리셋
    }

    /// <summary>그릇에 성공적으로 배치됐을 때 호출.</summary>
    public void OnPlaced()
    {
        CurrentState = ObjectState.Placed;
        rb.isKinematic = true;  // 그릇 안에서 움직이지 않도록

        // TODO: 배치 성공 파티클 (반짝임 효과)
        // TODO: 배치된 오브젝트 서서히 정착하는 애니메이션
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 시각 노이즈 (교육 목적)

    /// <summary>
    /// 조명 변화에 따라 색이 달라 보이는 효과 적용.
    /// VLA가 같은 물체를 다르게 인식할 수 있음을 체험하게 한다.
    /// </summary>
    private void ApplyLightingColorNoise()
    {
        if (objectRenderer == null) return;

        // TODO: 씬의 방향광(Directional Light) 색온도에 따라 물체 색상 보정
        // TODO: 주변 조명 변화(LightProbe 또는 커스텀 셰이더)와 연동
        // TODO: 플레이어가 색 착각을 경험하는 교육 이벤트 트리거
    }

    /// <summary>오브젝트를 초기 상태로 리셋한다 (미션 재시작 시 사용).</summary>
    public void ResetObject(Vector3 resetPosition)
    {
        CurrentState = ObjectState.Idle;
        transform.position = resetPosition;
        transform.rotation = Quaternion.identity;  // 회전도 초기화
        transform.localScale = originalScale;

        rb.isKinematic = false;
        rb.useGravity  = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (objectRenderer != null)
            objectRenderer.material.color = originalColor;

        // TODO: 리셋 시 스폰 애니메이션 (페이드 인 또는 낙하)
    }


    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 기즈모

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
        // TODO: 힘 임계값 구간을 색으로 표시하는 커스텀 에디터 GUI 추가
    }

    #endregion
}

// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// 그릇(Bowl) 오브젝트에 부착하는 컴포넌트.
/// 물체가 이 위에 놓이면 MissionManager에 판정을 요청한다.
/// </summary>
public class BowlReceiver : MonoBehaviour
{
    [SerializeField] private string bowlTag = "AnyBowl";
    public string BowlTag => bowlTag;

    /// <summary>ObjectSpawner 에서 코드로 태그를 설정할 때 사용.</summary>
    public void Initialize(string tag) { bowlTag = tag; }

    /// <summary>
    /// Trigger 방식으로 물체 감지 — 물체를 손에서 놓으면 그릇 안에 있는지 자동 판정.
    /// RobotArmController 의 Raycast 방식 보완.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        var obj = other.GetComponent<ObjectInteractable>();
        if (obj == null) return;

        // 들고 있거나 이미 다른 그릇에 배치된 물체는 무시
        var state = obj.CurrentState;
        if (state == ObjectInteractable.ObjectState.Held ||
            state == ObjectInteractable.ObjectState.Placed) return;

        // GameManager가 MissionActive 상태일 때만 판정
        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState != GameManager.GameState.MissionActive) return;

        MissionManager.Instance?.EvaluatePlacement(obj.ObjectTag, bowlTag);
    }

    // TODO: 정답 그릇 하이라이트 시스템 (힌트 모드)
    // TODO: 조명 색상 노이즈 — 빨간 그릇이 따뜻한 조명에서 주황으로 보이는 셰이더
}
