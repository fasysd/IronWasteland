#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Cờ kiểm thử dev-only: host tự bắt đầu trận và spawn một boss để smoke test đồng bộ.
    /// Chỉ tồn tại ở Editor/Development Build, không ảnh hưởng bản phát hành.
    /// </summary>
    public static class BossProbe
    {
        private const string CatalogResource = "BossProbeCatalog";
        private const float StartDeadline = 40f;
        private static int s_PendingIndex = -1;

        /// <summary>Đọc tham số -iw-boss: số thứ tự hoặc tên prefab boss.</summary>
        public static bool Request(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;

            if (int.TryParse(token, out int index))
            {
                s_PendingIndex = Mathf.Max(0, index);
                return true;
            }

            BossProbeCatalog catalog = Resources.Load<BossProbeCatalog>(CatalogResource);
            if (catalog == null || catalog.Prefabs == null) return false;

            for (int i = 0; i < catalog.Prefabs.Length; i++)
            {
                GameObject prefab = catalog.Prefabs[i];
                if (prefab == null || prefab.name != token) continue;
                s_PendingIndex = i;
                return true;
            }

            return false;
        }

        /// <summary>Host tự bắt đầu trận sau khi đủ người và tất cả đã sẵn sàng.</summary>
        public static IEnumerator AutoStartRoutine(NetBootstrap boot)
        {
            float deadline = Time.time + StartDeadline;
            while (Time.time < deadline)
            {
                MatchFlow flow = boot != null ? boot.Flow : null;
                if (flow != null && flow.PlayerCount >= 2 && flow.Phase == MatchPhase.Lobby)
                {
                    bool allReady = true;
                    for (int i = 0; i < flow.PlayerCount; i++)
                    {
                        if (!flow.TryGetPlayer(i, out _, out _, out bool ready) || !ready)
                        {
                            allReady = false;
                            break;
                        }
                    }

                    if (allReady)
                    {
                        Debug.Log("[BossProbe] bat dau tran de kiem tra boss.");
                        flow.StartMatch();
                        yield break;
                    }
                }

                yield return new WaitForSeconds(0.5f);
            }

            Debug.LogWarning("[BossProbe] het thoi gian cho client san sang.");
        }

        /// <summary>MatchFlow gọi trên host sau khi đã spawn xe; trả về true nếu có boss được spawn.</summary>
        public static bool TrySpawn(Vector3 position)
        {
            if (s_PendingIndex < 0) return false;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || !manager.IsListening) return false;

            BossProbeCatalog catalog = Resources.Load<BossProbeCatalog>(CatalogResource);
            if (catalog == null || catalog.Prefabs == null || s_PendingIndex >= catalog.Prefabs.Length)
            {
                Debug.LogWarning("[BossProbe] thieu catalog hoac chi so boss khong hop le.");
                s_PendingIndex = -1;
                return false;
            }

            GameObject prefab = catalog.Prefabs[s_PendingIndex];
            if (prefab == null)
            {
                Debug.LogWarning("[BossProbe] prefab boss dang null.");
                s_PendingIndex = -1;
                return false;
            }

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError("[BossProbe] prefab boss thieu NetworkObject.");
                Object.Destroy(instance);
                s_PendingIndex = -1;
                return false;
            }

            networkObject.Spawn(true);
            Debug.Log($"[BossProbe] da spawn boss '{prefab.name}'.");
            s_PendingIndex = -1;
            return true;
        }
    }
}
#endif
