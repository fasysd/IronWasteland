using System;
using IronWasteland.Enemies;

namespace IronWasteland.Stages
{
    /// <summary>Loai dieu kien thang cua man.</summary>
    public enum StageObjectiveType
    {
        /// <summary>Tieu diet du so quai.</summary>
        Eliminate = 0,

        /// <summary>Sinh ton du thoi gian.</summary>
        Survive = 1,

        /// <summary>Pha huy du so vat the (Cum Nam Me, Tru Dien Truong...).</summary>
        DestroyTargets = 2,

        /// <summary>Ho tong xe di du quang duong.</summary>
        Escort = 3,

        /// <summary>Kich hoat du so tram radar.</summary>
        Radar = 4,
    }

    /// <summary>
    /// Mot dieu kien thang cua man. Director tao ra tu du lieu cua
    /// <see cref="StageDefinition"/> roi goi <see cref="Tick"/> moi frame va
    /// cac ham On... khi co su kien.
    ///
    /// Lop nay KHONG phai MonoBehaviour: no chi giu so lieu, khong query scene.
    /// Nho vay test duoc bang cach goi tay cac ham On... nhu trong Unity Test.
    /// </summary>
    public abstract class StageObjective
    {
        /// <summary>Chuoi hien thi tren HUD, vi du "Quai 12/40".</summary>
        public abstract string Label { get; }

        /// <summary>Da thoa dieu kien thang chua.</summary>
        public abstract bool IsComplete { get; }

        /// <summary>Da thoa dieu kien thua chua.</summary>
        public virtual bool IsFailed => false;

        /// <summary>Ly do thua (null neu khong thua).</summary>
        public virtual string FailReason => null;

        /// <summary>Tien do 0..1 de ve thanh tien do tren HUD.</summary>
        public abstract float Progress01 { get; }

        /// <summary>Director goi moi frame.</summary>
        public abstract void Tick(float deltaTime);

        /// <summary>Mot quai bi tieu diet (chi tinh quai thuong, khong tinh vat the muc tieu).</summary>
        public virtual void OnEnemyKilled(EnemyBrain enemy) { }

        /// <summary>Mot vat the co id tuong ung bi pha huy.</summary>
        public virtual void OnTargetDestroyed(string targetId) { }

        /// <summary>Mot tram radar duoc kich hoat.</summary>
        public virtual void OnRadarActivated(string radarId) { }

        /// <summary>Xe ho tong bi pha huy.</summary>
        public virtual void OnEscortDestroyed(string escortId) { }

        /// <summary>Xe ho tong bao tien do moi (met da di).</summary>
        public virtual void OnEscortProgress(string escortId, float distance) { }
    }

    /// <summary>Tieu diet du so quai (man 1-1, 1-2, 2-1...).</summary>
    public sealed class EliminateObjective : StageObjective
    {
        private readonly int m_Target;
        private int m_Kills;

        public EliminateObjective(int target) => m_Target = Math.Max(1, target);

        public override string Label => $"Quái {m_Kills}/{m_Target}";
        public override bool IsComplete => m_Kills >= m_Target;
        public override float Progress01 => (float)m_Kills / m_Target;

        public override void Tick(float deltaTime) { }

        public override void OnEnemyKilled(EnemyBrain enemy) => m_Kills++;
    }

    /// <summary>Sinh ton du thoi gian (man 1-2, 2-2).</summary>
    public sealed class SurviveObjective : StageObjective
    {
        private readonly float m_Duration;
        private float m_Elapsed;

        public SurviveObjective(float duration) => m_Duration = Math.Max(0.01f, duration);

        public override string Label => $"Sống sót {m_Elapsed:0.0}/{m_Duration:0.0}s";
        public override bool IsComplete => m_Elapsed >= m_Duration;
        public override float Progress01 => m_Elapsed / m_Duration;

        public override void Tick(float deltaTime) => m_Elapsed += deltaTime;
    }

    /// <summary>Pha huy du so vat the co cung id (Cum Nam Me, Tru Dien Truong).</summary>
    public sealed class DestroyTargetsObjective : StageObjective
    {
        private readonly string m_TargetId;
        private readonly int m_Required;
        private int m_Destroyed;

        public DestroyTargetsObjective(string targetId, int required)
        {
            m_TargetId = string.IsNullOrWhiteSpace(targetId) ? "objective" : targetId;
            m_Required = Math.Max(1, required);
        }

        public override string Label => $"Mục tiêu {m_Destroyed}/{m_Required} ({m_TargetId})";
        public override bool IsComplete => m_Destroyed >= m_Required;
        public override float Progress01 => (float)m_Destroyed / m_Required;

        public override void Tick(float deltaTime) { }

        public override void OnTargetDestroyed(string targetId)
        {
            if (targetId != null && targetId == m_TargetId) m_Destroyed++;
        }
    }

    /// <summary>Ho tong xe di du quang duong; xe bi pha huy thi thua ngay.</summary>
    public sealed class EscortObjective : StageObjective
    {
        private readonly string m_EscortId;
        private readonly float m_Distance;
        private float m_Travelled;
        private bool m_Failed;

        public EscortObjective(string escortId, float distance)
        {
            m_EscortId = string.IsNullOrWhiteSpace(escortId) ? "escort" : escortId;
            m_Distance = Math.Max(1f, distance);
        }

        public override string Label => $"Hộ tống {Math.Min(m_Travelled, m_Distance):0}m/{m_Distance:0}m";
        public override bool IsComplete => !m_Failed && m_Travelled >= m_Distance;
        public override bool IsFailed => m_Failed;
        public override string FailReason => m_Failed ? "Xe hộ tống bị phá huỷ" : null;
        public override float Progress01 => Math.Min(1f, m_Travelled / m_Distance);

        public override void Tick(float deltaTime) { }

        public override void OnEscortProgress(string escortId, float distance)
        {
            if (escortId != null && escortId == m_EscortId) m_Travelled = Math.Max(0f, distance);
        }

        public override void OnEscortDestroyed(string escortId)
        {
            if (escortId != null && escortId == m_EscortId) m_Failed = true;
        }
    }

    /// <summary>Kich hoat du so tram radar (man 1-3).</summary>
    public sealed class RadarObjective : StageObjective
    {
        private readonly int m_Required;
        private int m_Activated;

        public RadarObjective(int required) => m_Required = Math.Max(1, required);

        public override string Label => $"Radar {m_Activated}/{m_Required}";
        public override bool IsComplete => m_Activated >= m_Required;
        public override float Progress01 => (float)m_Activated / m_Required;

        public override void Tick(float deltaTime) { }

        public override void OnRadarActivated(string radarId) => m_Activated++;
    }
}
