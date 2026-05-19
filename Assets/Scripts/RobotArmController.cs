using UnityEngine;
using UnityEngine.InputSystem;  // ← 신 Input System 패키지

/// <summary>
/// 플레이어(로봇 팔) 입력 및 이동을 처리한다.
/// - ←→ 방향키 : 그리퍼 X축 이동 (좌우)
/// - ↑↓ 방향키 : 그리퍼 Z축 이동 (화면 위=책상 안쪽/Z+, 화면 아래=앞쪽/Z-)
///               ※ 팔 Y(높이)는 고정 — 그리퍼가 항상 책상 표면 레벨 유지
/// - Z / X     : 그리퍼 힘(Grip Force) 감소 / 증가
/// - Space     : 집기(Grab) / 놓기(Release) 토글
///
/// 물리 상호작용:
///   팔 루트에 Kinematic Rigidbody가 있고, 그리퍼 손가락 Collider가 활성화되어 있어
///   이동 시 테이블 위 물체를 실제로 밀거나 쓰러뜨릴 수 있다.
///   이동은 FixedUpdate에서 rb.MovePosition()으로 처리해 물리 연산이 제대로 이루어진다.
/// </summary>
[RequireComponent(typeof(GripperPhysics))]
public class RobotArmController : MonoBehaviour
{
    // ── Inspector 설정 ───────────────────────────────────────────────────────
    [Header("이동 설정")]
    [SerializeField] private float moveSpeed  = 3f;
    [SerializeField] private float depthSpeed = 3f;  // Z축(화면 위아래) 속도

    [Tooltip("X: 좌우 범위,  Z: 화면 위아래(책상 앞뒤) 범위")]
    [SerializeField] private Vector2 xBounds = new Vector2(-4f, 4f);
    [SerializeField] private Vector2 zBounds = new Vector2(-1f, 2.5f);

    [Header("그리퍼 힘 설정")]
    [SerializeField] private float minGripForce = 0f;
    [SerializeField] private float maxGripForce = 10f;
    [SerializeField] private float gripForceStep = 0.5f;   // Z/X 키 1회 누름당 변화량
    [SerializeField] private float initialGripForce = 5f;

    [Header("그리퍼 시각화")]
    [SerializeField] private Transform gripperLeft;
    [SerializeField] private Transform gripperRight;
    [SerializeField] private float gripperOpenDistance = 0.5f;   // 열린 상태 간격
    [SerializeField] private float gripperCloseDistance = 0.05f; // 닫힌 상태 간격

    [Header("집기 감지")]
    [SerializeField] private Transform gripPoint;          // 집기 감지 기준점
    [SerializeField] private float grabRadius = 0.3f;
    [SerializeField] private LayerMask interactableLayer;

    // ── 런타임 상태 ──────────────────────────────────────────────────────────
    private float currentGripForce;
    private bool  isHolding  = false;
    private ObjectInteractable heldObject = null;

    private GripperPhysics gripperPhysics;

    // ── 인형뽑기 클로 메커니즘 ───────────────────────────────────────────────
    private enum ClawState { Hover, Descending, AtBottom, Ascending }
    private ClawState clawState = ClawState.Hover;

    [Header("인형뽑기 동작")]
    [SerializeField] private float clawDescendDistance = 0.85f;   // 내려가는 거리(m)
    [SerializeField] private float clawSpeed           = 1.6f;    // 상하 이동 속도(m/s)
    [SerializeField] private float clawBottomHoldTime  = 0.25f;   // 바닥에서 잡기 처리 대기(s)

    private float hoverY;
    private float bottomY;
    private float atBottomTimer = 0f;
    private bool  bottomActionTaken = false;

    // ── 물리 이동용 ──────────────────────────────────────────────────────────
    private Rigidbody armRb;
    private Vector3   targetPosition;        // 매 Update에서 계산한 목표 위치
    private Vector3   gripPointLocalOffset;  // gripPoint의 arm root 기준 로컬 오프셋

    // ── UI 참조 (선택) ───────────────────────────────────────────────────────
    [Header("UI (선택)")]
    [SerializeField] private UnityEngine.UI.Slider gripForceSlider;
    [SerializeField] private TMPro.TextMeshProUGUI gripForceLabel;

    // ─────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        gripperPhysics   = GetComponent<GripperPhysics>();
        currentGripForce = initialGripForce;
        if (interactableLayer == 0) interactableLayer = ~0;

