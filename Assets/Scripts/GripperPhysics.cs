using UnityEngine;

/// <summary>
/// 그리퍼 힘(Grip Force)에 따른 물리적 결과를 판정한다.
/// - 너무 약함 → 미끄러짐 (Slip)
/// - 적정 범위 → 집기 성공 (Success)
/// - 너무 강함 → 찌그러짐 (Crush)
///
/// 핵심 교육 목표: 로봇이 물체를 집을 때 힘/각도 조절이 얼마나 어려운지 체험.
/// </summary>
public class GripperPhysics : MonoBehaviour
{
    // ── 판정 결과 열거형 ─────────────────────────────────────────────────────
    public enum GripResult { Success, TooWeak, TooStrong }

    // ── Inspector 설정 ───────────────────────────────────────────────────────
    [Header("전역 힘 임계값 (오브젝트별 오버라이드 가능)")]
    [SerializeField] private float defaultSlipThreshold  = 2.0f;  // 이 미만 → 미끄러짐
    [SerializeField] private float defaultCrushThreshold = 8.0f;  // 이 초과 → 찌그러짐

    [Header("미끄러짐 파라미터")]
    [Tooltip("미끄러질 때 오브젝트에 가할 랜덤 속도 크기")]
    [SerializeField] private float slipImpulseMagnitude = 2f;

    [Header("찌그러짐 파라미터")]
    [Tooltip("찌그러질 때 오브젝트 스케일 감소 비율 (0~1)")]
    [SerializeField] private float crushScaleRatio = 0.6f;

    [Header("이펙트 (선택)")]
    [SerializeField] private ParticleSystem slipParticle;
    [SerializeField] private ParticleSystem crushParticle;

    // ─────────────────────────────────────────────────────────────────────────
    #region 핵심 판정 로직

    /// <summary>
    /// 주어진 힘과 오브젝트 속성을 바탕으로 집기 결과를 반환한다.
    /// ObjectInteractable의 개별 임계값이 있으면 우선 적용.
    /// </summary>
    public GripResult EvaluateGrip(float gripForce, ObjectInteractable obj)
    {
        float slipThresh  = obj.OverrideSlipThreshold  > 0 ? obj.OverrideSlipThreshold  : defaultSlipThreshold;
        float crushThresh = obj.OverrideCrushThreshold > 0 ? obj.OverrideCrushThreshold : defaultCrushThreshold;

        // TODO: 오브젝트 무게(mass)와 마찰 계수(friction)를 고려한 임계값 보정
        //       예) 무거운 물체는 슬립 임계값이 높아짐
        // TODO: 그리퍼 접촉 각도(angle) 에러 시뮬레이션
        //       — 비스듬히 접근하면 유효 힘이 cos(θ)만큼 감소

        if (gripForce < slipThresh)
            return GripResult.TooWeak;

        if (gripForce > crushThresh)
            return GripResult.TooStrong;

        return GripResult.Success;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 물리 효과 적용

    /// <summary>미끄러짐 효과 — 오브젝트에 랜덤 임펄스를 가한다.</summary>
    public void ApplySlipEffect(ObjectInteractable obj)
    {
        var rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 randomDir = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-0.5f, 0f),  // 주로 아래로 떨어짐
                Random.Range(-0.5f, 0.5f)
            ).normalized;
            rb.AddForce(randomDir * slipImpulseMagnitude, ForceMode.Impulse);
        }

        // 파티클
        if (slipParticle != null)
            Instantiate(slipParticle, obj.transform.position, Quaternion.identity).Play();

        // TODO: 미끄러짐 효과음 재생 (AudioSource.PlayClipAtPoint)
        // TODO: 미끄러짐 트레일 이펙트 (오브젝트에 Trail Renderer 일시 활성화)
    }

    /// <summary>찌그러짐 효과 — 오브젝트 스케일을 줄이고 형태 변형을 시각화한다.</summary>
    public void ApplyCrushEffect(ObjectInteractable obj)
    {
        // 스케일 Y축 압축으로 찌그러짐 표현
        Vector3 original = obj.transform.localScale;
        obj.transform.localScale = new Vector3(
            original.x * (1f + (1f - crushScaleRatio) * 0.3f), // X 약간 팽창
            original.y * crushScaleRatio,                        // Y 압축
            original.z * (1f + (1f - crushScaleRatio) * 0.3f)  // Z 약간 팽창
        );

        // 재질 색상 변경 (갈색/회색으로)
        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            // TODO: Material.Lerp로 부드러운 색상 전환 구현
            renderer.material.color = Color.Lerp(renderer.material.color, Color.gray, 0.6f);
        }

        // 파티클
        if (crushParticle != null)
            Instantiate(crushParticle, obj.transform.position, Quaternion.identity).Play();

        // TODO: 찌그러짐 효과음 재생
        // TODO: Mesh Deformation — 버텍스 레벨 변형으로 더 사실적인 찌그러짐 표현
        // TODO: 찌그러진 오브젝트를 "망가진 상태"로 마킹해 재사용 방지
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 노이즈 시뮬레이션

    /// <summary>
    /// 센서 노이즈 시뮬레이션.
    /// 실제 로봇은 힘 센서에 오차가 있어 의도한 힘이 정확히 전달되지 않음.
    /// </summary>
    public float AddSensorNoise(float gripForce, float noiseStdDev = 0.3f)
    {
        // 가우시안 근사 (Box-Muller 간소화)
        float u1 = Random.value;
        float u2 = Random.value;
        float noise = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        float noisyForce = gripForce + noise * noiseStdDev;

        // TODO: 노이즈 레벨을 난이도에 따라 동적으로 조절
        // TODO: 노이즈 적용 여부를 Inspector 토글로 제어

        return Mathf.Max(0f, noisyForce);
    }

    #endregion
}
