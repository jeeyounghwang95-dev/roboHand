using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 전체 구조를 런타임에 절차적으로 생성한다.
///   - 책상 (갈색 Cube)
///   - 유사하게 생긴 인터랙터블 오브젝트 9종 (MissionManager 태그와 일치)
///   - 비슷한 색의 그릇(Bowl) 4종
///   - 조명 설정 (따뜻한 색온도 → 색 착각 체험)
///   - 카메라 시점
///   - 팔 레일(시각적 트랙) 오브젝트
///
/// VLA 교육 포인트:
///   빨간 사과·토마토, 주황 오렌지·귤, 파란·청록 컵처럼
///   색깔·모양이 유사한 물체들을 배치해 명령 모호성을 직접 체험.
///   조명이 따뜻해 빨간 그릇이 주황으로 보일 수 있음.
/// </summary>
public class ObjectSpawner : MonoBehaviour
{
    public static ObjectSpawner Instance { get; private set; }

    // ── 레이아웃 상수 ────────────────────────────────────────────────────────
    // 책상 윗면 Y = DESK_Y + 0.05f (Cube 반높이)
    private const float DESK_Y    = -0.1f;
    private const float SURF_Y    =  DESK_Y + 0.05f;   // 책상 표면 Y

    // ── 오브젝트 리셋용 추적 ─────────────────────────────────────────────────
    private readonly List<(ObjectInteractable obj, Vector3 pos)> spawnedInteractables
        = new List<(ObjectInteractable, Vector3)>();

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        SpawnDesk();
        SpawnObjects();
        SpawnBowls();
        SpawnRail();
        SetupCamera();
        SetupLighting();
    }

    /// <summary>미션 전환 시 모든 오브젝트를 초기 위치로 리셋한다.</summary>
    public void ResetObjects()
    {
        foreach (var (obj, pos) in spawnedInteractables)
            if (obj != null) obj.ResetObject(pos);
    }

    /// <summary>
    /// 특정 오브젝트 하나만 초기 위치로 되돌린다 (집기 재시도용).
    /// delay > 0 이면 미끄러짐/찌그러짐 효과를 잠깐 보여준 뒤 복구한다.
    /// </summary>
    public void ResetSingleObject(ObjectInteractable target, float delay = 0f)
    {
        if (target == null) return;
        if (delay > 0f) StartCoroutine(ResetSingleAfter(target, delay));
        else            ResetSingleNow(target);
    }

    private System.Collections.IEnumerator ResetSingleAfter(ObjectInteractable target, float delay)
    {
        yield return new WaitForSeconds(delay);
        ResetSingleNow(target);
    }

    private void ResetSingleNow(ObjectInteractable target)
    {
        foreach (var (obj, pos) in spawnedInteractables)
            if (obj == target) { obj.ResetObject(pos); return; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    #region 책상

    private void SpawnDesk()
    {
        var desk = Primitive(PrimitiveType.Cube, "Desk",
            new Vector3(0f, DESK_Y, 0.8f),
            new Vector3(7f, 0.10f, 3.6f),
            new Color(0.55f, 0.36f, 0.18f));   // 나무 갈색

        // 충돌 레이어 유지 — 책상 위에 오브젝트가 쌓이도록
        desk.layer = 0;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 인터랙터블 오브젝트 9종
    //
    // 의도적으로 비슷한 그룹 3개:
    //   Group A — 둥글고 빨간/주황/노랑 계열 (사과, 토마토, 오렌지, 귤)
    //   Group B — 원기둥 컵 계열 (파란 컵, 청록 컵, 남색 병)
    //   Group C — 직육면체 계열 (노란 블록, 흰 비누)
    //
    private void SpawnObjects()
    {
        // ── 겹침 방지 격자 배치 ────────────────────────────────────────────────
        // 12개 물체를 책상 왼편에 4열 × 3행(가로 0.8m, 세로 0.9m 간격) 격자로 배치.
        // 이전엔 좌표가 너무 가까워(예: 파란 컵과 망고가 0.22m) 스폰 즉시 서로 밀어내며
        // 가벼운 컵이 책상에서 튕겨 떨어졌다. 셀마다 하나씩 두어 초기 겹침을 제거한다.
        //   열 X: -3.0 / -2.2 / -1.4 / -0.6,   행 Z: 1.4(안쪽) / 0.5(중간) / -0.4(앞)

        // ── Group A: 과일/채소 (FBX 모델, Assets/Resources/Prefabs/Fruits/) ─────
        // 빨간 계열 ── 사과·석류·망고 (색깔·모양 혼동 유도)
        SpawnFruitPrefab("Apple", tag: "Apple", label: "사과",
            pos: new Vector3(-3.0f, SURF_Y + 0.10f, 1.4f),
            slipThresh: 2.5f, crushThresh: 5.5f, crushable: true, slippery: false);

        SpawnFruitPrefab("Pomegranate", tag: "Pomegranate", label: "석류",
            pos: new Vector3(-3.0f, SURF_Y + 0.10f, 0.5f),
            slipThresh: 2.5f, crushThresh: 6.5f, crushable: true, slippery: false);

        SpawnFruitPrefab("Mango", tag: "Mango", label: "망고",
            pos: new Vector3(-3.0f, SURF_Y + 0.10f, -0.4f),
            slipThresh: 2.2f, crushThresh: 5.0f, crushable: true, slippery: false);

        // 주황 계열 ── 오렌지·한라봉 (꼭지 유무로만 구분)
        SpawnFruitPrefab("Orange", tag: "Orange", label: "오렌지",
            pos: new Vector3(-2.2f, SURF_Y + 0.10f, 1.4f),
            slipThresh: 2.2f, crushThresh: 5.0f, crushable: true, slippery: false);

        SpawnFruitPrefab("Hallabong", tag: "Hallabong", label: "한라봉",
            pos: new Vector3(-2.2f, SURF_Y + 0.10f, 0.5f),
            slipThresh: 2.5f, crushThresh: 4.5f, crushable: true, slippery: false);

        // 초록 계열 ── 브로콜리·아티초크 (모양 유사, 단단함 차이)
        SpawnFruitPrefab("Broccoli", tag: "Broccoli", label: "브로콜리",
            pos: new Vector3(-1.4f, SURF_Y + 0.10f, 0.5f),
            slipThresh: 2.0f, crushThresh: 6.0f, crushable: true, slippery: false);

        SpawnFruitPrefab("Artichoke", tag: "Artichoke", label: "아티초크",
            pos: new Vector3(-2.2f, SURF_Y + 0.10f, -0.4f),
            slipThresh: 2.8f, crushThresh: 7.5f, crushable: false, slippery: false);

        // ── Group B: 원기둥 컵류 ─────────────────────────────────────────────
        // 파란 컵: 파란 원기둥
        SpawnInteractable(PrimitiveType.Cylinder, "BlueCup",
            tag: "BlueCup", label: "파란 컵",
            pos: new Vector3(-1.4f, SURF_Y + 0.15f, -0.4f),
            scale: new Vector3(0.18f, 0.15f, 0.18f),
            col: new Color(0.15f, 0.40f, 0.90f),
            slipThresh: 3.5f, crushThresh: 7.0f, crushable: false, slippery: true);

        // 청록 컵: 파란 컵과 거의 동일한 크기·형태, 색만 약간 다름!
        SpawnInteractable(PrimitiveType.Cylinder, "TealCup",
            tag: "TealCup", label: "청록 컵",
            pos: new Vector3(-1.4f, SURF_Y + 0.15f, 1.4f),
            scale: new Vector3(0.18f, 0.15f, 0.18f),
            col: new Color(0.10f, 0.65f, 0.72f),
            slipThresh: 3.5f, crushThresh: 7.0f, crushable: false, slippery: true);

        // 남색 병: 더 얇고 긴 원기둥 (파란 컵과 헷갈림)
        SpawnInteractable(PrimitiveType.Cylinder, "NavyBottle",
            tag: "NavyBottle", label: "남색 병",
            pos: new Vector3(-0.6f, SURF_Y + 0.22f, 0.5f),
            scale: new Vector3(0.13f, 0.22f, 0.13f),
            col: new Color(0.08f, 0.15f, 0.55f),
            slipThresh: 4.0f, crushThresh: 8.5f, crushable: false, slippery: true);

        // ── Group C: 직육면체류 ───────────────────────────────────────────────
        // 노란 블록: 노란 큐브
        SpawnInteractable(PrimitiveType.Cube, "YellowBlock",
            tag: "YellowBlock", label: "노란 블록",
            pos: new Vector3(-0.6f, SURF_Y + 0.07f, -0.4f),
            scale: new Vector3(0.22f, 0.14f, 0.18f),
            col: new Color(0.98f, 0.88f, 0.10f),
            slipThresh: 1.5f, crushThresh: 9.0f, crushable: false, slippery: false);

        // 비누: 흰색(약간 노랑기) 큐브 — 블록과 형태 유사, 미끄러움!
        SpawnInteractable(PrimitiveType.Cube, "Soap",
            tag: "Soap", label: "비누",
            pos: new Vector3(-0.6f, SURF_Y + 0.065f, 1.4f),
            scale: new Vector3(0.20f, 0.13f, 0.16f),
            col: new Color(0.96f, 0.95f, 0.80f),
            slipThresh: 4.5f, crushThresh: 8.0f, crushable: false, slippery: true);
    }

    private void SpawnInteractable(
        PrimitiveType type, string name, string tag, string label,
        Vector3 pos, Vector3 scale, Color col,
        float slipThresh, float crushThresh, bool crushable, bool slippery)
    {
        var go = Primitive(type, name, pos, scale, col);

        // Rigidbody — 그리퍼에 닿았을 때 자연스럽게 밀리거나 쓰러지도록
        // linearDamping 낮춤 (0.6→0.2): 밀릴 때 더 잘 미끄러짐
        // angularDamping 낮춤 (1.0→0.4): 쓰러지거나 굴러가는 회전 저항 감소
        var rb = go.AddComponent<Rigidbody>();
        rb.mass           = 0.35f;
        rb.linearDamping  = 0.20f;
        rb.angularDamping = 0.40f;

        // 인터랙터블 컴포넌트
        var interactable = go.AddComponent<ObjectInteractable>();
        interactable.Initialize(tag, label, slipThresh, crushThresh, crushable, slippery);

        go.layer = 0;  // Default layer — OverlapSphere로 감지

        // 리셋용으로 초기 위치 기록
        spawnedInteractables.Add((interactable, pos));
    }

    /// <summary>
    /// Resources/Prefabs/Fruits/ 폴더의 FBX 프리팹을 로드해 인터랙터블로 셋업.
    /// 프리팹에는 Rigidbody + MeshCollider(convex)가 베이크되어 있어야 한다.
    /// </summary>
    private void SpawnFruitPrefab(
        string prefabName, string tag, string label,
        Vector3 pos,
        float slipThresh, float crushThresh, bool crushable, bool slippery)
    {
        var prefab = Resources.Load<GameObject>("Prefabs/Fruits/" + prefabName);
        if (prefab == null)
        {
            Debug.LogError($"[ObjectSpawner] Missing prefab: Resources/Prefabs/Fruits/{prefabName}");
            return;
        }

        var go = Instantiate(prefab);
        go.name = prefabName;
        go.transform.position = pos;
        go.layer = 0;

        // Rigidbody는 프리팹에 베이크돼 있지만 일관성을 위해 값 다시 설정
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass           = 0.35f;
        rb.linearDamping  = 0.20f;
        rb.angularDamping = 0.40f;

        var interactable = go.AddComponent<ObjectInteractable>();
        interactable.Initialize(tag, label, slipThresh, crushThresh, crushable, slippery);

        spawnedInteractables.Add((interactable, pos));
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 그릇(Bowl) 4종
    //
    // 교육 목적: 비슷한 색 그릇들 — 조명에 따라 혼동 유발
    //   빨간 그릇 vs 주황 그릇  (따뜻한 조명에서 구분 어려움)
    //   파란 그릇 vs 남색 그릇  (어두운 쪽 빛에서 구분 어려움)
    //
    private void SpawnBowls()
    {
        // 그릇 2×2 배치 — 책상 오른편, 간격 1.0 (그릇 외폭 ≈0.68 이므로 충분)
        // Y는 SURF_Y 기준 (바닥이 책상 위에 올라앉음)
        float bowlY = SURF_Y;

        // 그릇을 안쪽(왼쪽)으로 당겨 물체-그릇 사이 빈 공간을 줄임 → 화면을 더 채워 크게 보이게.
        // (이전 x: 1.4 / 2.5 → 변경 x: 0.9 / 1.9. 물체 우측 끝 x=-0.6과 1.5m 간격 유지)
        SpawnBowl("RedBowl",    "RedBowl",
            new Vector3(0.9f, bowlY, 0.0f),
            new Color(0.85f, 0.10f, 0.10f));   // 선명한 빨강

        SpawnBowl("OrangeBowl", "OrangeBowl",
            new Vector3(1.9f, bowlY, 0.0f),
            new Color(0.95f, 0.45f, 0.08f));   // 주황 (빨강과 혼동!)

        SpawnBowl("BlueBowl",   "BlueBowl",
            new Vector3(0.9f, bowlY, 1.1f),
            new Color(0.12f, 0.35f, 0.88f));   // 파랑

        SpawnBowl("NavyBowl",   "NavyBowl",
            new Vector3(1.9f, bowlY, 1.1f),
            new Color(0.08f, 0.10f, 0.45f));   // 남색 (파랑과 혼동!)
    }

    private void SpawnBowl(string name, string bowlTag, Vector3 pos, Color col)
    {
        // ── 그릇 치수 ──────────────────────────────────────────────────────────
        const float INNER   = 0.56f;  // 내부 가로·깊이
        const float WALL_T  = 0.06f;  // 벽 두께
        // WALL_H를 0.08f로 낮춤 — 그리퍼(Y≈0~0.21)보다 낮아 시각적 중첩 최소화
        // 물체는 OnPlaced()로 즉시 kinematic 고정되므로 낮은 벽도 충분함
        const float WALL_H  = 0.08f;
        const float FLOOR_H = 0.05f;  // 바닥 두께
        // Trigger 높이는 벽보다 훨씬 크게 — 위에서 떨어지는 물체도 감지
        const float TRIG_H  = 0.40f;
        float outer = INNER + WALL_T * 2;  // 외부 폭

        // ── 루트 (빈 GO — 이동/회전 기준) ─────────────────────────────────────
        var root = new GameObject(name);
        root.transform.position = new Vector3(pos.x, pos.y, pos.z);
        root.layer = 0;

        // ── 바닥 ──────────────────────────────────────────────────────────────
        BowlPart(root, "Floor",
            lp: new Vector3(0f, FLOOR_H * 0.5f, 0f),
            sc: new Vector3(outer, FLOOR_H, outer), col);

        // ── 4개 벽 (낮고 얇은 림 형태) ────────────────────────────────────────
        float wallY   = FLOOR_H + WALL_H * 0.5f;
        float wallOff = (INNER + WALL_T) * 0.5f;  // 벽 중심까지 거리

        // 앞 (Z+)
        BowlPart(root, "WallF",
            lp: new Vector3(0f,    wallY,  wallOff),
            sc: new Vector3(outer, WALL_H, WALL_T), col);
        // 뒤 (Z-)
        BowlPart(root, "WallB",
            lp: new Vector3(0f,    wallY, -wallOff),
            sc: new Vector3(outer, WALL_H, WALL_T), col);
        // 왼쪽 (X-)
        BowlPart(root, "WallL",
            lp: new Vector3(-wallOff, wallY, 0f),
            sc: new Vector3(WALL_T, WALL_H, INNER), col);
        // 오른쪽 (X+)
        BowlPart(root, "WallR",
            lp: new Vector3( wallOff, wallY, 0f),
            sc: new Vector3(WALL_T, WALL_H, INNER), col);

        // ── 감지 Trigger — 벽보다 훨씬 높게 설정해 위에서 떨어지는 물체도 포착 ──
        var trigger = new GameObject(name + "_Trigger");
        trigger.transform.SetParent(root.transform, false);
        // 트리거 중심: 바닥 위 TRIG_H/2 지점 (벽 위로 크게 돌출)
        trigger.transform.localPosition = new Vector3(0f, FLOOR_H + TRIG_H * 0.5f, 0f);

        var box = trigger.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(INNER - 0.02f, TRIG_H, INNER - 0.02f);

        var recv = trigger.AddComponent<BowlReceiver>();
        recv.Initialize(bowlTag);
    }

    /// <summary>그릇 구성 파트(Cube) 하나 생성 — 콜라이더 유지(물체가 벽에 걸림)</summary>
    private void BowlPart(GameObject root, string partName, Vector3 lp, Vector3 sc, Color col)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = partName;
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = lp;
        go.transform.localScale    = sc;
        go.layer = 0;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
            rend.material = new Material(rend.sharedMaterial) { color = col };
        // 콜라이더는 끄지 않음 — 물체가 벽/바닥에 충돌해 그릇 안에 머물러야 함
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 팔 레일 (시각적 가이드)

    private void SpawnRail()
    {
        // 팔이 XZ 평면에서 이동 → 갠트리 크레인 형태 레일
        // X축 레일 (좌우) × 2줄 (앞/뒤)
        foreach (float rz in new[] { -0.5f, 2.0f })
        {
            Primitive(PrimitiveType.Cube, "RailX",
                new Vector3(0f, 2.15f, rz),
                new Vector3(6.2f, 0.07f, 0.07f),
                new Color(0.22f, 0.22f, 0.28f));
        }
        // Z축 레일 (앞뒤) × 2줄 (좌/우)
        foreach (float rx in new[] { -3.0f, 3.0f })
        {
            Primitive(PrimitiveType.Cube, "RailZ",
                new Vector3(rx, 2.15f, 0.75f),
                new Vector3(0.07f, 0.07f, 3.2f),
                new Color(0.22f, 0.22f, 0.28f));
        }
        // 슬라이더 — 팔 루트에서 레일까지 연결하는 수직봉 (시각용, 이동 안 함)
        Primitive(PrimitiveType.Cube, "ArmSlider",
            new Vector3(0f, 2.08f, 0.3f),
            new Vector3(0.12f, 0.30f, 0.12f),
            new Color(0.32f, 0.32f, 0.38f));
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 카메라 & 조명

    private void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        // 3/4 하향 시점 — 팔·책상·오브젝트가 모두 보임
        // 1.3배(0,4.23,-3.99)로 당겼더니 좌우 끝(왼쪽 물체·오른쪽 그릇)이 잘렸음.
        // 그릇을 안쪽으로 모아 플레이 영역을 좁힌 뒤, 카메라를 살짝 왼쪽(x=-0.4)으로 옮겨
        // 좌우로 치우친 구도를 중앙 정렬하고 약 1.2배만 당김 → 전부 화면에 들어오면서 크게 보임.
        //   더 크게: z를 -3.99 쪽으로(당김) / 더 넓게: z를 -5.5 쪽으로(빼기). x는 좌우 중심.
        cam.transform.position = new Vector3(-0.4f, 4.6f, -4.4f);
        cam.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
        cam.fieldOfView = 55f;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
    }

    private void SetupLighting()
    {
        // 기존 Directional Light 찾거나 새로 생성
        var existingLight = FindObjectOfType<Light>();
        Light dirLight = existingLight;

        if (dirLight == null)
        {
            var lGo = new GameObject("DirectionalLight");
            dirLight = lGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
        }

        // 따뜻한 색온도 → 빨강/주황 그릇 색 착각 유발 (교육 목적)
        dirLight.color     = new Color(1.00f, 0.92f, 0.75f);
        dirLight.intensity = 1.15f;
        dirLight.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        // 약한 보조광 (그림자 채우기)
        var fillGo = new GameObject("FillLight");
        var fill   = fillGo.AddComponent<Light>();
        fill.type      = LightType.Directional;
        fill.color     = new Color(0.55f, 0.65f, 0.80f);
        fill.intensity = 0.35f;
        fillGo.transform.rotation = Quaternion.Euler(30f, 140f, 0f);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region 헬퍼

    /// <summary>Primitive를 생성하고 색상·콜라이더를 설정한다.</summary>
    private GameObject Primitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color col)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position   = pos;
        go.transform.localScale = scale;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(rend.sharedMaterial) { color = col };
            rend.material = mat;
        }

        return go;
    }

    #endregion
}
