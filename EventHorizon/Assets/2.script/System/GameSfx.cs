using UnityEngine;

/// <summary>
/// [2026-11-25 추가] 게임 공용 효과음 모음 (프로젝트 전체 1개).
/// 반드시 Resources 폴더 안에 "GameSfx" 이름으로 둔다 (현재: Assets/Etc/Resources/GameSfx.asset).
/// 소리 · 볼륨은 그 에셋에서 바꾼다. 모두 화면 기준(2D) 재생 + 설정 창 효과음 크기 적용.
///  - 플레이어 피격 (Health — 체력이 실제로 깎였을 때만)
///  - 몬스터 스폰 (MonsterSpawner) · 몬스터 파괴 (Health.Die, 일반 적)
///  - 사이버웨어 선택 (AugmentOfferWindow.Choose)
///  - 창 열림 (상점 · 설정 · 크레딧 · 메뉴 등. 대화창 · 사이버웨어 보상 창 같은 게임 진행 창은 제외)
/// 같은 소리가 한 순간에 여러 번(적 여러 마리 동시 스폰 등) 겹치면 한 번만 낸다.
/// </summary>
[CreateAssetMenu(fileName = "GameSfx", menuName = "Game/Game Sfx")]
public class GameSfx : ScriptableObject
{
    public AudioClip playerHurt;
    [Range(0f, 1f)] public float playerHurtVolume = 0.7f;
    public AudioClip enemySpawn;
    [Range(0f, 1f)] public float enemySpawnVolume = 0.45f;
    public AudioClip enemyDeath;
    [Range(0f, 1f)] public float enemyDeathVolume = 0.55f;
    public AudioClip augmentSelect;
    [Range(0f, 1f)] public float augmentSelectVolume = 0.7f;
    [Tooltip("상점 · 설정 · 크레딧 등 창이 열릴 때")]
    public AudioClip windowOpen;
    [Range(0f, 1f)] public float windowOpenVolume = 0.6f;
    [Tooltip("같은 소리가 이 시간(초) 안에 또 불리면 생략")]
    public float sameSoundGap = 0.08f;

    static GameSfx inst;
    static bool loaded;
    static GameSfx I
    {
        get
        {
            if (!loaded) { inst = Resources.Load<GameSfx>("GameSfx"); loaded = true; }
            return inst;
        }
    }

    static float tHurt = -10f, tSpawn = -10f, tDeath = -10f, tSelect = -10f, tWindow = -10f;

    static void Play(AudioClip clip, float volume, ref float last, SfxTier tier)
    {
        var s = I;
        if (s == null || clip == null) return;
        if (Time.unscaledTime - last < s.sameSoundGap) return;
        last = Time.unscaledTime;
        SkillSfx.Play(clip, volume, 0.04f, tier);   // [2026-11-25] 등급 (소리 우선순위)
    }

    public static void PlayerHurt() { var s = I; if (s != null) Play(s.playerHurt, s.playerHurtVolume, ref tHurt, SfxTier.Critical); }
    public static void EnemySpawn() { var s = I; if (s != null) Play(s.enemySpawn, s.enemySpawnVolume, ref tSpawn, SfxTier.Ambient); }
    public static void EnemyDeath() { var s = I; if (s != null) Play(s.enemyDeath, s.enemyDeathVolume, ref tDeath, SfxTier.Ambient); }
    public static void AugmentSelect() { var s = I; if (s != null) Play(s.augmentSelect, s.augmentSelectVolume, ref tSelect, SfxTier.Critical); }
    public static void WindowOpen() { var s = I; if (s != null) Play(s.windowOpen, s.windowOpenVolume, ref tWindow, SfxTier.Action); }
}