        // Rigidbody는 RobotArmBuilder.Awake()에서 추가됨.
        // 실행 순서에 따라 이미 존재할 수도, 없을 수도 있으므로 여기서도 보정.
        armRb = GetComponent<Rigidbody>();
        if (armRb == null)
        {
            armRb = gameObject.AddComponent<Rigidbody>();
            armRb.isKinematic = true;
            armRb.useGravity  = false;
        }
        // targetPosition은 Start()에서 초기화 — Awake() 실행 순서 문제로
        // RobotArmBuilder.Awake()가 Y를 세팅하기 전에 캡처하면 팔이 책상 아래로 처짐
    }

    private void Start()
    {
        // 모든 Awake() 완료 후 실행 — RobotArmBuilder가 Y=2.15로 세팅한 위치를 올바르게 읽음
        targetPosition = transform.position;

        // 인형뽑기 상하 이동 범위 설정 — 현재 Y를 호버 기준으로 잡음
        hoverY  = transform.position.y;
        bottomY = hoverY - clawDescendDistance;

        // RobotArmBuilder.Awake()에서 gripPoint가 생성된 뒤 오프셋 계산
        if (gripPoint != null)
            gripPointLocalOffset = gripPoint.position - transform.position;
    }

    private void Update()
    {
        bool gameActive = GameManager.Instance == null
            || GameManager.Instance.CurrentState == GameManager.GameState.MissionActive;
        if (!gameActive) return;

        HandleMovement();       // XZ는 Hover 상태일 때만 갱신
        HandleGripForceInput();
        HandleClawInput();      // Enter 입력 처리
        UpdateClawY();          // 상하 이동 진행 (state machine)
        UpdateGripperVisuals();
        UpdateGripForceUI();
    }

    private void FixedUpdate()
    {
        // Kinematic Rigidbody를 MovePosition으로 이동:
        // transform.position 직접 대입과 달리, 물리 엔진이 스윕 충돌을 계산해
        // 그리퍼 손가락이 물체를 밀거나 쓰러뜨리는 힘을 올바르게 생성한다.
        if (armRb != null)
            armRb.MovePosition(targetPosition);

        // 들고 있는 물체를 gripPoint 위치로 이동 (kinematic)
        if (isHolding && heldObject != null)
        {
            var heldRb = heldObject.GetComponent<Rigidbody>();
            if (heldRb != null)
                heldRb.MovePosition(targetPosition + gripPointLocalOffset);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 이동

    private void HandleMovement()
    {
        // 인형뽑기: 호버(상단) 상태일 때만 XZ 이동 허용
        if (clawState != ClawState.Hover) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        // ←→ : X축(좌우),  ↑↓ : Z축(화면 위=책상 안쪽/Z+, 화면 아래=앞쪽/Z-)
        float h = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed  ? 1f : 0f);
        float d = (kb.upArrowKey.isPressed    ? 1f : 0f) - (kb.downArrowKey.isPressed   ? 1f : 0f);

        Vector3 newPos = targetPosition;
        newPos.x = Mathf.Clamp(newPos.x + h * moveSpeed  * Time.deltaTime, xBounds.x, xBounds.y);
        newPos.z = Mathf.Clamp(newPos.z + d * depthSpeed * Time.deltaTime, zBounds.x, zBounds.y);
        newPos.y = hoverY;  // 호버 상태에선 Y 고정
        targetPosition = newPos;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 그리퍼 힘 조절

    private void HandleGripForceInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.zKey.wasPressedThisFrame)  // Z: 힘 감소
        {
            currentGripForce = Mathf.Max(minGripForce, currentGripForce - gripForceStep);
            Debug.Log($"[RobotArmController] Grip Force: {currentGripForce:F1}");

            if (isHolding && heldObject != null)
                gripperPhysics.EvaluateGrip(currentGripForce, heldObject);
        }

        if (kb.xKey.wasPressedThisFrame)  // X: 힘 증가
        {
            currentGripForce = Mathf.Min(maxGripForce, currentGripForce + gripForceStep);
            Debug.Log($"[RobotArmController] Grip Force: {currentGripForce:F1}");

            if (isHolding && heldObject != null)
                gripperPhysics.EvaluateGrip(currentGripForce, heldObject);
        }

        // TODO: 힘 수치 실시간 피드백 사운드 (낮음/적정/높음 구간별 다른 효과음)
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 집기 / 놓기

    /// <summary>Enter 키: 호버 상태에서만 클로 시퀀스를 시작한다.</summary>
    private void HandleClawInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        bool enterPressed =
            kb.enterKey.wasPressedThisFrame ||
            kb.numpadEnterKey.wasPressedThisFrame ||
            kb.spaceKey.wasPressedThisFrame;   // Space도 클로 시퀀스 트리거 (호환성)

        if (!enterPressed) return;

        Debug.Log($"[RobotArm] Claw 트리거! 현재 상태={clawState}");

        if (clawState != ClawState.Hover) return;
        clawState = ClawState.Descending;
    }

    /// <summary>매 프레임 클로의 Y축 이동과 상태 전이를 처리한다.</summary>
    private void UpdateClawY()
    {
        if (clawState == ClawState.Hover) return;

        Vector3 p = targetPosition;

        switch (clawState)
        {
            case ClawState.Descending:
                p.y -= clawSpeed * Time.deltaTime;
                if (p.y <= bottomY)
                {
                    p.y = bottomY;
                    atBottomTimer = 0f;
                    bottomActionTaken = false;
                    clawState = ClawState.AtBottom;
                }
                break;

            case ClawState.AtBottom:
                // 진입 직후 1회만 잡기/놓기 실행
                if (!bottomActionTaken)
                {
                    bottomActionTaken = true;
                    if (isHolding) Release();
                    else           TryGrab();
                }
                atBottomTimer += Time.deltaTime;
                if (atBottomTimer >= clawBottomHoldTime)
                    clawState = ClawState.Ascending;
                break;

            case ClawState.Ascending:
                p.y += clawSpeed * Time.deltaTime;
                if (p.y >= hoverY)
                {
                    p.y = hoverY;
                    clawState = ClawState.Hover;
                }
                break;
        }

        targetPosition = p;
    }

    private void TryGrab()
    {
        Vector3 center = gripPoint != null ? gripPoint.position : transform.position;
        Collider[] hits = Physics.OverlapSphere(center, grabRadius, interactableLayer);

        if (hits.Length == 0)
        {
            Debug.Log("[RobotArmController] 주변에 집을 수 있는 물체 없음");
            return;
        }

        // 가장 가까운 ObjectInteractable 선택 (그리퍼 자체 파트는 null 체크로 무시됨)
        ObjectInteractable closest = null;
        float minDist = float.MaxValue;
        foreach (var hit in hits)
        {
            var obj = hit.GetComponent<ObjectInteractable>();
            if (obj == null) continue;  // 그리퍼 파트, 책상 등 비대상 필터링
            float dist = Vector3.Distance(center, hit.transform.position);
            if (dist < minDist) { minDist = dist; closest = obj; }
        }

        if (closest == null) return;

        GripperPhysics.GripResult result = gripperPhysics.EvaluateGrip(currentGripForce, closest);

        if (result == GripperPhysics.GripResult.Success)
        {
            heldObject = closest;
            isHolding  = true;
            heldObject.OnGrabbed(currentGripForce);
            // 집기 시점 gripPointLocalOffset 갱신 (gripPoint 위치가 정확한지 보장)
            if (gripPoint != null)
                gripPointLocalOffset = gripPoint.position - transform.position;
            Debug.Log($"[RobotArmController] {heldObject.ObjectLabel} 집기 성공");
        }
        else if (result == GripperPhysics.GripResult.TooWeak)
        {
            Debug.Log("[RobotArmController] 힘이 너무 약해 미끄러짐");
            closest.OnSlipped();
            GameManager.Instance?.ReportFailure("힘이 너무 약해서 미끄러짐");
        }
        else if (result == GripperPhysics.GripResult.TooStrong)
        {
            Debug.Log("[RobotArmController] 힘이 너무 강해 찌그러짐");
            closest.OnCrushed();
            GameManager.Instance?.ReportFailure("힘이 너무 강해서 찌그러짐");
        }
    }

    private void Release()
    {
        if (heldObject == null) { isHolding = false; return; }

        heldObject.OnReleased();
        heldObject = null;
        isHolding  = false;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 시각화 & UI

    private void UpdateGripperVisuals()
    {
        if (gripperLeft == null || gripperRight == null) return;

        float t    = currentGripForce / maxGripForce;  // 0 ~ 1
        float dist = Mathf.Lerp(gripperOpenDistance, gripperCloseDistance, t);

        gripperLeft.localPosition  = new Vector3(-dist, 0f, 0f);
        gripperRight.localPosition = new Vector3( dist, 0f, 0f);
    }

    private void UpdateGripForceUI()
    {
        if (gripForceSlider != null)
            gripForceSlider.value = currentGripForce / maxGripForce;
        if (gripForceLabel != null)
            gripForceLabel.text = $"Grip: {currentGripForce:F1} / {maxGripForce}";

        UIManager.Instance?.UpdateGripForce(currentGripForce, maxGripForce);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 기즈모

    private void OnDrawGizmosSelected()
    {
        if (gripPoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(gripPoint.position, grabRadius);
    }

    #endregion

    public float CurrentGripForce => currentGripForce;
    public bool  IsHolding        => isHolding;
}
