using UnityEngine;

public class Weapon : MonoBehaviour
{
    public WeaponData data;

    public void ApplyTo(Stats stats, PlayerController player)
    {
        // 이전 무기 스탯 제거
        if (player.EquippedWeapon != null)
            player.EquippedWeapon.RemoveStats(stats);

        // 새 무기 스탯 적용
        data.ApplyStats(stats);

        // 무기 전환 (애니메이션·히트박스·이펙트·상성 타입 전체 교체, SwitchWeapon 내부에서 처리)
        player.SwitchWeapon(data);
    }
}