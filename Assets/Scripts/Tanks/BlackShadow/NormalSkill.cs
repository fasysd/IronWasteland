using IronWasteland;
using IronWasteland.Enemies;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(ColliderOwnerRef))]
public class NormalSkill : MonoBehaviour
{
    public event Action<NormalSkill, EnemyBrain> TargetFound;
    [SerializeField, Min(0f)] private float speed = 10f;

    [SerializeField, Min(0f)] private float hitCheckDelay = 0.1f;

    [SerializeField, Min(0f)] private float lifeTime = 10f;

    private Rigidbody2D body;

    private ColliderOwnerRef colliderOwnerRef;

    private Vector2 direction;

    private float elapseTime;

    private bool initialized;


    private Coroutine hitCheckCoroutine;
    private Action<NormalSkill> returnToPool;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        colliderOwnerRef = GetComponent<ColliderOwnerRef>();
    }
    private void OnEnable()
    {
        if (colliderOwnerRef != null)
        {
            colliderOwnerRef.TriggerEnter += ColliderOwnerRef_TriggerEnter;
        }
    }
    private IEnumerator CheckTargetsAfterDelay()
    {
        yield return new WaitForSeconds(hitCheckDelay);
        hitCheckCoroutine = null;
        if (!this || !gameObject.activeInHierarchy || !initialized)
            yield break;
        var enemies = new HashSet<EnemyBrain>();
        foreach(Collider2D collider in colliderOwnerRef.GetTriggerColliders())
        {
            if(collider == null) continue;
            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collider);
            if(targetRef == null) continue;
            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if(enemy != null) enemies.Add(enemy);
        }
        if(enemies.Count == 0)
        {
            yield break;
        }

        foreach (EnemyBrain enemy in enemies)
        {
            if (enemy != null)
                TargetFound?.Invoke(this, enemy);
        }
    }
    private void ColliderOwnerRef_TriggerEnter(Collider2D obj)
    {
        if (!initialized)
            return;

        hitCheckCoroutine = StartCoroutine(CheckTargetsAfterDelay());
    }
    public void Clear()
    {
        if(hitCheckCoroutine != null)
        {
            StopCoroutine(hitCheckCoroutine);
            hitCheckCoroutine = null;
        }
        if(colliderOwnerRef != null)
        {
            colliderOwnerRef.TriggerEnter -= ColliderOwnerRef_TriggerEnter;
        }
        if(body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
        direction = Vector2.zero;
        elapseTime = 0f;
        initialized = false;
        returnToPool = null;
        TargetFound = null;
    }
    private void OnDisable()
    {
        Clear();
    }
    private void FinishProjectile()
    {
        Action<NormalSkill> returnToPool = this.returnToPool;

        Clear();

        if (returnToPool != null)
            returnToPool(this);
        else if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }
    public void Initialize(Vector2 direction, Action<NormalSkill> returnToPool)
    {
        Clear();

        this.direction = direction.normalized;
        this.returnToPool = returnToPool;
        this.initialized = direction.sqrMagnitude > 0.001f;
        if (!initialized)
        {
            FinishProjectile();
            return;
        }
        elapseTime = 0f;
        transform.right = direction;
        if(body!= null)
        {
            body.linearVelocity = direction * speed;
        }
        if(colliderOwnerRef != null && isActiveAndEnabled)
        {
            colliderOwnerRef.TriggerEnter += ColliderOwnerRef_TriggerEnter;
        }


    }
    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        lifeTime = Mathf.Max(0.1f, lifeTime);
        hitCheckDelay = Mathf.Max(0f, hitCheckDelay);
    }

    void Start()
    {
           

    }

    // Update is called once per frame
    void Update()
    {
        if(!initialized)
        {
            return;
        }
        elapseTime += Time.deltaTime;
        if(elapseTime >= lifeTime)
        {
            FinishProjectile();
        }
    }
    private void FixedUpdate()
    {
        if (!initialized || body == null)
            return;

        body.linearVelocity = direction * speed;

    }
}
