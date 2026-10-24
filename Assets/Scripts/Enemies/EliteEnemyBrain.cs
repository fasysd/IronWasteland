using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Trang thai cua 1 Mang Nang Luong tren ke dich Tinh anh.
    /// </summary>
    public enum EnergyCoreState
    {
        /// <summary>Mac dinh - chua mo.</summary>
        Closed,

        /// <summary>Dang mo - nguoi choi can pha trong thoi gian cho phep.</summary>
        Open,

        /// <summary>Da pha thanh cong.</summary>
        Broken,
    }

    /// <summary>
    /// 1 Mang Nang Luong tren ke dich Tinh anh (chi co khung).
    /// </summary>
    [Serializable]
    public class EnergyCore
    {
        [Tooltip("Trang thai hien tai cua mang nang luong.")]
        public EnergyCoreState state = EnergyCoreState.Closed;

        public void Reset() => state = EnergyCoreState.Closed;
    }

    /// <summary>
    /// Ke dich TINH ANH - chi so lon, kho bi tieu diet, co co che Mang Nang Luong.
    ///
    /// Mang Nang Luong (chi xuat hien tren ke dich Tinh anh):
    /// - Moi ke dich co 1 so luong mang nhat dinh, mac dinh DONG,
    ///   MO khi vao giai doan nhat dinh cua ke dich (<see cref="OpenCores"/>).
    /// - Khi mo, nguoi choi can pha toan bo mang trong thoi gian cho phep
    ///   (<see cref="breakWindowDuration"/>):
    ///   + Pha het -> ke dich giam phong thuu / cung cap hieu ung co loi cho nguoi choi.
    ///   + Het gio khong pha het -> ke dich thieu trien ky nang
    ///     lam nguoi choi bi suy yeu (<see cref="OnBreakWindowExpired"/>).
    /// - Coop: tung thanh vien cung goi <see cref="BreakCore(int)"/> -> pha nhanh hon.
    ///
    /// CHI CO KHUNG - chua trien khai logic that. Day la lop ABSTRACT (framework) -
    /// dung lop con cu the (vi du EliteEnemyBrain_Test) de truc tiep dung.
    /// </summary>
    [UnityEngine.DisallowMultipleComponent]
    public abstract class EliteEnemyBrain : EnemyBrain
    {
        [Header("Energy Core")]
        [Tooltip("So luong Mang Nang Luong cua ke dich Tinh anh.")]
        [SerializeField, Min(1)] private int energyCoreCount = 3;

        [Tooltip("Thoi gian cho phep pha mang sau khi mo (giay). Het gio ma khong pha het -> ky nang suy yeu.")]
        [SerializeField, Min(0f)] private float breakWindowDuration = 10f;

        [Tooltip("Muc giam Phong thu (Defense) khi pha het toan bo mang. 0.5 = giam 50% Defense.")]
        [SerializeField, Range(0f, 1f)] private float defenseBreakBonus = 0.5f;

        /// <summary>Hoi chieu ky nang suy yeu (giay) - hardcode, Elite tu quan ly.</summary>
        private const float WeakenCooldown = 5f;

        // --- Runtime ---
        private readonly List<EnergyCore> m_Cores = new List<EnergyCore>();
        private float m_BreakWindowRemaining;
        private bool m_BreakWindowOpen;
        private float m_WeakenCooldown;

        /// <summary>Danh sach mang nang luong (runtime).</summary>
        public IReadOnlyList<EnergyCore> Cores => m_Cores;

        /// <summary>So mang da pha thanh cong.</summary>
        public int BrokenCoreCount { get; private set; }

        public bool AllCoresBroken => energyCoreCount > 0 && BrokenCoreCount >= energyCoreCount;

        /// <summary>Thoi gian pha con lai (0 = khong con cua so pha).</summary>
        public float BreakWindowRemaining => m_BreakWindowRemaining;

        public bool IsBreakWindowOpen => m_BreakWindowOpen;

        public int EnergyCoreCount => energyCoreCount;
        public float BreakWindowDuration => breakWindowDuration;
        public float DefenseBreakBonus => defenseBreakBonus;

        #region Energy Core

        /// <summary>
        /// Mo toan bo Mang Nang Luong - goi khi ke dich vao giai doan can pha mach.
        /// Chi goi TRONG lop nay (tu logic noi bo), khong cho phep ben ngoai kich hoat.
        /// </summary>
        protected virtual void OpenCores()
        {
            EnsureCores();

            for (int i = 0; i < m_Cores.Count; i++)
            {
                if (m_Cores[i].state == EnergyCoreState.Closed)
                {
                    m_Cores[i].state = EnergyCoreState.Open;
                }
            }

            m_BreakWindowOpen = true;
            m_BreakWindowRemaining = breakWindowDuration;

            // TODO: bao EnemyModel hien thi vi tri cac mang (sprite / hieuung).
        }

        /// <summary>
        /// Pha 1 mang. Se duoc goi NHIEU LAN khi nhieu player cung pha (coop pha nhanh hon) -
        /// cac lan goi den tu su kien ma EnemyModel chuyen len. Chi goi TRONG lop nay.
        /// Tra ve true neu mang bi pha thanh cong o lan goi nay.
        /// </summary>
        protected virtual bool BreakCore(int index)
        {
            EnsureCores();
            if (index < 0 || index >= m_Cores.Count) return false;

            EnergyCore core = m_Cores[index];
            if (core.state != EnergyCoreState.Open) return false;

            core.state = EnergyCoreState.Broken;
            BrokenCoreCount++;

            // TODO: hieuung pha mang tren EnemyModel + tich luy sat thuong pha mach.

            if (AllCoresBroken)
            {
                OnAllCoresBroken();
            }

            return true;
        }

        /// <summary>
        /// Pha het toan bo mang -> giam phong thuu / cung cap hieuung co loi cho nguoi choi.
        /// </summary>
        protected virtual void OnAllCoresBroken()
        {
            m_BreakWindowOpen = false;
            m_BreakWindowRemaining = 0f;

            // TODO: ap dung giam Defense (defenseBreakBonus) + hieuung co loi cho nguoi choi
            // (hoi mau, tang sat thuong...). Chua co he thong buff nen de trong tam thoi.
        }

        /// <summary>
        /// Het thoi gian pha ma khong pha het -> thieu trien ky nang
        /// lam nguoi choi bi suy yeu.
        /// </summary>
        protected virtual void OnBreakWindowExpired()
        {
            // Thieu trien ky nang suy yeu - CD do Elite tu quan ly.
            TryCastWeakenSkill();
        }

        /// <summary>Kiem tra hoi chieu roi thieu trien ky nang suy yeu. False = dang hoi chieu.</summary>
        private bool TryCastWeakenSkill()
        {
            if (m_WeakenCooldown > 0f) return false;

            CastWeakenSkill();
            m_WeakenCooldown = WeakenCooldown;
            return true;
        }

        /// <summary>Hieuung ky nang suy yeu - hardcode, rieng voi ke dich Tinh anh.</summary>
        protected virtual void CastWeakenSkill()
        {
            // TODO: ap dung debuff suy yeu len Tank / nguoi choi
            // (giam toc do, giam cong...) - chua co he thong debuff.
            Debug.Log($"[{nameof(EliteEnemyBrain)}] Thieu trien ky nang suy yeu!", this);
        }

        private void EnsureCores()
        {
            if (m_Cores.Count == energyCoreCount) return;

            m_Cores.Clear();
            for (int i = 0; i < energyCoreCount; i++)
            {
                m_Cores.Add(new EnergyCore());
            }

            BrokenCoreCount = 0;
        }

        #endregion

        #region EnemyBrain

        protected override void OnBrainTick(float deltaTime)
        {
            // Hoi chieu ky nang suy yeu (Elite tu quan ly).
            if (m_WeakenCooldown > 0f)
            {
                m_WeakenCooldown = Mathf.Max(0f, m_WeakenCooldown - deltaTime);
            }

            // Dong ho cua so pha mang.
            if (!m_BreakWindowOpen) return;

            m_BreakWindowRemaining = Mathf.Max(0f, m_BreakWindowRemaining - deltaTime);
            if (m_BreakWindowRemaining <= 0f)
            {
                m_BreakWindowOpen = false;

                if (!AllCoresBroken)
                {
                    OnBreakWindowExpired();
                }
            }
        }


        #endregion

        protected override void OnValidate()
        {
            base.OnValidate();

            energyCoreCount = Mathf.Max(1, energyCoreCount);
            breakWindowDuration = Mathf.Max(0f, breakWindowDuration);
        }
    }
}