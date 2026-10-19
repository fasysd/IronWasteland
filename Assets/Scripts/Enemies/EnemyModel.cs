using System;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Phan Visual + Physical cua ke dich: sprite, collider.
    /// Vai tro giong TankView nhung NAM TRONG cung prefab voi EnemyBrain
    /// (khong tao prefab rieng, khac voi TankView).
    ///
    /// - Xu ly hien thi (SpriteRenderer) va va cham (BoxCollider2D + ColliderOwnerRef).
    /// - KHONG xu ly logic game - no chi forward su kien len EnemyBrain qua C# event.
    /// - EnemyBrain la cha (parent), EnemyModel la con:
    ///   collider tren Model tu attach vao Rigidbody2D cua GameObject cha.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyModel : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("SpriteRenderer hien thi hinh anh ke dich (tam thoi dung Square mac dinh cua Unity).")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("Collider va cham cua ke dich. Nam tren Model nhung attach vao Rigidbody2D cua cha.")]
        [SerializeField] private BoxCollider2D bodyCollider;

        [Tooltip("ColliderOwnerRef cung GameObject - cho phep ben ngoai tra chu va dang ky event va cham.")]
        [SerializeField] private ColliderOwnerRef ownerRef;

        [Tooltip("Animator dieu khien animation cua ke dich (Idle, Move, TakeDamage, TakedDamege).")]
        [SerializeField] private Animator animator;

        [Header("Owner")]
        [Tooltip("EnemyBrain dang dieu khien Model nay. Do EnemyBrain gan luc khoi tao.")]
        [SerializeField, HideInInspector] private EnemyBrain owner;

        private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
        private static readonly int HashTakeDamage = Animator.StringToHash("TakeDamage");
        private static readonly int HashTakedDamage = Animator.StringToHash("TakedDamege");
        private static readonly int HashTakedDamageAlt = Animator.StringToHash("TakedDamage");

        /// <summary>Su kien cham giua collider nay va collider khac - forward tu ColliderOwnerRef.</summary>
        public event Action<ColliderOwnerRef> Contact;

        /// <summary>Su kien trigger enter - forward tu ColliderOwnerRef.</summary>
        public event Action<Collider2D> TriggerEntered;

        public EnemyBrain Owner => owner;
        public bool HasOwner => owner != null;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public BoxCollider2D BodyCollider => bodyCollider;
        public ColliderOwnerRef OwnerRef => ownerRef;
        public Animator Animator
        {
            get
            {
                if (animator == null) animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
                return animator;
            }
        }

        /// <summary>
        /// EnemyBrain goi luc khoi tao - gan owner (dua vao ColliderOwnerRef
        /// de ben ngoai raycast/trigger duoc tim ra Brain).
        /// </summary>
        public void RegisterOwner(EnemyBrain value)
        {
            owner = value;

            if (ownerRef != null && value != null)
            {
                ownerRef.SetOwner(value);
            }
        }

        // --- Visual & Animation control ---

        /// <summary>Hieu ung xuat hien khi ke dich spawn. TODO.</summary>
        public void PlaySpawn() { }

        /// <summary>Dieu chinh thong so MoveSpeed cho Animator (chuyen giua Idle va Move).</summary>
        public void SetMoveSpeed(float speed)
        {
            if (Animator != null)
            {
                Animator.SetFloat(HashMoveSpeed, speed);
            }
        }

        /// <summary>Kich hoat animation nhan sat thuong (TakeDamage).</summary>
        public void PlayTakeDamage()
        {
            if (Animator != null)
            {
                Animator.SetTrigger(HashTakeDamage);
            }
        }

        /// <summary>Kich hoat animation chet / bi ha guc (TakedDamage).</summary>
        public void PlayTakedDamage()
        {
            if (Animator != null)
            {
                Animator.SetTrigger(HashTakedDamage);
                Animator.SetTrigger(HashTakedDamageAlt);
            }
        }

        /// <summary>Hieu ung khi nhan sat thuong.</summary>
        public void PlayHit() => PlayTakeDamage();

        /// <summary>Hieu ung khi chet.</summary>
        public void PlayDeath() => PlayTakedDamage();

        private void Awake()
        {
            // Tu noi neu designer khong keo tay (giong OnValidate cua TankView).
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (bodyCollider == null) bodyCollider = GetComponent<BoxCollider2D>();
            if (ownerRef == null) ownerRef = GetComponent<ColliderOwnerRef>();
            if (animator == null) animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();

            // Forward su kien va cham len EnemyBrain (qua event cua Model).
            if (ownerRef != null)
            {
                ownerRef.Contact += HandleContact;
                ownerRef.TriggerEnter += HandleTriggerEnter;
            }
        }

        private void OnDestroy()
        {
            if (ownerRef != null)
            {
                ownerRef.Contact -= HandleContact;
                ownerRef.TriggerEnter -= HandleTriggerEnter;
            }

            Contact = null;
            TriggerEntered = null;
        }

        private void HandleContact(ColliderOwnerRef other) => Contact?.Invoke(other);

        private void HandleTriggerEnter(Collider2D other) => TriggerEntered?.Invoke(other);

        private void OnValidate()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (bodyCollider == null) bodyCollider = GetComponent<BoxCollider2D>();
            if (ownerRef == null) ownerRef = GetComponent<ColliderOwnerRef>();
            if (animator == null) animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        }
    }
}