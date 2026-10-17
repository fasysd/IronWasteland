using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IronWasteland.Editor
{
    [InitializeOnLoad]
    public static class EnemyAnimationGenerator
    {
        private const string SeaubearusDir = "Assets/Art/Enemies/SEAUBEARUS/Animations";
        private const string TuThanDir = "Assets/Art/Enemies/TuThanNguyenTo/Animations";

        private const string SeaubearusPrefabPath = "Assets/Prefabs/Enemies/SEAUBEARUS.prefab";
        private const string TuThanPrefabPath = "Assets/Prefabs/Enemies/TuThanNguyenTo.prefab";

        static EnemyAnimationGenerator()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool("EnemyAnimGenerated", false))
                {
                    SessionState.SetBool("EnemyAnimGenerated", true);
                    GenerateAll();
                }
            };
        }

        [MenuItem("IronWasteland/Generate Enemy Animations")]
        public static void GenerateAll()
        {
            Debug.Log("[EnemyAnimationGenerator] Starting generation of animations and controllers...");

            EnsureDirectory(SeaubearusDir);
            EnsureDirectory(TuThanDir);

            // 1. Generate SEAUBEARUS Animations
            AnimationClip seaIdle = GenerateSeaubearusIdle();
            AnimationClip seaMove = GenerateSeaubearusMove();
            AnimatorController seaController = CreateOrUpdateController(
                $"{SeaubearusDir}/SEAUBEARUS.controller",
                seaIdle,
                seaMove
            );
            AttachControllerToPrefab(SeaubearusPrefabPath, seaController);

            // 2. Generate TuThanNguyenTo Animations
            AnimationClip tuThanIdle = GenerateTuThanNguyenToIdle();
            AnimationClip tuThanMove = GenerateTuThanNguyenToMove();
            AnimatorController tuThanController = CreateOrUpdateController(
                $"{TuThanDir}/TuThanNguyenTo.controller",
                tuThanIdle,
                tuThanMove
            );
            AttachControllerToPrefab(TuThanPrefabPath, tuThanController);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[EnemyAnimationGenerator] Successfully generated all enemy animations with milestone keyframes!");
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace("\\", "/");
                string folderName = Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureDirectory(parent);
                }
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        #region SEAUBEARUS Animations

        private static AnimationClip GenerateSeaubearusIdle()
        {
            string clipPath = $"{SeaubearusDir}/SEAUBEARUS_Idle.anim";
            AnimationClip clip = new AnimationClip { frameRate = 60f };
            float duration = 2.0f;

            float[] breathTimes = { 0f, 0.5f, 1.0f, 1.5f, 2.0f };
            float[] scaleTimes = { 0f, 1.0f, 2.0f };

            // Thân: breathing scale
            AddScaleCurve(clip, "Thân", 0.6666667f, 0.6666667f, 1f, duration, scaleTimes, (t, baseV) =>
            {
                float breath = Mathf.Sin(t / duration * Mathf.PI * 2f);
                return new Vector3(baseV.x * (1f + 0.035f * breath), baseV.y * (1f + 0.045f * breath), 1f);
            });

            // Heads: gentle bobbing & tilt
            AddEulerCurveZ(clip, "Đầu giữa", duration, breathTimes, t =>
            {
                return Mathf.Sin(t / duration * Mathf.PI * 2f) * 2f;
            });
            AddEulerCurveZ(clip, "Đầu trái", duration, breathTimes, t =>
            {
                return Mathf.Sin((t / duration * Mathf.PI * 2f) + 0.5f) * 3.5f;
            });
            AddEulerCurveZ(clip, "Đầu phải", duration, breathTimes, t =>
            {
                return -Mathf.Sin((t / duration * Mathf.PI * 2f) + 0.5f) * 3.5f;
            });

            // Arms sway gently with breathing
            AddEulerCurveZ(clip, "Tay trái", duration, breathTimes, t =>
            {
                return 4.1f + Mathf.Sin(t / duration * Mathf.PI * 2f) * 8f;
            });
            AddEulerCurveZ(clip, "Tay phải", duration, breathTimes, t =>
            {
                return 4.1f - Mathf.Sin(t / duration * Mathf.PI * 2f) * 8f;
            });

            // Legs: slight breathing stability
            AddScaleCurve(clip, "Chân trái", 0.6666667f, 0.6666667f, 1f, duration, scaleTimes, (t, baseV) =>
            {
                float breath = Mathf.Sin(t / duration * Mathf.PI * 2f);
                return new Vector3(baseV.x, baseV.y * (1f + 0.015f * breath), 1f);
            });
            AddScaleCurve(clip, "Chân phải", 0.6666667f, 0.6666667f, 1f, duration, scaleTimes, (t, baseV) =>
            {
                float breath = Mathf.Sin(t / duration * Mathf.PI * 2f);
                return new Vector3(baseV.x, baseV.y * (1f + 0.015f * breath), 1f);
            });

            SetLoop(clip, true);
            SaveOrCreateClip(clip, clipPath);
            return clip;
        }

        private static AnimationClip GenerateSeaubearusMove()
        {
            string clipPath = $"{SeaubearusDir}/SEAUBEARUS_Move.anim";
            AnimationClip clip = new AnimationClip { frameRate = 60f };
            float duration = 0.8f;

            float[] moveTimes = { 0f, 0.2f, 0.4f, 0.6f, 0.8f };
            float[] leftStepTimes = { 0f, 0.2f, 0.4f, 0.6f, 0.8f };
            float[] rightStepTimes = { 0f, 0.2f, 0.4f, 0.6f, 0.8f };

            // Thân: rhythm bobbing
            AddScaleCurve(clip, "Thân", 0.6666667f, 0.6666667f, 1f, duration, moveTimes, (t, baseV) =>
            {
                float stepCycle = Mathf.Sin(t / duration * Mathf.PI * 4f);
                return new Vector3(baseV.x * (1f + 0.04f * stepCycle), baseV.y * (1f - 0.04f * stepCycle), 1f);
            });
            AddEulerCurveZ(clip, "Thân", duration, moveTimes, t =>
            {
                return Mathf.Sin(t / duration * Mathf.PI * 2f) * 3f;
            });

            // Heads
            AddEulerCurveZ(clip, "Đầu giữa", duration, moveTimes, t =>
            {
                return Mathf.Sin(t / duration * Mathf.PI * 4f) * 4f;
            });
            AddEulerCurveZ(clip, "Đầu trái", duration, moveTimes, t =>
            {
                return Mathf.Sin(t / duration * Mathf.PI * 2f) * 6f;
            });
            AddEulerCurveZ(clip, "Đầu phải", duration, moveTimes, t =>
            {
                return -Mathf.Sin(t / duration * Mathf.PI * 2f) * 6f;
            });

            // Arms
            AddEulerCurveZ(clip, "Tay trái", duration, moveTimes, t =>
            {
                return 4.1f + Mathf.Sin(t / duration * Mathf.PI * 2f) * 25f;
            });
            AddEulerCurveZ(clip, "Tay phải", duration, moveTimes, t =>
            {
                return 4.1f - Mathf.Sin(t / duration * Mathf.PI * 2f) * 25f;
            });

            // Legs
            AddPositionCurve(clip, "Chân trái", -0.225f, -0.79f, 0f, duration, leftStepTimes, (t, basePos) =>
            {
                float lift = 0f;
                if (t < 0.4f)
                {
                    float p = t / 0.4f;
                    lift = Mathf.Sin(p * Mathf.PI) * 0.16f;
                }
                return new Vector3(basePos.x, basePos.y + lift, basePos.z);
            });
            AddEulerCurveZ(clip, "Chân trái", duration, moveTimes, t =>
            {
                return 3.63f + Mathf.Sin(t / duration * Mathf.PI * 2f) * 10f;
            });

            AddPositionCurve(clip, "Chân phải", 0.225f, -0.79f, 0f, duration, rightStepTimes, (t, basePos) =>
            {
                float lift = 0f;
                if (t >= 0.4f)
                {
                    float p = (t - 0.4f) / 0.4f;
                    lift = Mathf.Sin(p * Mathf.PI) * 0.16f;
                }
                return new Vector3(basePos.x, basePos.y + lift, basePos.z);
            });
            AddEulerCurveZ(clip, "Chân phải", duration, moveTimes, t =>
            {
                return 3.63f - Mathf.Sin(t / duration * Mathf.PI * 2f) * 10f;
            });

            // Feet
            AddPositionCurve(clip, "Chân trái/Bàn chân trái", -0.192f, -1.645f, 0f, duration, leftStepTimes, (t, basePos) =>
            {
                float lift = 0f;
                if (t < 0.4f)
                {
                    float p = t / 0.4f;
                    lift = Mathf.Sin(p * Mathf.PI) * 0.12f;
                }
                return new Vector3(basePos.x, basePos.y + lift, basePos.z);
            });
            AddPositionCurve(clip, "Chân phải/Bàn chân phải", 0.192f, -1.645f, 0f, duration, rightStepTimes, (t, basePos) =>
            {
                float lift = 0f;
                if (t >= 0.4f)
                {
                    float p = (t - 0.4f) / 0.4f;
                    lift = Mathf.Sin(p * Mathf.PI) * 0.12f;
                }
                return new Vector3(basePos.x, basePos.y + lift, basePos.z);
            });

            SetLoop(clip, true);
            SaveOrCreateClip(clip, clipPath);
            return clip;
        }

        #endregion

        #region TuThanNguyenTo Animations

        private static AnimationClip GenerateTuThanNguyenToIdle()
        {
            string clipPath = $"{TuThanDir}/TuThanNguyenTo_Idle.anim";
            AnimationClip clip = new AnimationClip { frameRate = 60f };
            float duration = 16.0f;

            // Breathing milestones: peaks & troughs of 2.0s breath cycle
            float[] breathTimes = {
                0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 13f, 14f, 15f, 16f
            };

            // Thân: breathing scale
            AddScaleCurve(clip, "Thân", 1f, 1f, 1f, duration, breathTimes, (t, baseV) =>
            {
                float breath = Mathf.Sin(t * Mathf.PI);
                return new Vector3(baseV.x * (1f + 0.02f * breath), baseV.y * (1f + 0.025f * breath), 1f);
            });

            // 2 Pincers (Càng 1, Càng 2) under Thân
            AddEulerCurveZ(clip, "Thân/Càng 1", duration, breathTimes, t =>
            {
                return -18.45f + Mathf.Sin(t * Mathf.PI) * 10f;
            });
            AddScaleCurve(clip, "Thân/Càng 1", 0.6666667f, 0.6666667f, 1f, duration, breathTimes, (t, baseV) =>
            {
                float p = Mathf.Sin(t * Mathf.PI);
                return new Vector3(baseV.x * (1f + 0.03f * p), baseV.y * (1f + 0.03f * p), 1f);
            });

            AddEulerCurveZ(clip, "Thân/Càng 2", duration, breathTimes, t =>
            {
                return Mathf.Sin(t * Mathf.PI) * 10f;
            });
            AddScaleCurve(clip, "Thân/Càng 2", 0.6666667f, 0.6666667f, 1f, duration, breathTimes, (t, baseV) =>
            {
                float p = Mathf.Sin(t * Mathf.PI);
                return new Vector3(baseV.x * (1f + 0.03f * p), baseV.y * (1f + 0.03f * p), 1f);
            });

            // 8 Legs: subtle idle twitching
            string[] topLegs = {
                "Thân/Chân/TuThanNguyenTo_BoPhan_13",
                "Thân/Chân/TuThanNguyenTo_BoPhan_11",
                "Thân/Chân/TuThanNguyenTo_BoPhan_12",
                "Thân/Chân/TuThanNguyenTo_BoPhan_18 (1)"
            };
            float[] topBaseEuler = { -5.61f, 0f, 0f, 0f };

            string[] botLegs = {
                "Thân/Chân/TuThanNguyenTo_BoPhan_15",
                "Thân/Chân/TuThanNguyenTo_BoPhan_16",
                "Thân/Chân/TuThanNguyenTo_BoPhan_17",
                "Thân/Chân/TuThanNguyenTo_BoPhan_18"
            };
            float[] botBaseEuler = { 0f, 0f, 0f, 0f };

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                AddEulerCurveZ(clip, topLegs[idx], duration, breathTimes, t =>
                {
                    return topBaseEuler[idx] + Mathf.Sin((t * Mathf.PI) + idx * 0.8f) * 3f;
                });
                AddEulerCurveZ(clip, botLegs[idx], duration, breathTimes, t =>
                {
                    return botBaseEuler[idx] + Mathf.Sin((t * Mathf.PI) + (idx + 2) * 0.8f) * 3f;
                });
            }

            // Tail segments (Đuôi 1 .. Đuôi 7)
            string[] tailSegs = {
                "Thân/Đuôi 1",
                "Thân/Đuôi 1/Đuôi 2",
                "Thân/Đuôi 1/Đuôi 2/Đuôi 3",
                "Thân/Đuôi 1/Đuôi 2/Đuôi 3/Đuôi 4",
                "Thân/Đuôi 1/Đuôi 2/Đuôi 3/Đuôi 4/Đuôi 5",
                "Thân/Đuôi 1/Đuôi 2/Đuôi 3/Đuôi 4/Đuôi 5/Đuôi 6",
                "Thân/Đuôi 1/Đuôi 2/Đuôi 3/Đuôi 4/Đuôi 5/Đuôi 6/Đuôi 7"
            };

            float[] sideAEuler = { 31.29f, 18.20f, 14.70f, -15.51f, 13.92f, -83.50f, 57.70f };
            float[] sideBEuler = { 85.00f, 50.00f, 46.00f,  74.11f, 36.28f, -18.70f, 170.30f };

            float transDur = 0.8f;

            for (int s = 0; s < 7; s++)
            {
                int segIdx = s;
                float delay = segIdx * 0.025f;

                float t1Start = 7.0f + delay;
                float t1End = t1Start + transDur;
                float t2Start = 15.0f + delay;
                float t2End = t2Start + transDur;

                // Milestone times: breathing points and whip transition points
                float[] tailMilestones = {
                    0f, 1.75f, 3.5f, 5.25f,
                    t1Start, t1Start + 0.4f, t1End,
                    9.5f, 11.5f, 13.5f,
                    t2Start, t2Start + 0.4f, t2End,
                    16.0f
                };

                AddEulerCurveZ(clip, tailSegs[segIdx], duration, tailMilestones, t =>
                {
                    float k;
                    if (t < t1Start)
                    {
                        // Side A with subtle breathing
                        k = -0.015f * Mathf.Sin(t * Mathf.PI);
                    }
                    else if (t < t1End)
                    {
                        // Smooth whip A -> B
                        float u = (t - t1Start) / transDur;
                        k = (1.0f - Mathf.Cos(u * Mathf.PI)) * 0.5f;
                    }
                    else if (t < t2Start)
                    {
                        // Side B with subtle breathing
                        k = 1.0f + 0.015f * Mathf.Sin((t - t1End) * Mathf.PI);
                    }
                    else if (t < t2End)
                    {
                        // Smooth whip B -> A
                        float u = (t - t2Start) / transDur;
                        k = (1.0f + Mathf.Cos(u * Mathf.PI)) * 0.5f;
                    }
                    else
                    {
                        // Settle back on Side A
                        k = -0.015f * Mathf.Sin((t - 16.0f) * Mathf.PI);
                    }

                    return (1.0f - k) * sideAEuler[segIdx] + k * sideBEuler[segIdx];
                });
            }

            SetLoop(clip, true);
            SaveOrCreateClip(clip, clipPath);
            return clip;
        }

        private static AnimationClip GenerateTuThanNguyenToMove()
        {
            string clipPath = $"{TuThanDir}/TuThanNguyenTo_Move.anim";
            AnimationClip clip = new AnimationClip { frameRate = 60f };
            float duration = 1.2f;

            float[] crawlTimes = { 0f, 0.15f, 0.3f, 0.45f, 0.6f, 0.75f, 0.9f, 1.05f, 1.2f };
            float[] pincerTimes = { 0f, 0.3f, 0.6f, 0.9f, 1.2f };

            // Thân
            AddEulerCurveZ(clip, "Thân", duration, crawlTimes, t =>
            {
                return Mathf.Sin(t / duration * Mathf.PI * 4f) * 2.5f;
            });
            AddScaleCurve(clip, "Thân", 1f, 1f, 1f, duration, crawlTimes, (t, baseV) =>
            {
                float pulse = Mathf.Sin(t / duration * Mathf.PI * 4f);
                return new Vector3(baseV.x * (1f + 0.015f * pulse), baseV.y * (1f - 0.015f * pulse), 1f);
            });

            // 2 Pincers under Thân
            AddEulerCurveZ(clip, "Thân/Càng 1", duration, pincerTimes, t =>
            {
                return -18.45f + Mathf.Sin(t / duration * Mathf.PI * 2f) * 22f;
            });
            AddScaleCurve(clip, "Thân/Càng 1", 0.6666667f, 0.6666667f, 1f, duration, pincerTimes, (t, baseV) =>
            {
                float p = Mathf.Sin(t / duration * Mathf.PI * 2f);
                return new Vector3(baseV.x * (1f + 0.04f * p), baseV.y * (1f + 0.04f * p), 1f);
            });

            AddEulerCurveZ(clip, "Thân/Càng 2", duration, pincerTimes, t =>
            {
                return Mathf.Sin((t / duration * Mathf.PI * 2f) + Mathf.PI * 0.5f) * 22f;
            });
            AddScaleCurve(clip, "Thân/Càng 2", 0.6666667f, 0.6666667f, 1f, duration, pincerTimes, (t, baseV) =>
            {
                float p = Mathf.Sin((t / duration * Mathf.PI * 2f) + Mathf.PI * 0.5f);
                return new Vector3(baseV.x * (1f + 0.04f * p), baseV.y * (1f + 0.04f * p), 1f);
            });

            // 8 Legs: arachnid alternating gait
            string legTop1 = "Thân/Chân/TuThanNguyenTo_BoPhan_13";
            string legTop2 = "Thân/Chân/TuThanNguyenTo_BoPhan_11";
            string legTop3 = "Thân/Chân/TuThanNguyenTo_BoPhan_12";
            string legTop4 = "Thân/Chân/TuThanNguyenTo_BoPhan_18 (1)";

            string legBot1 = "Thân/Chân/TuThanNguyenTo_BoPhan_15";
            string legBot2 = "Thân/Chân/TuThanNguyenTo_BoPhan_16";
            string legBot3 = "Thân/Chân/TuThanNguyenTo_BoPhan_17";
            string legBot4 = "Thân/Chân/TuThanNguyenTo_BoPhan_18";

            float legAmp = 18f;
            float legFreq = (Mathf.PI * 2f * 2f) / duration;

            void AnimateLeg(string path, float baseEuler, float phase)
            {
                AddEulerCurveZ(clip, path, duration, crawlTimes, t =>
                {
                    return baseEuler + Mathf.Sin((t * legFreq) + phase) * legAmp;
                });
                AddScaleCurve(clip, path, 1f, 1f, 1f, duration, crawlTimes, (t, baseV) =>
                {
                    float lift = Mathf.Cos((t * legFreq) + phase) * 0.06f;
                    return new Vector3(baseV.x * (1f + lift), baseV.y * (1f + lift), 1f);
                });
            }

            // Group A (Phase 0)
            AnimateLeg(legTop1, -5.61f, 0f);
            AnimateLeg(legTop3, 0f, 0.4f);
            AnimateLeg(legBot2, 0f, 0f);
            AnimateLeg(legBot4, 0f, 0.4f);

            // Group B (Phase PI)
            AnimateLeg(legTop2, 0f, Mathf.PI);
            AnimateLeg(legTop4, 0f, Mathf.PI + 0.4f);
            AnimateLeg(legBot1, 0f, Mathf.PI);
            AnimateLeg(legBot3, 0f, Mathf.PI + 0.4f);

            // Tail: shake
            string[] tailSegs = {
                "Thân/Đuôi 1",
                "Thân/Đuôi 1/Đuôi 2",
                "Thân/Đuôi 1/Đuôi 2/Đuôi 3",
                "Thân/Đuôi 1/Đuôi 2/Đuôi 3/Đuôi 4",
                "Thân/Đuôi 1/Đuôi 2/Đuôi 3/Đuôi 4/Đuôi 5",
                "Thân/Đuôi 1/Đuôi 2/Đuôi 3/Đuôi 4/Đuôi 5/Đuôi 6",
                "Thân/Đuôi 1/Đuôi 2/Đuôi 3/Đuôi 4/Đuôi 5/Đuôi 6/Đuôi 7"
            };
            float[] tailBaseEuler = { 68.8f, 28.2f, 25.4f, 29.3f, 25.1f, -51.1f, 114.0f };
            float[] tailCurveWeights = { 12f * 2.9f, 14f * 2.9f, 14f * 2.9f, 12f * 2.9f, 12f * 2.9f, 10f * 2.9f, 0f };
            float[] shakeAmps = { 0.6f, 0.8f, 1.0f, 1.1f, 1.2f, 1.3f, 1.5f };
            float shakeFreq = (5.0f * 2.0f * Mathf.PI) / duration;

            float[] tailShakeTimes = {
                0f, 0.06f, 0.18f, 0.30f, 0.42f, 0.54f, 0.66f, 0.78f, 0.90f, 1.02f, 1.14f, 1.2f
            };

            for (int s = 0; s < 7; s++)
            {
                int segIdx = s;
                float baseAngle = tailBaseEuler[segIdx];
                float curveOffset = tailCurveWeights[segIdx];
                float shakeA = shakeAmps[segIdx];
                float delay = segIdx * 0.025f;

                AddEulerCurveZ(clip, tailSegs[segIdx], duration, tailShakeTimes, t =>
                {
                    float shake = Mathf.Sin((t - delay) * shakeFreq);
                    return baseAngle + curveOffset + (shake * shakeA);
                });
            }

            SetLoop(clip, true);
            SaveOrCreateClip(clip, clipPath);
            return clip;
        }

        #endregion

        #region Helpers

        private static void AddEulerCurveZ(AnimationClip clip, string path, float duration, float[] milestoneTimes, Func<float, float> evalZ)
        {
            AnimationCurve curveZ = CreateMilestoneCurve(duration, milestoneTimes, evalZ);
            AnimationCurve curveZero = CreateConstantCurve(duration, 0f);

            var bindingX = new EditorCurveBinding { path = path, type = typeof(Transform), propertyName = "localEulerAnglesRaw.x" };
            var bindingY = new EditorCurveBinding { path = path, type = typeof(Transform), propertyName = "localEulerAnglesRaw.y" };
            var bindingZ = new EditorCurveBinding { path = path, type = typeof(Transform), propertyName = "localEulerAnglesRaw.z" };

            AnimationUtility.SetEditorCurve(clip, bindingX, curveZero);
            AnimationUtility.SetEditorCurve(clip, bindingY, curveZero);
            AnimationUtility.SetEditorCurve(clip, bindingZ, curveZ);
        }

        private static void AddPositionCurve(AnimationClip clip, string path, float baseX, float baseY, float baseZ, float duration, float[] milestoneTimes, Func<float, Vector3, Vector3> evalPos)
        {
            Vector3 baseV = new Vector3(baseX, baseY, baseZ);
            AnimationCurve curveX = CreateMilestoneCurve(duration, milestoneTimes, t => evalPos(t, baseV).x);
            AnimationCurve curveY = CreateMilestoneCurve(duration, milestoneTimes, t => evalPos(t, baseV).y);
            AnimationCurve curveZ = CreateMilestoneCurve(duration, milestoneTimes, t => evalPos(t, baseV).z);

            var bX = new EditorCurveBinding { path = path, type = typeof(Transform), propertyName = "m_LocalPosition.x" };
            var bY = new EditorCurveBinding { path = path, type = typeof(Transform), propertyName = "m_LocalPosition.y" };
            var bZ = new EditorCurveBinding { path = path, type = typeof(Transform), propertyName = "m_LocalPosition.z" };

            AnimationUtility.SetEditorCurve(clip, bX, curveX);
            AnimationUtility.SetEditorCurve(clip, bY, curveY);
            AnimationUtility.SetEditorCurve(clip, bZ, curveZ);
        }

        private static void AddScaleCurve(AnimationClip clip, string path, float baseX, float baseY, float baseZ, float duration, float[] milestoneTimes, Func<float, Vector3, Vector3> evalScale)
        {
            Vector3 baseV = new Vector3(baseX, baseY, baseZ);
            AnimationCurve curveX = CreateMilestoneCurve(duration, milestoneTimes, t => evalScale(t, baseV).x);
            AnimationCurve curveY = CreateMilestoneCurve(duration, milestoneTimes, t => evalScale(t, baseV).y);
            AnimationCurve curveZ = CreateMilestoneCurve(duration, milestoneTimes, t => evalScale(t, baseV).z);

            var bX = new EditorCurveBinding { path = path, type = typeof(Transform), propertyName = "m_LocalScale.x" };
            var bY = new EditorCurveBinding { path = path, type = typeof(Transform), propertyName = "m_LocalScale.y" };
            var bZ = new EditorCurveBinding { path = path, type = typeof(Transform), propertyName = "m_LocalScale.z" };

            AnimationUtility.SetEditorCurve(clip, bX, curveX);
            AnimationUtility.SetEditorCurve(clip, bY, curveY);
            AnimationUtility.SetEditorCurve(clip, bZ, curveZ);
        }

        private static AnimationCurve CreateConstantCurve(float duration, float val)
        {
            return new AnimationCurve(
                new Keyframe(0f, val, 0f, 0f),
                new Keyframe(duration, val, 0f, 0f)
            );
        }

        private static AnimationCurve CreateMilestoneCurve(float duration, float[] milestoneTimes, Func<float, float> evalFunc, bool loop = true)
        {
            var timesList = new List<float>(milestoneTimes);
            if (!timesList.Contains(0f)) timesList.Add(0f);
            if (!timesList.Contains(duration)) timesList.Add(duration);
            timesList.Sort();

            var keys = new List<Keyframe>();
            for (int i = 0; i < timesList.Count; i++)
            {
                float t = timesList[i];
                if (i > 0 && Mathf.Approximately(t, timesList[i - 1])) continue;
                keys.Add(new Keyframe(t, evalFunc(t)));
            }

            AnimationCurve curve = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }

            if (loop && curve.length > 1)
            {
                float avgSlope = (curve.keys[0].outTangent + curve.keys[curve.length - 1].inTangent) * 0.5f;
                Keyframe first = curve.keys[0];
                first.inTangent = avgSlope;
                first.outTangent = avgSlope;
                curve.MoveKey(0, first);

                Keyframe last = curve.keys[curve.length - 1];
                last.inTangent = avgSlope;
                last.outTangent = avgSlope;
                curve.MoveKey(curve.length - 1, last);
            }

            return curve;
        }

        private static void SetLoop(AnimationClip clip, bool loop)
        {
            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static void SaveOrCreateClip(AnimationClip clip, string path)
        {
            clip.name = Path.GetFileNameWithoutExtension(path);
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                existing.ClearCurves();
                EditorUtility.CopySerialized(clip, existing);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(clip, path);
            }
        }

        private static AnimatorController CreateOrUpdateController(string path, AnimationClip idleClip, AnimationClip moveClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            // Ensure MoveSpeed parameter exists
            bool hasMoveSpeed = false;
            foreach (var param in controller.parameters)
            {
                if (param.name == "MoveSpeed")
                {
                    hasMoveSpeed = true;
                    break;
                }
            }

            if (!hasMoveSpeed)
            {
                controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            }

            // Setup state machine
            var rootStateMachine = controller.layers[0].stateMachine;

            var states = rootStateMachine.states;
            AnimatorState idleState = null;
            AnimatorState moveState = null;

            foreach (var childState in states)
            {
                if (childState.state.name == "Idle") idleState = childState.state;
                else if (childState.state.name == "Move") moveState = childState.state;
            }

            if (idleState == null)
            {
                idleState = rootStateMachine.AddState("Idle", new Vector3(250, 50, 0));
            }
            idleState.motion = idleClip;

            if (moveState == null)
            {
                moveState = rootStateMachine.AddState("Move", new Vector3(250, 150, 0));
            }
            moveState.motion = moveClip;

            rootStateMachine.defaultState = idleState;

            // Clear old transitions to recreate fresh
            idleState.transitions = new AnimatorStateTransition[0];
            moveState.transitions = new AnimatorStateTransition[0];

            // Transition Idle -> Move (MoveSpeed > 0.05)
            var toMove = idleState.AddTransition(moveState);
            toMove.hasExitTime = false;
            toMove.duration = 0.3f;
            toMove.AddCondition(AnimatorConditionMode.Greater, 0.05f, "MoveSpeed");

            // Transition Move -> Idle (MoveSpeed < 0.05)
            var toIdle = moveState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.3f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "MoveSpeed");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AttachControllerToPrefab(string prefabPath, AnimatorController controller)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[EnemyAnimationGenerator] Failed to load prefab at {prefabPath}");
                return;
            }

            // Attach Animator to GameObject containing EnemyModel
            Transform modelTransform = prefabRoot.transform.Find("EnemyModel");
            GameObject targetGo = modelTransform != null ? modelTransform.gameObject : prefabRoot;

            // Remove Animator from root if it was previously attached there
            if (targetGo != prefabRoot)
            {
                Animator rootAnim = prefabRoot.GetComponent<Animator>();
                if (rootAnim != null)
                {
                    UnityEngine.Object.DestroyImmediate(rootAnim, true);
                }
            }

            Animator animator = targetGo.GetComponent<Animator>();
            if (animator == null)
            {
                animator = targetGo.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;

            string targetName = targetGo.name;
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            Debug.Log($"[EnemyAnimationGenerator] Attached {controller.name} to {targetName} in {prefabPath}");
        }

        #endregion
    }
}
