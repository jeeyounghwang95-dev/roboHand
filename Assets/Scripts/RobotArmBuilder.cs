using UnityEngine;

/// <summary>
/// 로봇 팔 비주얼을 런타임에 절차적으로 생성한다.
/// RobotArmController, GripperPhysics 와 같은 GameObject에 붙인다.
///
/// 좌표 설계
///   ─ 팔 transform(= 어깨/슬라이더) 이 위아래·좌우로 이동
///   ─ 팔 비주얼은 transform 에서 아래 방향으로 뻗음
///   ─ GripPoint 는 손가락 끝 (transform 기준 약 -2.05 Y)
///
///   transform.y = 3.2  →  gripPoint.y ≈ 1.23  (책상 위 여유)
///   transform.y = 2.15 →  gripPoint.y ≈ 0.18  (책상 표면 위, 손가락 끝 ≈ 0.08)
///   transform.y = 2.0  →  gripPoint.y ≈ 0.03  (손가락 끝 ≈ -0.07, 책상 클리핑!)
///
/// 생성 계층
///   RobotArm (이동하는 루트)
///     ├─ ShoulderCap   — 회색 납작 원기둥 (슬라이더 연결부)
///     ├─ ArmBody       — 파란 긴 원기둥   (상완+전완 통합)
///     ├─ Wrist         — 회색 구          (손목 관절)
///     └─ GripperHolder — 빈 트랜스폼 (손목 위치)
///          ├─ GripperLeft   — 빨간 박스 핑거
///          ├─ GripperRight  — 빨간 박스 핑거
///          └─ GripPoint     — 빈 트랜스폼 (집기 기준)
/// </summary>
[RequireComponent(typeof(RobotArmController))]
[RequireComponent(typeof(GripperPhysics))]
public class RobotArmBuilder : MonoBehaviour
{
    [Header("자동 생성")]
    [SerializeField] private bool autoGenerate = true;

    [Header("팔 치수")]
    // 인형뽑기 형태: 평소엔 팔이 짧아 손가락 끝이 책상 위 ~0.9m에 떠 있고,
    // Enter 키로 내려와 집은 뒤 다시 올라간다 (RobotArmController 가 Y 제어).
    [SerializeField] private float armLength      = 0.85f;  // 어깨→손목 (인형뽑기용: 짧게)
    [SerializeField] private float armDiameter    = 0.13f;
    [SerializeField] private float fingerLength   = 0.28f;
    [SerializeField] private float fingerWidth    = 0.07f;
    [SerializeField] private float fingerOpenDist = 0.16f;  // 초기 벌림 거리 (반폭)

    [Header("색상")]
    [SerializeField] private Color colBody    = new Color(0.22f, 0.22f, 0.28f);
    [SerializeField] private Color colArm     = new Color(0.18f, 0.44f, 0.78f);
    [SerializeField] private Color colGripper = new Color(0.88f, 0.22f, 0.18f);

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        // 팔 Y는 고정 — 손가락 끝이 책상 표면(SURF_Y = -0.05) 위에 오도록 계산
        // armLength=1.70, holderOffset≈0.091, fingerLength=0.28, tipOffset=0.04 → 총 ≈2.071
        // ARM_FIXED_Y = 2.15 → 손가락 끝 Y ≈ 0.08 (책상 표면 위 여유)
        // ARM_FIXED_Y = 2.0  → 손가락 끝 Y ≈ -0.07 (책상 아래로 클리핑!)
        const float ARM_FIXED_Y = 2.15f;
        transform.position = new Vector3(0f, ARM_FIXED_Y, 0.3f);

