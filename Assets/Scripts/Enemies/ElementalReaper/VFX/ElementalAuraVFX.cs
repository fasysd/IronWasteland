using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.VFX
{
    /// <summary>
    /// Hào quang bao quanh thân Boss Tử Thần Nguyên Tố (VFX_ElementalAura):
    /// - Tự động đổi màu sắc và hiệu ứng hạt tương ứng 3 hệ:
    ///   + Thủy (Water): Xanh lam (#00BFFF), bọt nước và gợn sóng luân hồi.
    ///   + Độc (Poison): Xanh lục bảo pha tím (#32CD32 / #7B1FA2), khói độc bốc lên ngùn ngụt.
    ///   + Hỏa (Fire): Đỏ cam rực lửa (#FF4500), tàn tro than hồng và sóng nhiệt bốc hơi.
    /// </summary>
    [DisallowMultipleComponent]
    public class ElementalAuraVFX : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private SpriteRenderer auraGlowSprite;
        [SerializeField] private ParticleSystem auraParticles;
        [SerializeField] private ParticleSystem mistParticles;

        [Header("Palette — Thủy (Water)")]
        [SerializeField] private Color waterPrimaryColor = new Color(0f, 0.749f, 1f, 0.75f); // #00BFFF
        [SerializeField] private Color waterSecondaryColor = new Color(0.2f, 0.9f, 1f, 0.5f);

        [Header("Palette — Độc (Poison)")]
        [SerializeField] private Color poisonPrimaryColor = new Color(0.196f, 0.804f, 0.196f, 0.75f); // #32CD32
        [SerializeField] private Color poisonSecondaryColor = new Color(0.482f, 0.122f, 0.635f, 0.65f); // #7B1FA2

        [Header("Palette — Hỏa (Fire)")]
        [SerializeField] private Color firePrimaryColor = new Color(1f, 0.271f, 0f, 0.75f); // #FF4500
        [SerializeField] private Color fireSecondaryColor = new Color(1f, 0.6f, 0f, 0.65f);

        private ElementalType m_CurrentElement = ElementalType.Water;
        public ElementalType CurrentElement => m_CurrentElement;

        private void Awake()
        {
            ApplyElement(m_CurrentElement);
        }

        public void SetElement(ElementalType type)
        {
            m_CurrentElement = type;
            ApplyElement(type);
        }

        private void ApplyElement(ElementalType type)
        {
            Color primary, secondary;
            switch (type)
            {
                case ElementalType.Poison:
                    primary = poisonPrimaryColor;
                    secondary = poisonSecondaryColor;
                    break;
                case ElementalType.Fire:
                    primary = firePrimaryColor;
                    secondary = fireSecondaryColor;
                    break;
                case ElementalType.Water:
                default:
                    primary = waterPrimaryColor;
                    secondary = waterSecondaryColor;
                    break;
            }

            if (auraGlowSprite != null)
            {
                auraGlowSprite.color = primary;
            }

            if (auraParticles != null)
            {
                var main = auraParticles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(primary, secondary);
            }

            if (mistParticles != null)
            {
                var main = mistParticles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(secondary, primary * 0.7f);
            }
        }
    }
}
