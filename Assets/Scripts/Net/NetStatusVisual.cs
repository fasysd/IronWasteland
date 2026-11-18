using System;
using System.Collections.Generic;
using IronWasteland.Core.Effects;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Vẽ hào quang khiên và buff quanh xe trên client. Dữ liệu lấy từ host qua
    /// <see cref="PlayerNetState.ShieldAmount"/> và <see cref="NetEffectSync"/>; host/offline đọc trực tiếp từ
    /// <see cref="TankBrain.CurrentShield"/> / <see cref="TankBrain.ActiveEffects"/>.
    /// Hai hào quang là object con dựng sẵn trên prefab (component này chỉ bật/tắt, đổi màu và nhịp sáng).
    /// </summary>
    public class NetStatusVisual : MonoBehaviour
    {
        [Header("Hào quang khiên")]
        [SerializeField, Tooltip("SpriteRenderer của hào quang khiên (object con dựng sẵn trên prefab).")]
        private SpriteRenderer shieldAura;

        [SerializeField, Tooltip("Màu hào quang khiên.")]
        private Color shieldColor = new Color(0.55f, 0.85f, 1f, 0.55f);

        [SerializeField, Min(0.1f), Tooltip("Tốc độ nhịp sáng của hào quang khiên (radian/giây).")]
        private float shieldPulseSpeed = 2.5f;

        [SerializeField, Min(0f), Tooltip("Alpha nhỏ nhất của hào quang khiên.")]
        private float shieldMinAlpha = 0.28f;

        [SerializeField, Min(0f), Tooltip("Alpha lớn nhất của hào quang khiên.")]
        private float shieldMaxAlpha = 0.62f;

        [Header("Hào quang khiên nước (Leviathan)")]
        [SerializeField, Tooltip("Sprite hào quang nước dùng cho khiên của Leviathan (Tide Commander / Mobile Altar).")]
        private Sprite waterShieldSprite;

        [SerializeField, Tooltip("Màu hào quang nước.")]
        private Color waterShieldColor = new Color(0.55f, 0.85f, 1f, 0.5f);

        [SerializeField, Min(0.1f), Tooltip("Đường kính hào quang nước (đơn vị world) — to hơn thân xe để dễ nhận ra.")]
        private float waterShieldDiameter = 5.8f;

        [SerializeField, Min(0f), Tooltip("Alpha lớn nhất của hào quang nước (mờ để vẫn thấy xe bên dưới).")]
        private float waterMaxAlpha = 0.38f;

        [Header("Hào quang buff")]
        [SerializeField, Tooltip("SpriteRenderer của hào quang buff (object con dựng sẵn trên prefab).")]
        private SpriteRenderer buffAura;

        [SerializeField, Min(0.1f), Tooltip("Tốc độ nhịp sáng của hào quang buff (radian/giây).")]
        private float buffPulseSpeed = 4f;

        [SerializeField, Min(0f), Tooltip("Alpha nhỏ nhất của hào quang buff.")]
        private float buffMinAlpha = 0.18f;

        [SerializeField, Min(0f), Tooltip("Alpha lớn nhất của hào quang buff.")]
        private float buffMaxAlpha = 0.45f;

        private enum ShieldAuraKind
        {
            None,
            Default,
            Water,
            External
        }

        private TankBrain m_Brain;
        private PlayerNetState m_NetState;
        private float m_ShieldPulse;
        private float m_BuffPulse;
        private Sprite m_DefaultShieldSprite;
        private Vector3 m_DefaultShieldScale;

        private void Awake()
        {
            m_Brain = GetComponent<TankBrain>();
            m_NetState = GetComponent<PlayerNetState>();

            if (shieldAura != null)
            {
                m_DefaultShieldSprite = shieldAura.sprite;
                m_DefaultShieldScale = shieldAura.transform.localScale;
                shieldAura.enabled = false;
            }

            if (buffAura != null) buffAura.enabled = false;
        }

        private void Update()
        {
            UpdateShieldAura();
            UpdateBuffAura();
        }

        private void UpdateShieldAura()
        {
            if (shieldAura == null) return;

            float amount = ResolveShieldAmount();
            ShieldAuraKind kind = amount <= 0.01f ? ShieldAuraKind.None : ResolveShieldKind();
            if (kind == ShieldAuraKind.None || kind == ShieldAuraKind.External)
            {
                shieldAura.enabled = false;
                m_ShieldPulse = 0f;
                return;
            }

            bool water = kind == ShieldAuraKind.Water && waterShieldSprite != null;
            Sprite sprite = water ? waterShieldSprite : m_DefaultShieldSprite;
            if (shieldAura.sprite != sprite) shieldAura.sprite = sprite;

            if (water)
            {
                // Hào quang nước do Leviathan ban: cùng một sprite ở cả host và client, phóng to theo
                // kích thước sprite để phủ rộng hơn thân xe nhưng giữ alpha thấp cho thấy xe bên dưới.
                float extent = Mathf.Max(0.01f, sprite.bounds.size.x);
                float scale = waterShieldDiameter / extent;
                shieldAura.transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                shieldAura.transform.localScale = m_DefaultShieldScale;
            }

            m_ShieldPulse += Time.deltaTime * shieldPulseSpeed;
            float pulse = (Mathf.Sin(m_ShieldPulse) + 1f) * 0.5f;

            Color color = water ? waterShieldColor : shieldColor;
            float maxAlpha = water ? Mathf.Min(waterMaxAlpha, shieldMaxAlpha) : shieldMaxAlpha;
            color.a = Mathf.Lerp(shieldMinAlpha, maxAlpha, pulse);
            shieldAura.color = color;
            shieldAura.enabled = true;
        }

        private void UpdateBuffAura()
        {
            if (buffAura == null) return;

            if (!TryGetBuffColor(out Color buffColor))
            {
                buffAura.enabled = false;
                m_BuffPulse = 0f;
                return;
            }

            m_BuffPulse += Time.deltaTime * buffPulseSpeed;
            float pulse = (Mathf.Sin(m_BuffPulse) + 1f) * 0.5f;

            buffColor.a = Mathf.Lerp(buffMinAlpha, buffMaxAlpha, pulse);
            buffAura.color = buffColor;
            buffAura.enabled = true;
        }

        private float ResolveShieldAmount()
        {
            // Client: giá trị host đồng bộ qua NetworkVariable. Host/offline: đọc trực tiếp từ brain.
            if (m_NetState != null && m_NetState.IsSpawned) return m_NetState.ShieldAmount;
            return m_Brain != null ? m_Brain.CurrentShield : 0f;
        }

        /// <summary>
        /// Loại hào quang khiên theo nguồn ban, suy ra từ id hiệu ứng mà cả host và client đều có
        /// (host đọc <see cref="TankBrain.ActiveEffects"/>, client đọc <see cref="NetEffectSync"/>) —
        /// nhờ vậy hai bên luôn vẽ cùng một kiểu, không phụ thuộc sprite riêng của từng prefab.
        /// </summary>
        private ShieldAuraKind ResolveShieldKind()
        {
            if (m_NetState != null && m_NetState.IsSpawned && !m_NetState.IsServer)
            {
                NetEffectSync sync = NetEffectSync.Instance;
                if (sync == null) return ShieldAuraKind.Default;
                return ClassifyEffects(sync.GetEffects(m_NetState.OwnerClientId), null);
            }

            return ClassifyEffects(null, m_Brain != null ? m_Brain.ActiveEffects : null);
        }

        private static ShieldAuraKind ClassifyEffects(
            IReadOnlyCollection<NetEffectInfo> networkEffects,
            IReadOnlyList<StatusEffect> activeEffects)
        {
            ShieldAuraKind result = ShieldAuraKind.None;

            if (networkEffects != null)
            {
                foreach (NetEffectInfo effect in networkEffects)
                {
                    ShieldAuraKind kind = ClassifyShieldId(effect.EffectId);
                    if (kind == ShieldAuraKind.External) return ShieldAuraKind.External;
                    if (kind == ShieldAuraKind.Water) result = ShieldAuraKind.Water;
                    else if (kind == ShieldAuraKind.Default && result == ShieldAuraKind.None) result = ShieldAuraKind.Default;
                }

                return result;
            }

            if (activeEffects != null)
            {
                for (int i = 0; i < activeEffects.Count; i++)
                {
                    if (!(activeEffects[i] is ShieldEffect shield)) continue;

                    ShieldAuraKind kind = ClassifyShieldId(shield.EffectId);
                    if (kind == ShieldAuraKind.External) return ShieldAuraKind.External;
                    if (kind == ShieldAuraKind.Water) result = ShieldAuraKind.Water;
                    else if (kind == ShieldAuraKind.Default && result == ShieldAuraKind.None) result = ShieldAuraKind.Default;
                }
            }

            return result;
        }

        private static ShieldAuraKind ClassifyShieldId(string effectId)
        {
            if (string.IsNullOrEmpty(effectId)) return ShieldAuraKind.None;

            // Bubble Shield tự dựng object bong bóng riêng nên không vẽ thêm hào quang chung.
            if (effectId.IndexOf("Bubble", StringComparison.OrdinalIgnoreCase) >= 0) return ShieldAuraKind.External;

            // Khiên do Leviathan ban (Tide Commander / Mobile Altar) dùng hào quang nước.
            if (effectId.IndexOf("TideCommander", StringComparison.OrdinalIgnoreCase) >= 0
                || effectId.IndexOf("Altar", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ShieldAuraKind.Water;
            }

            return IsShieldEffect(effectId) ? ShieldAuraKind.Default : ShieldAuraKind.None;
        }

        private bool TryGetBuffColor(out Color color)
        {
            color = default;
            if (m_Brain == null) return false;

            if (m_NetState != null && m_NetState.IsSpawned && !m_NetState.IsServer)
            {
                NetEffectSync sync = NetEffectSync.Instance;
                if (sync == null) return false;

                IReadOnlyCollection<NetEffectInfo> effects = sync.GetEffects(m_NetState.OwnerClientId);
                if (effects == null) return false;

                foreach (NetEffectInfo effect in effects)
                {
                    if (string.IsNullOrEmpty(effect.EffectId)) continue;
                    if (IsShieldEffect(effect.EffectId)) continue;

                    color = ColorForEffect(effect.EffectId);
                    return true;
                }

                return false;
            }

            IReadOnlyList<StatusEffect> active = m_Brain.ActiveEffects;
            for (int i = 0; i < active.Count; i++)
            {
                StatusEffect effect = active[i];
                if (effect == null || effect.IsExpired) continue;
                if (effect is ShieldEffect) continue;
                if (IsShieldEffect(effect.EffectId)) continue;

                color = ColorForEffect(effect.EffectId);
                return true;
            }

            return false;
        }

        /// <summary>Hiệu ứng khiên đã có hào quang riêng nên bỏ khỏi hào quang buff (tránh hai vòng chồng nhau).</summary>
        private static bool IsShieldEffect(string effectId)
        {
            return effectId.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0
                || effectId.IndexOf("Guard", StringComparison.OrdinalIgnoreCase) >= 0
                || effectId.IndexOf("Fortress", StringComparison.OrdinalIgnoreCase) >= 0
                || effectId.IndexOf("Guardian", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Màu ổn định theo tên hiệu ứng để mỗi buff có một màu khác nhau.</summary>
        private static Color ColorForEffect(string effectId)
        {
            int hash = effectId.GetHashCode();
            float hue = Mathf.Abs(hash % 360) / 360f;
            return Color.HSVToRGB(hue, 0.6f, 1f);
        }
    }
}
