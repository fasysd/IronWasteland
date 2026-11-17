using System;
using System.Collections.Generic;
using System.Text;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronWasteland.Net
{
    /// <summary>
    /// HUD debug tạm thời (IMGUI) để kiểm tra đồng bộ mạng trong lúc chơi online.
    /// Đây KHÔNG phải UI chính thức của game — phần thiết kế HUD do nhóm UI đảm nhận,
    /// lớp này chỉ hiển thị số liệu thô (máu, năng lượng, hồi chiêu, vị trí, phe kết nối)
    /// để phát hiện lệch trạng thái giữa host và client.
    /// </summary>
    public class NetDebugHud : MonoBehaviour
    {
        [Header("Điều khiển")]
        [Tooltip("Phím bật/tắt bảng debug.")]
        [SerializeField] private Key toggleKey = Key.F3;

        [Tooltip("Bắt đầu hiện bảng ngay khi vào Play.")]
        [SerializeField] private bool visibleOnStart = true;

        [Header("Tần suất")]
        [Tooltip("Số giây giữa hai lần cập nhật số liệu (giây).")]
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;

        [Tooltip("Hiện danh sách toàn bộ người chơi đã spawn.")]
        [SerializeField] private bool showAllPlayers = true;

        private readonly StringBuilder m_Builder = new();
        private readonly List<PlayerNetState> m_Players = new();

        private bool m_Visible;
        private float m_RefreshTimer;
        private string[] m_Lines = Array.Empty<string>();
        private GUIStyle m_Style;
        private GUIStyle m_BannerStyle;
        private Texture2D m_Background;
        private NetBootstrap m_Bootstrap;
        private MatchFlow m_Flow;

        private void Awake()
        {
            m_Visible = visibleOnStart;
            // HUD nằm cùng GameObject với NetworkManager/NetBootstrap nên lấy trực tiếp.
            m_Bootstrap = GetComponent<NetBootstrap>();
            m_Flow = GetComponent<MatchFlow>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
            {
                m_Visible = !m_Visible;
            }

            m_RefreshTimer += Time.unscaledDeltaTime;
            if (m_RefreshTimer < refreshInterval) return;

            m_RefreshTimer = 0f;
            m_Lines = BuildLines();
        }

        private void OnGUI()
        {
            if (!m_Visible || m_Lines.Length == 0) return;

            EnsureStyle();

            bool lost = m_Bootstrap != null && m_Bootstrap.ConnectionLost;
            bool ended = m_Bootstrap != null && m_Bootstrap.SessionEnded;
            NetStageEvents stageEvents = NetStageEvents.Instance;
            string banner = null;
            if (lost)
            {
                banner = "MẤT KẾT NỐI";
            }
            else if (ended)
            {
                banner = "PHIÊN ĐÃ KẾT THÚC";
            }
            else if (stageEvents != null && stageEvents.HasStageState &&
                     (stageEvents.StageState == NetStageEvents.StateCleared || stageEvents.StageState == NetStageEvents.StateFailed))
            {
                banner = stageEvents.StageState == NetStageEvents.StateCleared ? "THẮNG" : "THUA";
            }

            if (banner != null)
            {
                EnsureBannerStyle();
                m_BannerStyle.normal.textColor = (lost || ended)
                    ? new Color(1f, 0.35f, 0.3f, 0.95f)
                    : new Color(1f, 0.92f, 0.35f, 0.95f);
                GUI.Label(new Rect(0f, Screen.height * 0.32f, Screen.width, 90f), banner, m_BannerStyle);
            }

            const float lineHeight = 20f;
            const float padding = 10f;
            float height = m_Lines.Length * lineHeight + padding * 2f;

            // Bề rộng co theo dòng dài nhất để không bị cắt chữ (trước đây cố định 520 px làm
            // dòng thông tin người chơi bị mất phần đuôi trên client).
            float width = 520f;
            for (int i = 0; i < m_Lines.Length; i++)
            {
                float needed = m_Style.CalcSize(new GUIContent(m_Lines[i])).x + padding * 2f;
                if (needed > width) width = needed;
            }
            width = Mathf.Clamp(width, 320f, Mathf.Max(320f, Screen.width - 20f));

            // Đặt HUD ở góc phải để không chồng lên panel Lobby (góc trái).
            Rect box = new Rect(Mathf.Max(padding, Screen.width - width - padding), padding, width, height);
            GUI.DrawTexture(box, m_Background);
            GUI.Label(new Rect(box.x + padding, box.y + padding, width - padding * 2f, height), string.Join("\n", m_Lines), m_Style);
        }

        /// <summary>
        /// Sinh nội dung bảng debug. Tách khỏi <see cref="OnGUI"/> để có thể gọi và kiểm tra
        /// bằng công cụ (unity_eval) khi không nhìn thấy màn hình.
        /// </summary>
        public string[] BuildLines()
        {
            m_Builder.Clear();

            NetworkManager manager = NetworkManager.Singleton;
            m_Builder.Append("[F3] Mạng: ");
            if (manager == null || !manager.IsListening)
            {
                m_Builder.Append("OFFLINE");
            }
            else
            {
                m_Builder.Append(manager.IsHost ? "HOST" : "CLIENT");
                m_Builder.Append(" | clientId=").Append(manager.LocalClientId);
                m_Builder.Append(" | tick=").Append(manager.NetworkConfig.TickRate);

                // Trên client, ConnectedClientsIds chỉ phản ánh những gì NGO biết ở máy đó nên
                // thường chỉ có chính mình; lấy thêm số người từ snapshot của MatchFlow để HUD đúng.
                int playerCount = manager.ConnectedClientsIds.Count;
                if (m_Flow != null && m_Flow.PlayerCount > playerCount) playerCount = m_Flow.PlayerCount;
                m_Builder.Append(" | players=").Append(playerCount);
            }

            m_Builder.Append(" | quái=").Append(CountEnemies());
            m_Builder.Append(" | FPS=").Append(Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)));
            if (m_Bootstrap != null) m_Builder.Append(" | kết nối=").Append(m_Bootstrap.ConnectionLost ? "MẤT" : "OK");
            if (m_Bootstrap != null && m_Bootstrap.SessionEnded) m_Builder.Append(" | phiên=KẾT THÚC");

            NetStageEvents stageEvents = NetStageEvents.Instance;
            if (stageEvents != null && stageEvents.HasStageState)
            {
                m_Builder.Append(" | ải=").Append(StageLabel(stageEvents.StageState))
                    .Append(' ').Append(stageEvents.StageKills).Append('/').Append(stageEvents.StageKillTarget);
            }

            if (Time.time < NetPlayerController.LocalCastMessageUntil &&
                !string.IsNullOrEmpty(NetPlayerController.LocalCastMessage))
            {
                m_Builder.Append(" | cast=").Append(NetPlayerController.LocalCastMessage);
            }

            CollectPlayers();
            PlayerNetState local = FindLocalPlayer();

            if (local == null)
            {
                m_Builder.Append('\n').Append("Chưa có xe của mình trong phiên.");
            }
            else
            {
                AppendPlayer(m_Builder, "Bạn", local);
            }

            if (showAllPlayers)
            {
                for (int i = 0; i < m_Players.Count; i++)
                {
                    PlayerNetState player = m_Players[i];
                    if (player == null || player == local) continue;
                    AppendPlayer(m_Builder, "P" + (i + 1), player);
                }
            }

            return m_Builder.ToString().Split('\n');
        }

        private void CollectPlayers()
        {
            m_Players.Clear();
            // Khi xe chết, GameObject có thể bị tạm ẩn; vẫn liệt kê để HUD không "mất" người chơi.
            PlayerNetState[] found = FindObjectsByType<PlayerNetState>(FindObjectsInactive.Include);
            for (int i = 0; i < found.Length; i++)
            {
                m_Players.Add(found[i]);
            }
        }

        private PlayerNetState FindLocalPlayer()
        {
            for (int i = 0; i < m_Players.Count; i++)
            {
                PlayerNetState player = m_Players[i];
                if (player != null && player.IsSpawned && player.IsOwner) return player;
            }

            return null;
        }

        private void AppendPlayer(StringBuilder builder, string label, PlayerNetState player)
        {
            TankBrain brain = player.Brain;
            builder.Append('\n').Append(label).Append(": ");
            builder.Append(brain != null ? brain.GetType().Name : "?");

            builder.Append(" | HP ").Append(Mathf.Round(player.Health)).Append('/').Append(Mathf.Round(player.MaxHealth));
            builder.Append(" | EN ").Append(Mathf.Round(player.Energy)).Append('/').Append(Mathf.Round(player.MaxEnergy));

            builder.Append(" | CD");
            for (int i = 0; i < TankBrain.SkillCount; i++)
            {
                builder.Append(' ').Append(player.GetRemainingCooldown(i).ToString("0.0"));
            }

            if (brain != null)
            {
                Vector3 position = brain.transform.position;
                builder.Append(" | pos ").Append(position.x.ToString("0.0")).Append(',').Append(position.y.ToString("0.0"));
                builder.Append(" | alive=").Append(brain.IsAlive);
                builder.Append(" | server=").Append(brain.IsServerSimulated);
                builder.Append(" | local=").Append(brain.IsLocallyControlled);
            }

            builder.Append(" | owner=").Append(player.IsOwner);
            builder.Append(" | loadout=").Append(player.LoadoutApplied);
            builder.Append(" | khiên ").Append(Mathf.Round(player.ShieldAmount));

            NetEffectSync effects = NetEffectSync.Instance;
            if (effects != null && player.IsSpawned)
            {
                IReadOnlyCollection<NetEffectInfo> list = effects.GetEffects(player.OwnerClientId);
                if (list.Count > 0)
                {
                    builder.Append(" | ef");
                    foreach (NetEffectInfo info in list)
                    {
                        builder.Append(' ').Append(info.EffectId);
                        if (info.Stacks > 1) builder.Append('x').Append(info.Stacks);
                    }
                }
            }
        }

        private int CountEnemies()
        {
            return FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude).Length;
        }

        private void EnsureStyle()
        {
            if (m_Style != null) return;

            m_Background = new Texture2D(1, 1);
            m_Background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            m_Background.Apply();

            m_Style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = false,
                wordWrap = false,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(0, 0, 0, 0),
            };
            m_Style.normal.textColor = Color.white;
        }

        private static string StageLabel(byte state)
        {
            return state switch
            {
                NetStageEvents.StateCleared => "THẮNG",
                NetStageEvents.StateFailed => "THUA",
                _ => "ĐANG CHƠI"
            };
        }

        private void EnsureBannerStyle()
        {
            if (m_BannerStyle != null) return;

            m_BannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 64,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = false,
            };
            m_BannerStyle.normal.textColor = new Color(1f, 0.92f, 0.35f, 0.95f);
        }

        private void OnDestroy()
        {
            if (m_Background != null)
            {
                Destroy(m_Background);
            }
        }
    }
}
