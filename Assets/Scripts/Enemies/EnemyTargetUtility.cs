using IronWasteland.Core;
using IronWasteland.Enemies.The2Face;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Helper dùng chung cho quái thường: nhận diện và gây sát thương bình đẳng lên cả
    /// <see cref="TankBrain"/> và <see cref="The2FaceLightPoint"/> (không ưu tiên).
    /// </summary>
    public static class EnemyTargetUtility
    {
        public static Transform ScanNearestTarget(Vector2 currentPos, Vector2 anchorPointD, float radiusC, int layerMask)
        {
            int mask = layerMask != 0 ? layerMask : LayerMask.GetMask("Tank");
            Collider2D[] hits = Physics2D.OverlapCircleAll(anchorPointD, radiusC, mask);
            if (hits == null || hits.Length == 0) return null;

            Transform bestTarget = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;

                if (!TryResolveTarget(hit, out Transform targetTransform, out bool isAlive) || !isAlive)
                    continue;

                Vector2 targetPos = targetTransform.position;
                Vector2 closestPoint = hit.bounds.ClosestPoint(anchorPointD);
                if (Vector2.Distance(targetPos, anchorPointD) > radiusC && Vector2.Distance(closestPoint, anchorPointD) > radiusC)
                    continue;

                float dist = Vector2.Distance(currentPos, targetPos);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    bestTarget = targetTransform;
                }
            }

            return bestTarget;
        }

        public static bool TryResolveTarget(Collider2D hit, out Transform targetTransform, out bool isAlive)
        {
            targetTransform = null;
            isAlive = false;
            if (hit == null) return false;

            ColliderOwnerRef ownerRef = hit.GetComponent<ColliderOwnerRef>() ?? hit.GetComponentInParent<ColliderOwnerRef>();
            if (ownerRef != null)
            {
                if (TryResolveFromObject(ownerRef.Owner, out targetTransform, out isAlive))
                    return true;
            }

            TankBrain tank = hit.GetComponentInParent<TankBrain>();
            if (tank != null)
            {
                targetTransform = tank.transform;
                isAlive = tank.IsAlive;
                return true;
            }

            The2FaceLightPoint lightPoint = hit.GetComponentInParent<The2FaceLightPoint>();
            if (lightPoint != null)
            {
                targetTransform = lightPoint.transform;
                isAlive = lightPoint.IsAlive;
                return true;
            }

            return false;
        }

        public static bool TryResolveTargetComponent(Collider2D hit, out Component target, out bool isAlive)
        {
            target = null;
            isAlive = false;
            if (!TryResolveTarget(hit, out Transform targetTransform, out isAlive) || targetTransform == null)
                return false;

            return TryResolveTargetComponent(targetTransform, out target, out isAlive);
        }

        public static bool TryResolveTargetComponent(Transform targetTransform, out Component target, out bool isAlive)
        {
            target = null;
            isAlive = false;
            if (targetTransform == null) return false;

            TankBrain tank = targetTransform.GetComponent<TankBrain>() ?? targetTransform.GetComponentInParent<TankBrain>();
            if (tank != null)
            {
                target = tank;
                isAlive = tank.IsAlive;
                return true;
            }

            The2FaceLightPoint lightPoint = targetTransform.GetComponent<The2FaceLightPoint>()
                ?? targetTransform.GetComponentInParent<The2FaceLightPoint>();
            if (lightPoint != null)
            {
                target = lightPoint;
                isAlive = lightPoint.IsAlive;
                return true;
            }

            return false;
        }

        public static bool IsTargetAlive(Component target)
        {
            if (target == null) return false;
            if (target is TankBrain tank) return tank.IsAlive;
            if (target is The2FaceLightPoint lightPoint) return lightPoint.IsAlive;
            return TryResolveTargetComponent(target.transform, out _, out bool isAlive) && isAlive;
        }

        public static bool TryDealDamage(
            EnemyBrain attacker,
            Collider2D hit,
            float damage,
            Vector3 hitPos,
            DamageSourceType sourceType = DamageSourceType.EnemyAttack)
        {
            if (hit == null || damage <= 0f) return false;
            if (!TryResolveTarget(hit, out Transform targetTransform, out bool isAlive) || !isAlive) return false;

            return TryDealDamage(attacker, targetTransform, damage, hitPos, sourceType);
        }

        public static bool TryDealDamage(
            EnemyBrain attacker,
            Component target,
            float damage,
            Vector3 hitPos,
            DamageSourceType sourceType = DamageSourceType.EnemyAttack)
        {
            if (target == null || damage <= 0f) return false;

            TankBrain tank = target as TankBrain ?? target.GetComponent<TankBrain>() ?? target.GetComponentInParent<TankBrain>();
            if (tank != null && tank.IsAlive)
            {
                DamageInfo info = new DamageInfo(attacker, tank, damage, 0f, hitPos, sourceType: sourceType);
                tank.TakeDamage(info);
                return true;
            }

            The2FaceLightPoint lightPoint = target as The2FaceLightPoint
                ?? target.GetComponent<The2FaceLightPoint>()
                ?? target.GetComponentInParent<The2FaceLightPoint>();
            if (lightPoint != null && lightPoint.IsAlive)
            {
                lightPoint.ApplyDamage(damage);
                return true;
            }

            return false;
        }

        private static bool TryResolveFromObject(Object owner, out Transform targetTransform, out bool isAlive)
        {
            targetTransform = null;
            isAlive = false;
            if (owner == null) return false;

            if (owner is TankBrain tb)
            {
                targetTransform = tb.transform;
                isAlive = tb.IsAlive;
                return true;
            }

            if (owner is The2FaceLightPoint lp)
            {
                targetTransform = lp.transform;
                isAlive = lp.IsAlive;
                return true;
            }

            if (owner is GameObject go)
            {
                TankBrain tank = go.GetComponent<TankBrain>() ?? go.GetComponentInParent<TankBrain>();
                if (tank != null)
                {
                    targetTransform = tank.transform;
                    isAlive = tank.IsAlive;
                    return true;
                }

                The2FaceLightPoint point = go.GetComponent<The2FaceLightPoint>() ?? go.GetComponentInParent<The2FaceLightPoint>();
                if (point != null)
                {
                    targetTransform = point.transform;
                    isAlive = point.IsAlive;
                    return true;
                }
            }

            if (owner is Component comp)
            {
                TankBrain tank = comp.GetComponent<TankBrain>() ?? comp.GetComponentInParent<TankBrain>();
                if (tank != null)
                {
                    targetTransform = tank.transform;
                    isAlive = tank.IsAlive;
                    return true;
                }

                The2FaceLightPoint point = comp.GetComponent<The2FaceLightPoint>() ?? comp.GetComponentInParent<The2FaceLightPoint>();
                if (point != null)
                {
                    targetTransform = point.transform;
                    isAlive = point.IsAlive;
                    return true;
                }
            }

            return false;
        }
    }
}
