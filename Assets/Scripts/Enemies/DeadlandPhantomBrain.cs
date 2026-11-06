using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Boss 1 cua man 1-4 - "Bong Ma Tu Dia" (tai lieu thiet ke, TABLE 9):
    /// HP 2000 / ATK 120 / Giap 45 / SPD 90.
    /// - Vet nut khong gian: mo 1 vet nut ngau nhien tren ban do, tank buoc vao se bi
    ///   dich chuyen toi vi tri cua Bong Ma.
    /// - Tang hinh: cu moi vai giay la bien mat trong thoi gian ngan.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeadlandPhantomBrain : ChasingEnemyBrain
    {
        [Header("Vet nut khong gian")]
        [Tooltip("Bao lau mo mot vet nut moi (giay).")]
        [SerializeField, Min(1f)] private float riftInterval = 8f;

        [Tooltip("Ban kinh vet nut (m).")]
        [SerializeField, Min(0.2f)] private float riftRadius = 1.5f;

        [Tooltip("Vet nut ton tai bao lau truoc khi tu dong (giay).")]
        [SerializeField, Min(1f)] private float riftLifetime = 14f;

        [Tooltip("Khoang cach gan nhat tu tank toi vet nut (m).")]
        [SerializeField, Min(1f)] private float riftMinDistance = 5f;

        [Tooltip("Khoang cach xa nhat tu tank toi vet nut (m).")]
        [SerializeField, Min(1f)] private float riftMaxDistance = 10f;

        [Header("Tang hinh")]
        [Tooltip("Chu ky tang hinh (giay).")]
        [SerializeField, Min(1f)] private float invisibilityInterval = 12f;

        [Tooltip("Thoi gian tang hinh moi lan (giay).")]
        [SerializeField, Min(0.2f)] private float invisibilityDuration = 2f;

        [Tooltip("Do mo cua sprite khi tang hinh.")]
        [SerializeField, Range(0f, 1f)] private float invisibilityAlpha = 0.22f;

        private float m_RiftTimer;
        private float m_InvisibleTimer;
        private bool m_Invisible;
        private GameObject m_Rift;
        private float m_RiftAge;

        protected override void OnBrainTick(float deltaTime)
        {
            base.OnBrainTick(deltaTime);

            TankBrain target = Target;
            if (target == null) return;

            m_RiftTimer += deltaTime;
            if (m_RiftTimer >= riftInterval && m_Rift == null)
            {
                m_RiftTimer = 0f;
                SpawnRift(target);
            }

            UpdateRift(deltaTime, target);
            UpdateInvisibility(deltaTime);
        }

        private void SpawnRift(TankBrain target)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(riftMinDistance, Mathf.Max(riftMinDistance, riftMaxDistance));
            Vector2 origin = target.transform.position;
            Vector2 position = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            m_Rift = new GameObject("VetNutKhongGian");
            m_Rift.transform.position = position;

            SpriteRenderer renderer = m_Rift.AddComponent<SpriteRenderer>();
            renderer.sprite = IronWasteland.Utilities.SpriteFactory.SoftCircle;
            renderer.color = new Color(0.6f, 0.2f, 0.95f, 0.55f);
            renderer.transform.localScale = Vector3.one * (riftRadius * 2f);
            renderer.sortingOrder = -1;

            m_RiftAge = 0f;
            Debug.Log("[Phantom] mo vet nut tai " + position.ToString("0.00"));
        }

        private void UpdateRift(float deltaTime, TankBrain target)
        {
            if (m_Rift == null) return;

            m_RiftAge += deltaTime;
            if (m_RiftAge >= riftLifetime)
            {
                DestroyRift();
                return;
            }

            float gap = Vector2.Distance(target.transform.position, m_Rift.transform.position);
            if (gap > riftRadius + 0.5f) return;

            Vector2 away = (Vector2)target.transform.position - (Vector2)transform.position;
            Vector3 destination = transform.position + (Vector3)(away.sqrMagnitude < 0.0001f
                ? Vector2.right * 0.8f
                : away.normalized * 0.8f);

            target.transform.position = destination;
            Debug.Log("[Phantom] nuot tank qua vet nut -> " + destination.ToString("0.00"));
            DestroyRift();
        }

        private void UpdateInvisibility(float deltaTime)
        {
            SpriteRenderer sprite = Model != null ? Model.SpriteRenderer : null;
            if (sprite == null) return;

            if (m_Invisible)
            {
                m_InvisibleTimer -= deltaTime;
                if (m_InvisibleTimer > 0f) return;

                m_Invisible = false;
                SetAlpha(sprite, 1f);
                return;
            }

            m_InvisibleTimer += deltaTime;
            if (m_InvisibleTimer < invisibilityInterval) return;

            m_Invisible = true;
            m_InvisibleTimer = invisibilityDuration;
            SetAlpha(sprite, invisibilityAlpha);
            Debug.Log("[Phantom] tang hinh " + invisibilityDuration.ToString("0.0") + "s");
        }

        private static void SetAlpha(SpriteRenderer sprite, float alpha)
        {
            Color color = sprite.color;
            color.a = alpha;
            sprite.color = color;
        }

        private void DestroyRift()
        {
            if (m_Rift != null) Destroy(m_Rift);
            m_Rift = null;
            m_RiftAge = 0f;
        }

        protected override void OnDeath()
        {
            DestroyRift();
            Destroy(gameObject);
        }

        private void OnDestroy() => DestroyRift();
    }
}
