using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Audio
{
    /// <summary>
    /// Phat am thanh dung chung cho ca game.
    ///
    /// Vi sao can 1 manager thay vi moi object tu tao AudioSource:
    /// - Chi co 1 noi chinh am luong tong.
    /// - Gioi han so tieng phat cung luc (tranh 40 con quai cung keu 1 luc).
    /// - Tu chon bien the clip khac nhau de khong bi lap am.
    ///
    /// Cach dung trong scene: tao GameObject "AudioManager", gan component nay,
    /// keo asset SfxLibrary vao. Goi tu code: AudioManager.Play(SfxCategory.EnemyHit);
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        private const string SfxLogTag = "[AudioManager]";

        public static AudioManager Instance { get; private set; }

        [Header("Du lieu")]
        [SerializeField] private SfxLibrary library;
        [Tooltip("Am luong tong cua SFX (0 - 1).")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.8f;

        [Tooltip("Am luong rieng cho hieu ung (nhan them vao Master).")]
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        [Header("Giong phat")]
        [Tooltip("So tieng phat cung luc. 12 du cho man co ~20 quai.")]
        [SerializeField, Min(1)] private int voiceCount = 12;

        [Tooltip("Giu AudioManager khi chuyen scene.")]
        [SerializeField] private bool persistBetweenScenes = true;

        private AudioSource[] m_Voices;
        private int m_NextVoice;
        private readonly Dictionary<SfxCategory, int> m_LastClip = new Dictionary<SfxCategory, int>();
        private bool m_WarnedMissingLibrary;

        public SfxLibrary Library => library;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistBetweenScenes) DontDestroyOnLoad(gameObject);

            BuildVoices();
            if (library == null) library = FindLibraryFromResources();
        }

        private void BuildVoices()
        {
            m_Voices = new AudioSource[Mathf.Max(1, voiceCount)];
            for (int i = 0; i < m_Voices.Length; i++)
            {
                var go = new GameObject($"SfxVoice_{i}");
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f; // 2D: am thanh deu 2 tai, hop game top-down
                source.volume = 1f;
                m_Voices[i] = source;
            }
        }

        /// <summary>Tim SfxLibrary trong Resources neu quen gan (tuy chon).</summary>
        private static SfxLibrary FindLibraryFromResources()
        {
            return Resources.Load<SfxLibrary>("SfxLibrary");
        }

        /// <summary>Phat 1 am thanh. An toan khi chua co manager (chi log canh bao 1 lan).</summary>
        public static void Play(SfxCategory category, float volumeScale = 1f)
        {
            if (Instance == null) return;
            Instance.PlayInternal(category, volumeScale);
        }

        public static void Play(SfxCategory category, Vector3 position, float volumeScale = 1f)
        {
            // Game 2D top-down nen chua dung vi tri; giu tham so de sau nay doi sang 3D.
            Play(category, volumeScale);
        }

        /// <summary>Am luong tong (0..1).</summary>
        public float MasterVolume
        {
            get => masterVolume;
            set => masterVolume = Mathf.Clamp01(value);
        }

        /// <summary>Am luong hieu ung (0..1).</summary>
        public float SfxVolume
        {
            get => sfxVolume;
            set => sfxVolume = Mathf.Clamp01(value);
        }

        private void PlayInternal(SfxCategory category, float volumeScale)
        {
            if (library == null)
            {
                if (!m_WarnedMissingLibrary)
                {
                    m_WarnedMissingLibrary = true;
                    Debug.LogWarning($"{SfxLogTag} Chua gan SfxLibrary, SFX se khong keu.", this);
                }
                return;
            }

            SfxLibrary.Entry entry = library.Get(category);
            if (entry == null)
            {
                return;
            }

            m_LastClip.TryGetValue(category, out int avoid);
            int index = library.PickIndex(category, avoid);
            if (index < 0) return;

            m_LastClip[category] = index;
            AudioClip clip = entry.Clips[index];
            if (clip == null) return;

            AudioSource voice = NextVoice();
            if (voice == null) return;

            voice.pitch = Random.Range(entry.PitchRange.x, entry.PitchRange.y);
            float volume = Mathf.Clamp01(masterVolume * sfxVolume * entry.Volume * Mathf.Max(0f, volumeScale));
            voice.PlayOneShot(clip, volume);
        }

        private AudioSource NextVoice()
        {
            if (m_Voices == null || m_Voices.Length == 0) return null;
            AudioSource source = m_Voices[m_NextVoice];
            m_NextVoice = (m_NextVoice + 1) % m_Voices.Length;
            return source;
        }
    }
}
