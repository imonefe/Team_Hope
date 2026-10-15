using System.Collections;
using UnityEngine;

// 스킬 테스트용 임시 스크립트 — 지정한 Skill 프리팹을 키 입력으로 즉시 발동시켜본다.
// 정식 스킬 슬롯(Q/W/E, CharacterSkills)과는 별개의 경로이고, 애니메이션 이벤트 없이
// Launch() 직후 spawnEntries를 spawnInterval 간격으로 하나씩 순서대로 발사한다.
// 테스트 끝나면 플레이어 오브젝트에서 이 컴포넌트만 떼어내면 됨 — 스킬 시스템 자체엔 영향 없음.
public class SkillTestKey : MonoBehaviour
{
    [Tooltip("테스트로 발동할 스킬 프리팹")]
    public Skill skillPrefab;

    [Tooltip("시전 기준 Transform (비우면 이 오브젝트 자신 — 보통 플레이어에 붙이면 됨)")]
    public Transform caster;

    [Tooltip("발동 테스트용 키")]
    public KeyCode testKey = KeyCode.J;

    [Tooltip("spawnEntries를 한꺼번에(SpawnAll) 안 쓰고, 순서대로 하나씩 이 간격(초)만큼 텀을 두고 발사")]
    public float spawnInterval = 0.3f;

    void Awake()
    {
        if (caster == null) caster = transform;
    }

    void Update()
    {
        if (Input.GetKeyDown(testKey))
            Fire();
    }

    void Fire()
    {
        if (skillPrefab == null)
        {
            Debug.LogWarning("[SkillTestKey] skillPrefab이 비어있어요. 인스펙터에서 연결해주세요.");
            return;
        }

        // CharacterSkills.GetFacing()과 동일한 방식 — localScale.x 부호로 방향 판단
        int facing = caster.localScale.x < 0 ? -1 : 1;

        Skill instance = Instantiate(skillPrefab);
        instance.Launch(caster, facing);
        StartCoroutine(FireSequence(instance));
    }

    // spawnEntries를 애니메이션 이벤트 없이, 정해진 간격을 두고 하나씩 순서대로 발사
    // 매번 같은 순서(상->중->하)로만 나가면 패턴이 뻔해 보이므로, 발사 순서 자체를 매번 랜덤하게 섞는다.
    IEnumerator FireSequence(Skill instance)
    {
        if (instance == null || instance.spawnEntries == null) yield break;

        int count = instance.spawnEntries.Length;
        int[] order = new int[count];
        for (int i = 0; i < count; i++) order[i] = i;

        // Fisher-Yates 셔플
        for (int i = count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = order[i];
            order[i] = order[j];
            order[j] = temp;
        }

        for (int idx = 0; idx < count; idx++)
        {
            if (instance == null) yield break; // 스킬 본체가 lifetime으로 먼저 사라졌으면 중단
            instance.SpawnGroup(order[idx]);
            yield return new WaitForSeconds(spawnInterval);
        }
    }
}
