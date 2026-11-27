using UnityEngine;
using IronWasteland.Stages;

namespace IronWasteland.Audio
{
    /// <summary>
    /// Gan len object StageDirector: phat tieng khi dot quai bat dau, khi thang, khi thua.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StageDirector))]
    public class StageSfxHooks : MonoBehaviour
    {
        [SerializeField] private StageDirector director;

        [SerializeField] private bool playWaveStart = true;
        [SerializeField] private bool playClear = true;
        [SerializeField] private bool playFail = true;

        private void Awake()
        {
            if (director == null) director = GetComponent<StageDirector>();
        }

        private void OnEnable()
        {
            if (director == null) return;
            director.WaveStarted += HandleWaveStarted;
            director.StageCleared += HandleStageCleared;
            director.StageFailed += HandleStageFailed;
        }

        private void OnDisable()
        {
            if (director == null) return;
            director.WaveStarted -= HandleWaveStarted;
            director.StageCleared -= HandleStageCleared;
            director.StageFailed -= HandleStageFailed;
        }

        private void HandleWaveStarted(int index, EnemyWave wave)
        {
            if (!playWaveStart) return;
            AudioManager.Play(SfxCategory.EnemySpawn, 0.8f);
        }

        private void HandleStageCleared(StageDirector d, StageRewardResult reward)
        {
            if (!playClear) return;
            AudioManager.Play(SfxCategory.StageClear);
        }

        private void HandleStageFailed(StageDirector d, string reason)
        {
            if (!playFail) return;
            AudioManager.Play(SfxCategory.StageFailed);
        }
    }
}
