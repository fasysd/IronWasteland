using Unity.Netcode.Components;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Cầu nối phát trigger animation quái theo đúng quyền mạng.
    /// Host gọi qua NetworkAnimator để client nhận cùng trạng thái; khi chưa spawn (offline)
    /// hoặc object không có NetworkAnimator thì rơi về Animator cục bộ.
    /// </summary>
    public static class NetEnemyAnimation
    {
        /// <summary>Phát một trigger animation, ưu tiên NetworkAnimator khi object đã spawn trên host.</summary>
        public static void Trigger(GameObject owner, Animator fallback, string triggerName)
        {
            if (string.IsNullOrEmpty(triggerName)) return;
            Trigger(owner, fallback, Animator.StringToHash(triggerName));
        }

        /// <summary>Phát một trigger animation, ưu tiên NetworkAnimator khi object đã spawn trên host.</summary>
        public static void Trigger(GameObject owner, Animator fallback, int hash)
        {
            if (hash == 0) return;

            if (owner != null)
            {
                NetworkAnimator networkAnimator = owner.GetComponent<NetworkAnimator>();
                if (networkAnimator == null) networkAnimator = owner.GetComponentInParent<NetworkAnimator>(true);

                if (networkAnimator != null && networkAnimator.IsSpawned && networkAnimator.IsServer)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log("[NetAnim] host trigger " + owner.name + " hash=" + hash);
#endif
                    networkAnimator.SetTrigger(hash);
                    return;
                }
            }

            if (fallback != null) fallback.SetTrigger(hash);
        }
    }
}
