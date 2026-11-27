using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Vùng địa hình chuẩn vật lý 2D dành cho Phụ kiện (vệt lửa xanh, màn khói độc...).
    /// Sử dụng NetworkBehaviour + NetworkObject Prefab để đồng bộ trực tiếp cả Offline lẫn Co-op.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class AccessoryAreaZone : NetworkBehaviour
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

        private readonly NetworkVariable<float> m_NetRadius = new(
            1.2f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Color> m_NetColor = new(
            Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private float m_Timer;
        private float m_SlowRefreshTimer;
        private CircleCollider2D m_Collider;
        private SpriteRenderer m_Renderer;
        private Color m_BaseColor = Color.white;
        private readonly HashSet<TankBrain> m_TanksInside = new HashSet<TankBrain>();
        private readonly HashSet<EnemyBrain> m_EnemiesInside = new HashSet<EnemyBrain>();
        private static readonly Dictionary<TankBrain, int> s_SoulFireCounts = new Dictionary<TankBrain, int>();

        public static AccessoryAreaZone Spawn(
            Vector2 position,
            ZoneEffectType type,
            float zoneRadius,
            float zoneDuration,
            Color zoneColor,
            GameObject prefab = null)
        {
            bool isNetSession = TankBrain.NetworkSessionActive;
            NetworkManager manager = isNetSession ? NetworkManager.Singleton : null;
            if (isNetSession && (manager == null || !manager.IsServer))
            {
                return null;
            }

            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, position, Quaternion.identity);
            }
            else
            {
                go = new GameObject($"AccessoryZone_{type}");
                go.transform.position = position;
                go.AddComponent<CircleCollider2D>();
                go.AddComponent<SpriteRenderer>();
                go.AddComponent<AccessoryAreaZone>();
            }

            AccessoryAreaZone zone = go.GetComponent<AccessoryAreaZone>() ?? go.AddComponent<AccessoryAreaZone>();
            zone.Configure(type, zoneRadius, zoneDuration, zoneColor);

            if (isNetSession && go.TryGetComponent(out NetworkObject netObj) && !netObj.IsSpawned)
            {
                netObj.Spawn(true);
            }

            return zone;
        }

        private void Configure(ZoneEffectType type, float zoneRadius, float zoneDuration, Color zoneColor)
        {
            zoneType = type;
            radius = Mathf.Max(0.1f, zoneRadius);
            duration = Mathf.Max(0.1f, zoneDuration);
            m_BaseColor = zoneColor;
            m_Timer = 0f;

            ApplyVisualTransform(radius, zoneColor);

            if (IsSpawned && IsServer)
            {
                m_NetRadius.Value = radius;
                m_NetColor.Value = zoneColor;
            }
        }

        private void ApplyVisualTransform(float targetRadius, Color targetColor)
        {
            if (m_Collider == null) m_Collider = GetComponent<CircleCollider2D>();
            if (m_Collider != null)
            {
                m_Collider.isTrigger = true;
                m_Collider.radius = 0.5f;
            }

            if (m_Renderer == null) m_Renderer = GetComponent<SpriteRenderer>();
            if (m_Renderer != null)
            {
                if (m_Renderer.sprite == null)
                {
                    string spriteResource = zoneType == ZoneEffectType.SoulFire
                        ? "VFX/Accessories/vfx_accessory_soulfire"
                        : "VFX/Accessories/vfx_accessory_toxicspore";
                    Sprite customSprite = Resources.Load<Sprite>(spriteResource);
                    m_Renderer.sprite = customSprite != null ? customSprite : SpriteFactory.SoftCircle;
                }
                m_Renderer.color = targetColor;
                m_Renderer.sortingLayerName = "Default";
                m_Renderer.sortingOrder = -1;
            }

            transform.localScale = Vector3.one * (Mathf.Max(0.1f, targetRadius) * 2f);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                m_NetRadius.Value = radius;
                m_NetColor.Value = m_BaseColor;
            }
            else
            {
                m_BaseColor = m_NetColor.Value;
                ApplyVisualTransform(m_NetRadius.Value, m_BaseColor);
            }
        }

        private void Awake()
        {
            m_Collider = GetComponent<CircleCollider2D>();
            if (m_Collider != null) m_Collider.isTrigger = true;
            m_Renderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            m_Timer += Time.deltaTime;

            if (IsSpawned && !IsServer)
            {
                if (m_Renderer != null && duration > 0f)
                {
                    float alpha = Mathf.Lerp(m_BaseColor.a, 0f, Mathf.Clamp01(m_Timer / duration));
                    m_Renderer.color = new Color(m_BaseColor.r, m_BaseColor.g, m_BaseColor.b, alpha);
                }
                return;
            }

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
                if (IsSpawned && IsServer && NetworkObject != null)
                {
                    NetworkObject.Despawn(true);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsSpawned && !IsServer) return;

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
            if (IsSpawned && !IsServer) return;
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
