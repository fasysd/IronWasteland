using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Vùng địa hình chuẩn vật lý 2D dành cho Phụ kiện (vệt lửa xanh, màn khói độc...).
    /// Sử dụng CircleCollider2D (isTrigger = true) và ColliderOwnerRef để phát hiện xe Tank hoặc Kẻ địch.
    /// Tuyệt đối không dùng OverlapCircleNonAlloc hay GetComponentInParent.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public class AccessoryAreaZone : MonoBehaviour
    {
        public enum ZoneEffectType
        {
            SoulFire,   // Động cơ Tua-bin Linh Hỏa (Phụ kiện 6): +20% MoveSpeed đồng minh, -20% MoveSpeed địch
            ToxicSpore  // Lõi Ma Pháp Bào Tử Độc (Phụ kiện 12): -20% Phòng thủ kẻ địch
        }

        [Header("Zone Configuration")]
        [SerializeField] private ZoneEffectType zoneType;
        [SerializeField] private float duration = 3.5f;
        [SerializeField] private float radius = 1.2f;

        private float m_Timer;
        private float m_SlowRefreshTimer;
        private CircleCollider2D m_Collider;
        private readonly HashSet<TankBrain> m_TanksInside = new HashSet<TankBrain>();
        private readonly HashSet<EnemyBrain> m_EnemiesInside = new HashSet<EnemyBrain>();
        private static readonly Dictionary<TankBrain, int> s_SoulFireCounts = new Dictionary<TankBrain, int>();

        public static AccessoryAreaZone Spawn(Vector2 position, ZoneEffectType type, float zoneRadius, float zoneDuration, Color zoneColor)
        {
            if (TankBrain.NetworkSessionActive)
            {
                var manager = Unity.Netcode.NetworkManager.Singleton;
                if (manager == null || !manager.IsServer) return null;

                IronWasteland.Net.NetVisual.SpawnPrimitive(
                    position, Vector2.zero, zoneRadius, zoneDuration, fade: true, sortingOrder: -1, color: zoneColor);
            }

            GameObject go = new GameObject($"AccessoryZone_{type}");
            go.transform.position = position;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            // localScale = zoneRadius * 2 => radius 0.5 trong local space cho đúng bán kính world = zoneRadius
            col.radius = 0.5f;

            var zone = go.AddComponent<AccessoryAreaZone>();
            zone.zoneType = type;
            zone.radius = zoneRadius;
            zone.duration = zoneDuration;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.SoftCircle;
            sr.color = zoneColor;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = -1;
            go.transform.localScale = Vector3.one * (zoneRadius * 2f);

            return zone;
        }

        private void Awake()
        {
            m_Collider = GetComponent<CircleCollider2D>();
            m_Collider.isTrigger = true;
        }

        private void Update()
        {
            m_Timer += Time.deltaTime;
            if (zoneType == ZoneEffectType.SoulFire)
            {
                m_SlowRefreshTimer += Time.deltaTime;
                if (m_SlowRefreshTimer >= 0.4f)
                {
                    m_SlowRefreshTimer = 0f;
                    foreach (var enemy in m_EnemiesInside)
                    {
                        if (enemy != null && enemy.IsAlive)
                        {
                            enemy.ApplyCrowdControl(CrowdControlType.Slow, 0.6f, 0.20f);
                        }
                    }
                }
            }

            if (m_Timer >= duration)
            {
                CleanupZone();
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            ColliderOwnerRef ownerRef = ColliderOwnerRef.FromCollider(other);
            if (ownerRef == null) return;

            TankBrain tank = ownerRef.GetOwner<TankBrain>();
            if (tank != null && tank.IsAlive)
            {
                if (m_TanksInside.Add(tank))
                {
                    ApplyTankEffect(tank);
                }
                return;
            }

            EnemyBrain enemy = ownerRef.GetOwner<EnemyBrain>();
            if (enemy != null && enemy.IsAlive)
            {
                if (m_EnemiesInside.Add(enemy))
                {
                    ApplyEnemyEffect(enemy);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            ColliderOwnerRef ownerRef = ColliderOwnerRef.FromCollider(other);
            if (ownerRef == null) return;

            TankBrain tank = ownerRef.GetOwner<TankBrain>();
            if (tank != null && m_TanksInside.Remove(tank))
            {
                RemoveTankEffect(tank);
                return;
            }

            EnemyBrain enemy = ownerRef.GetOwner<EnemyBrain>();
            if (enemy != null && m_EnemiesInside.Remove(enemy))
            {
                RemoveEnemyEffect(enemy);
            }
        }

        private void ApplyTankEffect(TankBrain tank)
        {
            if (zoneType == ZoneEffectType.SoulFire)
            {
                s_SoulFireCounts.TryGetValue(tank, out int count);
                s_SoulFireCounts[tank] = count + 1;
                if (count == 0)
                {
                    tank.AddBonusStats("Acc_SoulFire_Speed", null, new TankStats { MoveSpeed = 0.20f });
                }
            }
        }

        private void RemoveTankEffect(TankBrain tank)
        {
            if (zoneType == ZoneEffectType.SoulFire)
            {
                if (s_SoulFireCounts.TryGetValue(tank, out int count))
                {
                    count--;
                    if (count <= 0)
                    {
                        s_SoulFireCounts.Remove(tank);
                        tank.RemoveBonusStats("Acc_SoulFire_Speed");
                    }
                    else
                    {
                        s_SoulFireCounts[tank] = count;
                    }
                }
            }
        }

        private void ApplyEnemyEffect(EnemyBrain enemy)
        {
            if (zoneType == ZoneEffectType.SoulFire)
            {
                enemy.ApplyCrowdControl(CrowdControlType.Slow, 1.0f, 0.20f);
            }
            else if (zoneType == ZoneEffectType.ToxicSpore)
            {
                if (enemy.Stats != null)
                {
                    enemy.Stats.Defense = Mathf.Max(0f, enemy.Stats.Defense * 0.8f);
                }
            }
        }

        private void RemoveEnemyEffect(EnemyBrain enemy)
        {
            if (zoneType == ZoneEffectType.ToxicSpore)
            {
                if (enemy.Stats != null)
                {
                    enemy.Stats.Defense /= 0.8f;
                }
            }
        }

        private void CleanupZone()
        {
            foreach (var tank in m_TanksInside)
            {
                if (tank != null) RemoveTankEffect(tank);
            }
            foreach (var enemy in m_EnemiesInside)
            {
                if (enemy != null) RemoveEnemyEffect(enemy);
            }
            m_TanksInside.Clear();
            m_EnemiesInside.Clear();
        }

        private void OnDestroy()
        {
            CleanupZone();
        }
    }
}
