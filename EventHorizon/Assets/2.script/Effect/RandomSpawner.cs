using System.Collections;
using UnityEngine;

// [Faust 2026-11-07] 프리팹을 정한 영역 안에 랜덤 위치·각도·크기로 생성하는 스포너 (2026-11-08 좌우 반전 추가)
public class RandomSpawner : MonoBehaviour
{
    public enum AreaShape { Box, Circle }

    [Header("생성할 프리팹")]
    public GameObject prefab;

    [Header("영역 (이 오브젝트 위치 기준, Scene 뷰에 노란 선으로 표시)")]
    public AreaShape areaShape = AreaShape.Box;
    public Vector2 boxSize = new Vector2(3f, 2f);
    public float circleRadius = 1.5f;

    [Header("각도 (Z축, 도)")]
    public float minAngle = 0f;
    public float maxAngle = 360f;

    [Header("크기 (X·Y·Z 같은 비율)")]
    public float minScale = 0.7f;
    public float maxScale = 1.3f;

    [Header("개수와 간격")]
    public int countPerWave = 5;          // 한 번에 몇 개
    public float intervalInWave = 0.05f;  // 한 번 안에서 하나씩 나오는 간격 (0이면 동시에)
    public bool loop = true;              // 반복 여부
    public float waveInterval = 1f;       // 반복할 때 다음 묶음까지 대기
    public bool playOnStart = true;

    [Header("정리")]
    public float destroyAfter = 2f;       // 생성된 오브젝트 삭제까지 시간 (0이면 안 지움)
    public bool parentToSpawner = false;  // 스포너 자식으로 둘지

    [Header("좌우 반전")]
    public bool followFlip = true;        // [Faust 2026-11-08] 스포너가 좌우로 뒤집혀 있으면(월드 X 스케일 음수) 생성물도 거울처럼 뒤집음

    Coroutine running;

    void Start()
    {
        if (playOnStart) Play();
    }

    public void Play()
    {
        Stop();
        running = StartCoroutine(Run());
    }

    public void Stop()
    {
        if (running != null) StopCoroutine(running);
        running = null;
    }

    IEnumerator Run()
    {
        do
        {
            for (int i = 0; i < countPerWave; i++)
            {
                SpawnOne();
                if (intervalInWave > 0f) yield return new WaitForSeconds(intervalInWave);
            }
            if (loop) yield return new WaitForSeconds(waveInterval);
        } while (loop);
        running = null;
    }

    [ContextMenu("지금 한 묶음 생성")]
    public void SpawnWaveNow()
    {
        for (int i = 0; i < countPerWave; i++) SpawnOne();
    }

    public GameObject SpawnOne()
    {
        if (prefab == null) return null;

        Vector2 local = areaShape == AreaShape.Box
            ? new Vector2(Random.Range(-0.5f, 0.5f) * boxSize.x, Random.Range(-0.5f, 0.5f) * boxSize.y)
            : Random.insideUnitCircle * circleRadius;
        Vector3 pos = transform.TransformPoint(new Vector3(local.x, local.y, 0f));
        // 화면 기준 Z축 랜덤 회전 (프리팹 루트 원래 회전은 안쪽 오브젝트에 그대로 유지)
        bool mirror = followFlip && !parentToSpawner && transform.lossyScale.x < 0f;   // 자식으로 둘 때는 부모 반전을 그대로 물려받음
        float ang = Random.Range(minAngle, maxAngle);
        if (mirror) ang = -ang;   // 거울 반전이면 각도도 반대로 (예: 20도 → -20도)
        Quaternion rot = transform.rotation * Quaternion.Euler(0f, 0f, ang);

        // [Faust 2026-11-08] 빈 부모(holder)에 크기를 주고 그 안에 생성.
        // 프리팹 애니메이션이 루트 스케일을 키로 잡고 있어도(예: sword_hit) 랜덤 크기가 덮어써지지 않게 함.
        float s = Random.Range(minScale, maxScale);
        var holder = new GameObject(prefab.name + "_Spawned");
        if (parentToSpawner) holder.transform.SetParent(transform, true);
        holder.transform.SetPositionAndRotation(pos, rot);
        holder.transform.localScale = new Vector3(mirror ? -s : s, s, s);
        GameObject go = Instantiate(prefab, holder.transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = prefab.transform.localRotation;
        go.transform.localScale = prefab.transform.localScale;

        if (destroyAfter > 0f) Destroy(holder, destroyAfter);
        return go;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        if (areaShape == AreaShape.Box)
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(boxSize.x, boxSize.y, 0f));
        else
        {
            const int seg = 48;
            Vector3 prev = new Vector3(circleRadius, 0f, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 p = new Vector3(Mathf.Cos(a) * circleRadius, Mathf.Sin(a) * circleRadius, 0f);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
