using UnityEngine;

namespace IronWasteland.Stages
{
    /// <summary>
    /// Vung sinh quai tren map. Moi vung co 1 id de StageDefinition chi dinh
    /// "dot nay sinh o vung nao" (de quai ra tu hai canh, tu sau luoi...).
    ///
    /// Dat component nay len 1 GameObject rong o vi tri muon sinh quai,
    /// chinh ban kinh bang gizmo trong Scene view.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpawnZone : MonoBehaviour
    {
        [Tooltip("Id de StageDefinition tham chieu, vi du A / B / Left / Right.")]
        [SerializeField] private string zoneId = "A";

        [Tooltip("Ban kinh vung sinh quai (met). Quai se roi ngau nhien trong hinh tron nay.")]
        [SerializeField, Min(0.5f)] private float radius = 5f;

        [Tooltip("Chi dung de ve gizmo, khong anh huong gameplay.")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.45f, 0.1f, 0.35f);

        public string ZoneId => zoneId;
        public float Radius => radius;
        public Vector2 Center => transform.position;

        /// <summary>Vung nay co phuc vu id yeu cau khong (id rong = bat ky vung nao).</summary>
        public bool Matches(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return true;
            return string.Equals(id, zoneId, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Mot diem ngau nhien trong vung.</summary>
        public Vector2 RandomPoint()
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            return Center + offset;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(Center, 0.25f);
            Gizmos.DrawWireSphere(Center, radius);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireSphere(Center, radius);
#if UNITY_EDITOR
            UnityEditor.Handles.color = gizmoColor;
            UnityEditor.Handles.Label(Center + Vector2.up * (radius + 0.4f), $"SpawnZone [{zoneId}] r={radius}");
#endif
        }
    }
}
