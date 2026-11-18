using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Audio
{
    /// <summary>
    /// Bang am thanh dung chung: moi nhom co nhieu clip, cung 1 am luong va dai pitch.
    ///
    /// Vi sao gom vao 1 asset: doi am thanh chi can thay clip trong Inspector,
    /// khong phai sua code o tung prefab. Ca game dung chung 1 bang.
    ///
    /// Tao asset: chuot phai trong Project > Create > Iron Wasteland > Audio > Sfx Library.
    /// </summary>
    [CreateAssetMenu(fileName = "SfxLibrary", menuName = "Iron Wasteland/Audio/Sfx Library")]
    public class SfxLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SfxCategory Category = SfxCategory.SkillCast;
            public AudioClip[] Clips = new AudioClip[0];

            [Tooltip("Am luong tuong doi cua nhom (1 = theo AudioManager).")]
            [Range(0f, 2f)] public float Volume = 1f;

            [Tooltip("Dao pitch moi lan phat de do lap. Vi du 0.95 - 1.05.")]
            public Vector2 PitchRange = new Vector2(0.95f, 1.05f);
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>Tim nhom am thanh. Tra ve null neu chua khai bao.</summary>
        public Entry Get(SfxCategory category)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry != null && entry.Category == category) return entry;
            }
            return null;
        }

        /// <summary>
        /// Chon ngau nhien 1 clip trong nhom, tranh lap lai clip vua phat.
        /// Tra ve chi so clip, hoac -1 neu nhom rong.
        /// </summary>
        public int PickIndex(SfxCategory category, int avoidIndex)
        {
            Entry entry = Get(category);
            if (entry == null || entry.Clips == null) return -1;

            int count = 0;
            for (int i = 0; i < entry.Clips.Length; i++)
                if (entry.Clips[i] != null) count++;

            if (count == 0) return -1;
            if (count == 1)
            {
                for (int i = 0; i < entry.Clips.Length; i++)
                    if (entry.Clips[i] != null) return i;
                return -1;
            }

            int picked = -1;
            // Thu toi da 8 lan de khong bi treo neu tat ca deu trung avoidIndex.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int candidate = UnityEngine.Random.Range(0, entry.Clips.Length);
                if (entry.Clips[candidate] == null) continue;
                if (candidate == avoidIndex) continue;
                picked = candidate;
                break;
            }

            if (picked < 0)
            {
                for (int i = 0; i < entry.Clips.Length; i++)
                {
                    if (i == avoidIndex) continue;
                    if (entry.Clips[i] == null) continue;
                    picked = i;
                    break;
                }
            }

            return picked;
        }

        /// <summary>So clip hien co cua 1 nhom (dung de kiem tra luc import).</summary>
        public int ClipCount(SfxCategory category)
        {
            Entry entry = Get(category);
            if (entry == null || entry.Clips == null) return 0;
            int n = 0;
            for (int i = 0; i < entry.Clips.Length; i++) if (entry.Clips[i] != null) n++;
            return n;
        }
    }
}
