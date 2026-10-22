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
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            if (playerStats != null)
                playerStats.TakeDamage(10);
            else
                Debug.LogError("playerStats 연결 안됨");
        }

        // [2026-10-21] 5번: 게임 초기화 (판 데이터·맵 진행·퍼즈 게이지 초기화 후 인트로 화면으로 — 3분 무입력 초기화와 같음)
        if (Input.GetKeyDown(KeyCode.Alpha5))
            RunManager.ReturnToIntro();
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