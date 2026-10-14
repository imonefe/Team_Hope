using UnityEngine;

/// <summary>
/// 방 밖에 있어도 어두운 방(RoomLighting)에서 꺼지지 않게 할 빛에 붙이는 표시용 컴포넌트.
/// 예: 플레이어에 붙은 빛, 씬 전체에 늘 켜 둘 연출용 빛.
/// </summary>
[RequireComponent(typeof(Light))]
public class KeepLightInDarkRoom : MonoBehaviour { }
