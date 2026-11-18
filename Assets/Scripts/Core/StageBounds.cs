using UnityEngine;
using IronWasteland.Tanks;

namespace IronWasteland.Core
{
    /// <summary>
    /// Chan tank nguoi choi khong chay ra khoi bien ban do va keo camera cung bi chan theo.
    ///
    /// Vi sao can: truoc day tank chay tu do ra ngoai vung co background, den khi chi con
    /// khoang khong den thi mat dau. Component nay tu dong xuat hien trong moi scene (khong
    /// phai gan tay vao tung scene): no do kich thuoc object "Background" roi lay rect do
    /// lam bien, sau do moi FixedUpdate keo vi tri Rigidbody2D cua tank ve trong bien.
    ///
    /// Khong can gan vao scene nao: [RuntimeInitializeOnLoadMethod] se tao instance khi scene
    /// load xong. Muon doi bien thi gan component nay vao 1 object trong scene va sua
    /// "manualBounds" / "inset".
    /// </summary>
    [DisallowMultipleComponent]
    public class StageBounds : MonoBehaviour
    {
        [Header("Bien (de trong = tu do theo Background)")]
        [Tooltip("Bat de dung dong 'bounds' ben duoi thay vi do theo Background.")]
        [SerializeField] private bool useManualBounds;

        [SerializeField] private Rect manualBounds = new Rect(-18.8f, -10.5f, 37.6f, 21f);

        [Tooltip("Chua vao trong bien mot chut de khong bi cat mep hinh tank (nua than tank ~1,5 unit).")]
        [SerializeField, Min(0f)] private float inset = 1.6f;

        [Tooltip("Bat camera cung bi chan theo bien nay.")]
        [SerializeField] private bool clampCamera = true;

        [Header("Tranh goc HUD")]
        [Tooltip("Khi camera bi chan o mep phai, cum o ky nang nam o goc duoi-phai man hinh. " +
                 "Bat de tank khong di vao dung goc do (tranh bi HUD che).")]
        [SerializeField] private bool avoidHudCorner = true;

        [Tooltip("Kich thuoc vung tranh o goc duoi-phai, tinh bang unit (rong, cao).")]
        [SerializeField] private Vector2 hudCornerSize = new Vector2(7f, 3.6f);

        [Tooltip("In ra Console khi khoi tao (chi de debug).")]
        [SerializeField] private bool logEvents;

        private Rect m_Bounds;
        private GameController m_GameController;
        private Rigidbody2D m_Body;
        private CameraFollow2D m_Follow;

        public Rect Bounds => m_Bounds;

        /// <summary>Tao component nay mot lan cho moi scene (khong can gan tay).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<StageBounds>() != null) return;
            GameObject go = new GameObject("StageBounds");
            go.AddComponent<StageBounds>();
        }

        private void Awake()
        {
            m_Bounds = useManualBounds ? manualBounds : MeasureBackground();
            if (m_Bounds.width <= 0.01f || m_Bounds.height <= 0.01f)
            {
                m_Bounds = new Rect(-18.8f, -10.5f, 37.6f, 21f);
            }

            if (logEvents) Debug.Log($"[StageBounds] bien = {m_Bounds}");
        }

        private void Start()
        {
            Resolve();
            if (clampCamera && m_Follow != null)
            {
                m_Follow.SetBounds(new Bounds(m_Bounds.center, new Vector3(m_Bounds.width, m_Bounds.height, 0f)));
            }
        }

        private void FixedUpdate()
        {
            if (m_Body == null) Resolve();
            if (m_Body == null) return;

            Vector2 p = m_Body.position;
            Vector2 clamped = Clamp(p);
            if (clamped == p) return;

            m_Body.position = clamped;

            // Dung lai o huong bi chan de tank khong "dui" vao tuong mai.
            Vector2 v = m_Body.linearVelocity;
            if (!Mathf.Approximately(clamped.x, p.x)) v.x = 0f;
            if (!Mathf.Approximately(clamped.y, p.y)) v.y = 0f;
            m_Body.linearVelocity = v;
        }

        private void LateUpdate()
        {
            // Luoi an toan: co skill teleport thang bang transform nen clamp them lan nua.
            if (m_GameController == null || m_GameController.PlayerTank == null) return;

            Transform t = m_GameController.PlayerTank.transform;
            Vector3 pos = t.position;
            Vector2 clamped = Clamp(pos);
            if (!Mathf.Approximately(pos.x, clamped.x) || !Mathf.Approximately(pos.y, clamped.y))
            {
                t.position = new Vector3(clamped.x, clamped.y, pos.z);
            }
        }

        private Vector2 Clamp(Vector2 p)
        {
            float minX = m_Bounds.xMin + inset;
            float maxX = m_Bounds.xMax - inset;
            float minY = m_Bounds.yMin + inset;
            float maxY = m_Bounds.yMax - inset;

            if (minX > maxX) { float mid = (minX + maxX) * 0.5f; minX = maxX = mid; }
            if (minY > maxY) { float mid = (minY + maxY) * 0.5f; minY = maxY = mid; }

            Vector2 c = new Vector2(Mathf.Clamp(p.x, minX, maxX), Mathf.Clamp(p.y, minY, maxY));

            // Goc duoi-phai man hinh la cum o ky nang: day tank ra khoi hop do theo huong gan nhat.
            if (avoidHudCorner)
            {
                float boxMinX = maxX - hudCornerSize.x;
                float boxMaxY = minY + hudCornerSize.y;

                if (c.x > boxMinX && c.y < boxMaxY)
                {
                    float dx = c.x - boxMinX;
                    float dy = boxMaxY - c.y;
                    if (dx <= dy) c.x = boxMinX;
                    else c.y = boxMaxY;
                }
            }

            return c;
        }

        private void Resolve()
        {
            if (m_GameController == null) m_GameController = FindAnyObjectByType<GameController>();
            if (m_GameController != null && m_GameController.PlayerTank != null)
            {
                m_Body = m_GameController.PlayerTank.GetComponent<Rigidbody2D>();
                if (m_Body == null) m_Body = m_GameController.PlayerTank.GetComponentInChildren<Rigidbody2D>();
            }

            if (m_Follow == null) m_Follow = FindAnyObjectByType<CameraFollow2D>();
        }

        /// <summary>Do rect cua object "Background" (fallback: vung 37,6 x 21 quanh goc toa do).</summary>
        private Rect MeasureBackground()
        {
            SpriteRenderer[] all = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            SpriteRenderer background = null;

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.name == "Background") { background = all[i]; break; }
                if (background == null && all[i].sortingOrder <= -900) background = all[i];
            }

            if (background == null) return new Rect(-18.8f, -10.5f, 37.6f, 21f);

            Bounds b = background.bounds;
            return new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
        }

        private void OnDrawGizmosSelected()
        {
            Rect r = useManualBounds ? manualBounds : m_Bounds;
            Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.7f);
            Gizmos.DrawWireCube(new Vector3(r.center.x, r.center.y, 0f), new Vector3(r.width, r.height, 0f));
        }
    }
}
