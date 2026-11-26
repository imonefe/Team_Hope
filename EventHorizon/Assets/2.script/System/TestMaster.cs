using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class TestMaster : MonoBehaviour
{
    public MonsterSpawner[] spawners;
    public Stats playerStats;

    public void RefreshSpawners()
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag("sp");
        List<MonsterSpawner> list = new List<MonsterSpawner>();

        foreach (var obj in objs)
        {
            MonsterSpawner spawner = obj.GetComponent<MonsterSpawner>();
            if (spawner != null)
                list.Add(spawner);
            else
                Debug.LogWarning(obj.name + "에 MonsterSpawner 없음");
        }

        spawners = list.ToArray();
        Debug.Log("Spawner 갱신됨: " + spawners.Length);
    }

    void Update()
    {
        // [2026-11-24] Ctrl 을 누른 채로만 작동 — 숫자 1·2·3 은 증강 고르기 키와 겹치고,
        // 전시장에서 관람객이 숫자를 눌러 게임이 초기화(5)되거나 적이 지워지는(3) 일을 막음
        if (!(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) return;

        // 1번: 몬스터 스폰
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            foreach (MonsterSpawner spawner in spawners)
            {
                if (spawner != null)
                    spawner.SpawnMonster();
            }
        }

        // 2번: 플레이어 전체 회복
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (playerStats != null)
                playerStats.Heal(playerStats.maxHp);
            else
                Debug.LogError("playerStats 연결 안됨");

            PauseManager.Instance?.AddGauge(100f);  // 퍼즈 게이지 풀차징
        }

        // 3번: 몬스터 전체 삭제
        if (Input.GetKeyDown(KeyCode.Alpha3))
            DestroyAllEnemies();

        // 4번: 플레이어에게 체력 10 피해
        // [2026-11-26] 체력 숫자만 빼던 것 → 실제로 맞는 경로(Health.TakeDamage)로 보냄.
        //  예전엔 증강의 피격 반응(과부하 받는 피해 감소 · 나노 봉합 · 비상 이식 · 과부하로 잃은 체력 기록)이
        //  전혀 안 돌고, 0이 돼도 죽지 않았음. 넉백 없음 · 피격 무적 무시(연타해도 매번 들어감)
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            if (playerStats != null)
            {
                Health hp = playerStats.GetComponent<Health>();
                if (hp != null) hp.TakeDamage(10, 0, 0f, 0f, true);
                else playerStats.TakeDamage(10);
            }
            else
                Debug.LogError("playerStats 연결 안됨");
        }

        // [2026-10-21] 5번: 게임 초기화 (판 데이터·맵 진행·퍼즈 게이지 초기화 후 인트로 화면으로 — 3분 무입력 초기화와 같음)
        if (Input.GetKeyDown(KeyCode.Alpha5))
            RunManager.ReturnToIntro();

        // [2026-11-25] 6번: 재화 · 증강 파편 각각 9000개 (상점 · 강화 시험용)
        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            if (RunManager.Instance != null)
            {
                RunManager.Instance.AddCurrency(9000);
                RunManager.Instance.AddShards(9000);
                ScreenNotice.Show("테스트: 재화 · 사이버웨어 파편 각각 9000개");
            }
            else Debug.LogError("RunManager 없음");
        }
    }

    void DestroyAllEnemies()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        foreach (GameObject obj in FindObjectsOfType<GameObject>())
        {
            if (obj.layer == enemyLayer)
                Destroy(obj);
        }
    }
}