        if (autoGenerate) BuildArm();
    }

    // ─────────────────────────────────────────────────────────────────────────
    #region 팔 생성

    private void BuildArm()
    {
        // ── Kinematic Rigidbody — 팔 루트에 추가해 물리 엔진 기반 이동 활성화 ──
        // RobotArmController.FixedUpdate에서 rb.MovePosition()으로 이동하면
        // 그리퍼 손가락 Collider가 물체를 실제로 밀거나 쓰러뜨릴 수 있다.
        var armRb = gameObject.GetComponent<Rigidbody>();
        if (armRb == null) armRb = gameObject.AddComponent<Rigidbody>();
        armRb.isKinematic   = true;
        armRb.useGravity    = false;
        armRb.interpolation = RigidbodyInterpolation.Interpolate;  // 부드러운 시각 이동

        // ── 어깨 캡 (슬라이더 연결 시각 표시) ──────────────────────────────
        Part(PrimitiveType.Cylinder, "ShoulderCap",
             localPos: Vector3.zero,
             scale: new Vector3(0.22f, 0.055f, 0.22f),
             col: colBody);

        // ── 팔 몸통 (아래로 뻗는 원기둥) ────────────────────────────────────
        float bodyCenter = -armLength * 0.5f;
        Part(PrimitiveType.Cylinder, "ArmBody",
             localPos: new Vector3(0f, bodyCenter, 0f),
             scale: new Vector3(armDiameter, armLength * 0.5f, armDiameter),  // Cylinder half-height
             col: colArm);

        // ── 손목 관절 ────────────────────────────────────────────────────────
        float wristY = -armLength;
        Part(PrimitiveType.Sphere, "Wrist",
             localPos: new Vector3(0f, wristY, 0f),
             scale: Vector3.one * (armDiameter * 1.4f),
             col: colBody);

        // ── GripperHolder (빈 트랜스폼 — 손목 위치) ─────────────────────────
        float holderY = wristY - armDiameter * 0.7f;
        var holder = new GameObject("GripperHolder");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = new Vector3(0f, holderY, 0f);

        // ── 핑거 Left / Right ────────────────────────────────────────────────
        // keepCollider: true — 그리퍼 손가락은 Collider를 유지해 물체를 물리적으로 밀 수 있게 함
        float fingerCenterY = -fingerLength * 0.5f;  // 홀더 기준

        var gripLeft = Part(PrimitiveType.Cube, "GripperLeft",
            localPos: new Vector3(-fingerOpenDist, fingerCenterY, 0f),
            scale: new Vector3(fingerWidth, fingerLength, fingerWidth),
            col: colGripper,
            parent: holder.transform,
            keepCollider: true);

        var gripRight = Part(PrimitiveType.Cube, "GripperRight",
            localPos: new Vector3(fingerOpenDist, fingerCenterY, 0f),
            scale: new Vector3(fingerWidth, fingerLength, fingerWidth),
            col: colGripper,
            parent: holder.transform,
            keepCollider: true);

        // ── GripPoint (핑거 끝 중앙) ─────────────────────────────────────────
        var gripPoint = new GameObject("GripPoint");
        gripPoint.transform.SetParent(holder.transform, false);
        gripPoint.transform.localPosition = new Vector3(0f, -fingerLength * 0.5f - 0.04f, 0f);

        // ── RobotArmController 에 필드 주입 ─────────────────────────────────
        var ctrl = GetComponent<RobotArmController>();
        if (ctrl != null)
        {
            SetField(ctrl, "gripperLeft",  gripLeft.transform);
            SetField(ctrl, "gripperRight", gripRight.transform);
            SetField(ctrl, "gripPoint",    gripPoint.transform);
            // X: 책상 좌우 풀 범위 (책상은 -3.5~3.5)
            SetField(ctrl, "xBounds",      new Vector2(-3.30f, 3.30f));
            // Z: 화면 위아래(책상 앞/안쪽) 범위 — 책상 Z: -1.0 ~ 2.6
            SetField(ctrl, "zBounds",      new Vector2(-0.90f, 2.50f));
            SetField(ctrl, "moveSpeed",    5.0f);
            SetField(ctrl, "depthSpeed",   5.0f);
            SetField(ctrl, "grabRadius",   0.55f);  // 책상 위 물체 집기 여유 확보
            SetField(ctrl, "gripperOpenDistance",  fingerOpenDist);
            SetField(ctrl, "gripperCloseDistance", 0.04f);
            SetField(ctrl, "interactableLayer", ~0);
        }

        Debug.Log("[RobotArmBuilder] 팔 생성 완료 — GripPoint world Y ≈ " +
                  (transform.position.y + holderY - fingerLength * 0.5f - 0.04f).ToString("F2"));
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 헬퍼

    private GameObject Part(PrimitiveType type, string name,
                             Vector3 localPos, Vector3 scale, Color col,
                             Transform parent = null, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent ?? transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = scale;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
            rend.material = new Material(rend.sharedMaterial) { color = col };

        // keepCollider=false: 팔 몸통·손목 등 시각 파트는 충돌 불필요 → 끔
        // keepCollider=true : 그리퍼 손가락은 물체를 물리적으로 밀어야 하므로 유지
        var col2 = go.GetComponent<Collider>();
        if (col2 != null) col2.enabled = keepCollider;

        return go;
    }

    private void SetField(object obj, string field, object value)
    {
        var f = obj.GetType().GetField(
            field, System.Reflection.BindingFlags.NonPublic |
                   System.Reflection.BindingFlags.Instance);
        f?.SetValue(obj, value);
    }

    #endregion
}
