using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace IronWasteland
{
    /// <summary>
    /// Phân loại giá trị hiển thị trên TextValueManager để chọn màu sắc tương ứng.
    /// </summary>
    public enum TextValueType : byte
    {
        EnemyDamage = 0,
        TankDamage = 1,
        Heal = 2,
        ShieldAbsorb = 3
    }

    /// <summary>
    /// Singleton hien thi TextValue (so sat thuong, hoi mau, khien chan) bang pooling.
    ///
    /// - Goi <see cref="Show(float, Vector3)"/>: truyen so sat thuong (float) va vi tri world.
    ///   So hien thi la float da lam tron sang int, chi chua so thuong (khong trang thai dac biet).
    /// - Text xuat hien tai vi tri ngau nhien trong ban kinh spawnRadius quan vi tri gay sat thuong,
    ///   sau do di chuyen ra ngoai (nguoc huong ve tam vi tri gay sat thuong) va tro ve pool khi het lifetime.
    /// - Gan het lifetime: alpha nhan dan va scale nhan dan theo 2 AnimationCurve RIENG
    ///   (alphaOverLifetime / scaleOverLifetime), dieu chinh duoc trong Inspector.
    ///
    /// Tu tao GameObject khi truy cap <see cref="Instance"/> neu chua co trong Scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class TextValueManager : MonoBehaviour
    {
        #region Inspector fields

        [Header("Pool")]
        [Tooltip("So TextDamage tao san luc khoi tao.")]
        [SerializeField, Min(0)] private int prewarmCount = 20;

        [Tooltip("So TextDamage hien thi toi da cung luc. Vuot qua se tai su dung cai cu nhat (khong tao them).")]
        [SerializeField, Min(1)] private int maxActiveCount = 64;

        [Header("Spawn")]
        [Tooltip("Ban kinh vung xuat hien ngau nhien quan vi tri gay sat thuong (world space).")]
        [SerializeField, Min(0f)] private float spawnRadius = 0.5f;

        [Header("Movement")]
        [Tooltip("Toc do di chuyen ra khoi tam (don vi / giay).")]
        [SerializeField, Min(0f)] private float moveSpeed = 1f;

        [Tooltip("Thoi gian TextDamage hien thi truoc khi tra ve pool (giay).")]
        [SerializeField, Min(0.01f)] private float lifetime = 0.5f;

        [Header("Text")]
        [Tooltip("Font hien thi. De trong se dung default font cua TMP Settings (LiberationSans SDF).")]
        [SerializeField] private TMP_FontAsset font;

        [Tooltip("Co chu (world space).")]
        [SerializeField, Min(1)] private int fontSize = 14;

        [Tooltip("Mau chu sat thuong Enemy nhan tru vao mau.")]
        [SerializeField] private Color enemyDamageColor = Color.red;

        [Tooltip("Mau chu sat thuong Tank nhan tru vao mau.")]
        [SerializeField] private Color tankDamageColor = Color.yellow;

        [Tooltip("Mau chu gia tri hoi mau (Tank va Enemy).")]
        [SerializeField] private Color healColor = Color.green;

        [Tooltip("Mau chu gia tri bi chan boi khien (Tank va Enemy).")]
        [SerializeField] private Color shieldAbsorbColor = new Color(0.25f, 0.75f, 1f, 1f);

        [Header("Text Outline")]
        [Tooltip("Mau vien chu TextDamage.")]
        [SerializeField] private Color outlineColor = Color.black;

        [Tooltip("Do day vien chu TextDamage (0 = khong co vien, gia tri thuong dung 0.1 - 0.5).")]
        [SerializeField, Range(0f, 1f)] private float outlineThickness = 0.2f;

        [Tooltip("Sorting Layer cho số sát thương; đặt trên layer sprite của boss/enemy để không bị che.")]
        [SerializeField] private string sortingLayerName = "Enemy";

        [Tooltip("Sort order de TextDamage hien tren sprite (URP 2D).")]
        [SerializeField] private int sortingOrder = 500;

        [Header("Lifetime Curves")]
        [Tooltip("Alpha nhan (0..1) theo tien do lifetime: 0 = moi spawn, 1 = het lifetime. Mac dinh giu 1 den 0.7 roi xuong 0 (nhan o phia cuoi).")]
        [SerializeField] private AnimationCurve alphaOverLifetime = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.7f, 1f),
            new Keyframe(1f, 0f));

        [Tooltip("Scale nhan theo tien do lifetime: 0 = moi spawn, 1 = het lifetime. Mac dinh giu 1 den 0.7 roi xuong 0 (nho o phia cuoi).")]
        [SerializeField] private AnimationCurve scaleOverLifetime = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.7f, 1f),
            new Keyframe(1f, 0f));

        #endregion

        #region Singleton

        private static TextValueManager m_Instance;

        /// <summary>Đảm bảo cảnh báo thiếu Outline chỉ in một lần cho cả phiên.</summary>
        private static bool s_OutlineWarningLogged;

        /// <summary>Instance duy nhat. Tu tao GameObject neu chua co trong Scene.</summary>
        public static TextValueManager Instance
        {
            get
            {
                if (m_Instance == null)
                {
                    m_Instance = FindAnyObjectByType<TextValueManager>();
                    if (m_Instance == null)
                    {
                        var go = new GameObject(nameof(TextValueManager));
                        m_Instance = go.AddComponent<TextValueManager>();
                    }
                }

                return m_Instance;
            }
        }

        #endregion

        #region Pool state

        /// <summary>Trang thai cua 1 TextDamage (cache component + trang thai bay).</summary>
        private struct ActiveItem
        {
            public GameObject Go;
            public Transform Transform;
            public TextMeshPro Text;
            public MeshRenderer Renderer;
            public Vector3 Direction;
            public Vector3 BaseScale;
            public Color BaseColor;
            public float Elapsed;
        }

        private readonly Stack<ActiveItem> m_Pool = new Stack<ActiveItem>();
        private readonly List<ActiveItem> m_Active = new List<ActiveItem>();

        private Transform m_Container;

        #endregion


        #region Lifecycle

        private void Awake()
        {
            // Guard instance trung: giu instance dau tien, huy cac cai sau.
            if (m_Instance != null && m_Instance != this)
            {
                Debug.LogWarning($"[{nameof(TextValueManager)}] Co instance trung, giu instance dau tien.");
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
            //DontDestroyOnLoad(gameObject);

            var container = new GameObject("TextDamagePool");
            m_Container = container.transform;
            m_Container.SetParent(transform, false);

            Prewarm();
        }

        private void OnDestroy()
        {
            if (m_Instance == this) m_Instance = null;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            // Duyet nguoc de RemoveAt khong lam mat index.
            for (int i = m_Active.Count - 1; i >= 0; i--)
            {
                ActiveItem item = m_Active[i];
                item.Elapsed += deltaTime;

                // Het lifetime -> an + tra ve pool.
                if (item.Elapsed >= lifetime)
                {
                    item.Go.SetActive(false);
                    m_Pool.Push(item);
                    m_Active.RemoveAt(i);
                    continue;
                }

                // Di chuyen ra ngoai (nguoc huong ve tam vi tri gay sat thuong).
                item.Transform.position += item.Direction * (moveSpeed * deltaTime);

                // Alpha + scale theo curve tren tien do lifetime.
                ApplyVisual(item, item.Elapsed / lifetime);

                m_Active[i] = item;
            }
        }

        private void OnValidate()
        {
            prewarmCount = Mathf.Max(0, prewarmCount);
            maxActiveCount = Mathf.Max(1, maxActiveCount);
            spawnRadius = Mathf.Max(0f, spawnRadius);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            lifetime = Mathf.Max(0.01f, lifetime);
            fontSize = Mathf.Max(1, fontSize);
            outlineThickness = Mathf.Clamp01(outlineThickness);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Hien thi TextValue tai vi tri world voi loai va mau tuong ung.
        /// </summary>
        public void Show(float value, Vector3 worldPosition, TextValueType type = TextValueType.EnemyDamage)
        {
            if (value <= 0f) return;

            ActiveItem item = RentItem();

            // Vi tri xuat hien: ngau nhien trong vung ban kinh quan vi tri gay sat thuong.
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPosition = worldPosition + new Vector3(offset.x, offset.y, 0f);

            // Huong di chuyen: ra ngoai, nguoc huong ve tam (vi tri gay sat thuong).
            Vector3 direction = spawnPosition - worldPosition;
            direction += Vector3.forward;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                Vector2 randomDirection = Random.insideUnitCircle;
                direction = new Vector3(randomDirection.x, randomDirection.y, 0f);
            }
            if (direction.sqrMagnitude <= 0.0001f) direction = Vector3.right;
            direction.Normalize();

            item.Elapsed = 0f;
            item.Direction = direction;
            item.BaseColor = GetColorForType(type);
            item.Transform.position = spawnPosition;

            // Dat font truoc text de TMP chon dung material/shader cua Font Asset.
            item.Text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            item.Text.fontSize = fontSize;
            item.Text.text = Mathf.RoundToInt(value).ToString();

            // Buoc TMP cap nhat mesh/material truoc khi ghi cac property outline.
            item.Text.ForceMeshUpdate();

            // Dung material instance rieng cho TextDamage, sau do bat keyword OUTLINE_ON.
            // Chi set OutlineWidth thoi co the chua du neu keyword shader dang tat.
            Material textMaterial = item.Text.fontMaterial;

            if (textMaterial != null &&
                textMaterial.HasProperty(ShaderUtilities.ID_OutlineColor) &&
                textMaterial.HasProperty(ShaderUtilities.ID_OutlineWidth))
            {
                textMaterial.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
                textMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineThickness);

                if (outlineThickness > 0f)
                    textMaterial.EnableKeyword("OUTLINE_ON");
                else
                    textMaterial.DisableKeyword("OUTLINE_ON");

                // Gan lai material instance de TMP su dung material da cap nhat.
                item.Text.fontMaterial = textMaterial;
                item.Text.SetMaterialDirty();
                item.Text.ForceMeshUpdate();
            }
            else
            {
                // Chỉ cảnh báo một lần: thiếu outline không chặn hiển thị, nhưng log mỗi hạt
                // sát thương sẽ bắn hàng nghìn dòng và làm chậm trận đông quái.
                if (!s_OutlineWarningLogged)
                {
                    s_OutlineWarningLogged = true;
                    Debug.LogWarning(
                        $"[{nameof(TextValueManager)}] Font material khong ho tro Outline. " +
                        "Hay dung TMP Distance Field shader cho Font Asset.",
                        item.Go);
                }
            }

            item.Renderer.sortingLayerID = SortingLayer.NameToID(sortingLayerName);
            item.Renderer.sortingOrder = sortingOrder;

            // Reset visual tai moc 0 cua lifetime (color + scale theo curve).
            ApplyVisual(item, 0f);

            if (!item.Go.activeSelf) item.Go.SetActive(true);
            m_Active.Add(item);
        }

        #endregion

        #region Pool internals

        private void Prewarm()
        {
            for (int i = 0; i < prewarmCount; i++)
            {
                ActiveItem item = CreateItem();
                item.Go.SetActive(false);
                m_Pool.Push(item);
            }
        }

        /// <summary>Lay TextDamage tu pool. Vuot toi da -> tai su dung cai cu nhat.</summary>
        private ActiveItem RentItem()
        {
            if (m_Pool.Count > 0) return m_Pool.Pop();

            if (m_Active.Count >= maxActiveCount)
            {
                // Dau list = spawn som nhat -> dung lai, Show se reset trang thai.
                ActiveItem oldest = m_Active[0];
                m_Active.RemoveAt(0);
                return oldest;
            }

            return CreateItem();
        }

        /// <summary>Tao TextDamage moi voi TextMeshPro (world space).</summary>
        private ActiveItem CreateItem()
        {
            var go = new GameObject("TextDamage");
            if (m_Container != null) go.transform.SetParent(m_Container, false);

            var text = go.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;

            // TextMeshPro RequireComponent MeshRenderer -> khong can them tay.
            var meshRenderer = go.GetComponent<MeshRenderer>();

            return new ActiveItem
            {
                Go = go,
                Transform = go.transform,
                Text = text,
                Renderer = meshRenderer,
                BaseScale = Vector3.one,
                Elapsed = 0f,
            };
        }

        private Color GetColorForType(TextValueType type)
        {
            switch (type)
            {
                case TextValueType.EnemyDamage: return enemyDamageColor;
                case TextValueType.TankDamage: return tankDamageColor;
                case TextValueType.Heal: return healColor;
                case TextValueType.ShieldAbsorb: return shieldAbsorbColor;
                default: return enemyDamageColor;
            }
        }

        /// <summary>
        /// Ap dung alpha + scale theo 2 AnimationCurve RIENG tren tien do lifetime (0..1).
        /// </summary>
        private void ApplyVisual(ActiveItem item, float progress)
        {
            float alphaMultiplier = alphaOverLifetime != null
                ? Mathf.Clamp01(alphaOverLifetime.Evaluate(progress))
                : 1f;
            float scaleMultiplier = scaleOverLifetime != null
                ? Mathf.Clamp01(scaleOverLifetime.Evaluate(progress))
                : 1f;

            Color displayColor = item.BaseColor;
            displayColor.a *= alphaMultiplier;

            item.Text.color = displayColor;
            item.Transform.localScale = item.BaseScale * scaleMultiplier;
        }

        #endregion
    }
}



