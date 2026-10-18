using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using IronWasteland.Enemies;

namespace IronWasteland.Editor
{
    public static class EnemyGeneratorBatch
    {
        private const string PrefabTemplatePath = "Assets/Prefabs/Enemies/Enemy_Normal.prefab";
        private const string PrefabOutputDir = "Assets/Prefabs/Enemies";
        private const string ChildVisualName = "Visual";

        private enum EnemyGroup
        {
            Group1_Standard2F,    // KIenLuaKhongLo, QuaiVatDamLay, KySinhThienThach
            Group2_FloatingSway,  // MatTrangDen, MatTroiDo, XaThuTinhThe
            Group3_PulsingIdle,   // SaoDen, NamDocGaiThach
            Group4_SpiritSway,    // TinhLinhNghiaDia, TinhLinhSamSetNho, XenNuoc
            Group4_FastQuaiVat    // QuaiVatTinhThach (0.13s move)
        }

        private struct EnemyConfig
        {
            public string Name;
            public EnemyGroup Group;

            public EnemyConfig(string name, EnemyGroup group)
            {
                Name = name;
                Group = group;
            }
        }

        private static readonly EnemyConfig[] Enemies = new[]
        {
            new EnemyConfig("KIenLuaKhongLo", EnemyGroup.Group1_Standard2F),
            new EnemyConfig("QuaiVatDamLay", EnemyGroup.Group1_Standard2F),
            new EnemyConfig("KySinhThienThach", EnemyGroup.Group1_Standard2F),

            new EnemyConfig("MatTrangDen", EnemyGroup.Group2_FloatingSway),
            new EnemyConfig("MatTroiDo", EnemyGroup.Group2_FloatingSway),
            new EnemyConfig("XaThuTinhThe", EnemyGroup.Group2_FloatingSway),

            new EnemyConfig("SaoDen", EnemyGroup.Group3_PulsingIdle),
            new EnemyConfig("NamDocGaiThach", EnemyGroup.Group3_PulsingIdle),

            new EnemyConfig("TinhLinhNghiaDia", EnemyGroup.Group4_SpiritSway),
            new EnemyConfig("TinhLinhSamSetNho", EnemyGroup.Group4_SpiritSway),
            new EnemyConfig("XenNuoc", EnemyGroup.Group4_SpiritSway),
            new EnemyConfig("QuaiVatTinhThach", EnemyGroup.Group4_FastQuaiVat)
        };

        [MenuItem("IronWasteland/Generate All Enemies Batch")]
        public static void GenerateAll()
        {
            Debug.Log("[EnemyGeneratorBatch] Starting batch generation for 12 enemies...");

            if (!AssetDatabase.IsValidFolder(PrefabOutputDir))
            {
                EnsureDirectory(PrefabOutputDir);
            }

            foreach (var cfg in Enemies)
            {
                GenerateEnemy(cfg);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EnemyGeneratorBatch] Successfully generated all 12 enemies animations, controllers, and prefabs!");
        }

        private static void GenerateEnemy(EnemyConfig cfg)
        {
            string enemyDir = $"Assets/Art/Enemies/{cfg.Name}";
            string animDir = $"{enemyDir}/Animations";
            EnsureDirectory(animDir);

            // Load sprites
            string spriteSheetPath = $"{enemyDir}/{cfg.Name}.png";
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(spriteSheetPath);
            var sprites = subAssets.OfType<Sprite>().OrderBy(s => s.name).ToList();

            if (sprites.Count == 0)
            {
                Debug.LogError($"[EnemyGeneratorBatch] No sprites found at {spriteSheetPath}");
                return;
            }

            Sprite sprite0 = sprites[0];
            Sprite sprite1 = sprites.Count > 1 ? sprites[1] : sprites[0];

            // 1. Create Animations
            AnimationClip idleClip = CreateIdleClip(cfg, animDir, sprite0, sprite1);
            AnimationClip moveClip = CreateMoveClip(cfg, animDir, sprite0, sprite1);
            AnimationClip takeDamageClip = CreateTakeDamageClip(cfg, animDir, sprite0, sprite1);
            AnimationClip takedDamegeClip = CreateTakedDamegeClip(cfg, animDir, sprite0, sprite1);

            // 2. Create AnimatorController
            string controllerPath = $"{animDir}/{cfg.Name}.controller";
            AnimatorController controller = CreateOrUpdateController(
                controllerPath,
                idleClip,
                moveClip,
                takeDamageClip,
                takedDamegeClip
            );

            // 3. Create Prefab
            CreateOrUpdatePrefab(cfg.Name, sprite0, controller);
        }

        #region Animation Clips Generation

        private static AnimationClip CreateIdleClip(EnemyConfig cfg, string animDir, Sprite s0, Sprite s1)
        {
            string clipPath = $"{animDir}/{cfg.Name}_Idle.anim";
            AnimationClip clip = new AnimationClip { frameRate = 60f };

            switch (cfg.Group)
            {
                case EnemyGroup.Group1_Standard2F:
                    // 1 frame: sprite 1
                    SetSpriteFrames(clip, ChildVisualName, new[] { s0 }, 1.0f, false);
                    SetClipLoop(clip, true);
                    break;

                case EnemyGroup.Group2_FloatingSway:
                    // Single sprite, sways with small amplitude simulating floating in the air
                    SetSpriteFrames(clip, ChildVisualName, new[] { s0 }, 2.0f, false);
                    // Minimal keyframes for smooth floating
                    var posYCurve = CreateSmoothCurve(
                        (0.0f, 0.0f),
                        (0.5f, 0.08f),
                        (1.0f, 0.0f),
                        (1.5f, -0.08f),
                        (2.0f, 0.0f)
                    );
                    var rotZCurve = CreateSmoothCurve(
                        (0.0f, 0.0f),
                        (0.5f, 1.5f),
                        (1.0f, 0.0f),
                        (1.5f, -1.5f),
                        (2.0f, 0.0f)
                    );
                    clip.SetCurve(ChildVisualName, typeof(Transform), "m_LocalPosition.y", posYCurve);
                    clip.SetCurve(ChildVisualName, typeof(Transform), "localEulerAnglesRaw.z", rotZCurve);
                    SetClipLoop(clip, true);
                    break;

                case EnemyGroup.Group3_PulsingIdle:
                    // Single sprite, pulsing scale (breathing)
                    SetSpriteFrames(clip, ChildVisualName, new[] { s0 }, 1.2f, false);
                    var scaleCurve = CreateSmoothCurve(
                        (0.0f, 1.0f),
                        (0.6f, 1.12f),
                        (1.2f, 1.0f)
                    );
                    clip.SetCurve(ChildVisualName, typeof(Transform), "m_LocalScale.x", scaleCurve);
                    clip.SetCurve(ChildVisualName, typeof(Transform), "m_LocalScale.y", scaleCurve);
                    clip.SetCurve(ChildVisualName, typeof(Transform), "m_LocalScale.z", CreateConstantCurve(1.2f, 1.0f));
                    SetClipLoop(clip, true);
                    break;

                case EnemyGroup.Group4_SpiritSway:
                case EnemyGroup.Group4_FastQuaiVat:
                    // 2 frames with 0.2s spacing
                    SetSpriteFrames(clip, ChildVisualName, new[] { s0, s1 }, 0.2f, true);
                    SetClipLoop(clip, true);
                    break;
            }

            SaveClip(clip, clipPath);
            return clip;
        }

        private static AnimationClip CreateMoveClip(EnemyConfig cfg, string animDir, Sprite s0, Sprite s1)
        {
            if (cfg.Group == EnemyGroup.Group3_PulsingIdle)
            {
                // Group 3 does NOT have a Move animation
                return null;
            }

            string clipPath = $"{animDir}/{cfg.Name}_Move.anim";
            AnimationClip clip = new AnimationClip { frameRate = 60f };

            switch (cfg.Group)
            {
                case EnemyGroup.Group1_Standard2F:
                    // 2 frames with 0.2s spacing
                    SetSpriteFrames(clip, ChildVisualName, new[] { s0, s1 }, 0.2f, true);
                    SetClipLoop(clip, true);
                    break;

                case EnemyGroup.Group2_FloatingSway:
                    // Single sprite, sways stronger (higher amplitude, faster frequency)
                    SetSpriteFrames(clip, ChildVisualName, new[] { s0 }, 1.0f, false);
                    var posYStrong = CreateSmoothCurve(
                        (0.0f, 0.0f),
                        (0.25f, 0.18f),
                        (0.5f, 0.0f),
                        (0.75f, -0.18f),
                        (1.0f, 0.0f)
                    );
                    var rotZStrong = CreateSmoothCurve(
                        (0.0f, 0.0f),
                        (0.25f, 4.0f),
                        (0.5f, 0.0f),
                        (0.75f, -4.0f),
                        (1.0f, 0.0f)
                    );
                    clip.SetCurve(ChildVisualName, typeof(Transform), "m_LocalPosition.y", posYStrong);
                    clip.SetCurve(ChildVisualName, typeof(Transform), "localEulerAnglesRaw.z", rotZStrong);
                    SetClipLoop(clip, true);
                    break;

                case EnemyGroup.Group4_SpiritSway:
                    // 2 frames with 0.2s spacing + slight swaying
                    SetSpriteFrames(clip, ChildVisualName, new[] { s0, s1 }, 0.2f, true);
                    var posYLight = CreateSmoothCurve(
                        (0.0f, 0.0f),
                        (0.1f, 0.06f),
                        (0.2f, 0.0f),
                        (0.3f, -0.06f),
                        (0.4f, 0.0f)
                    );
                    var rotZLight = CreateSmoothCurve(
                        (0.0f, 0.0f),
                        (0.1f, 2.0f),
                        (0.2f, 0.0f),
                        (0.3f, -2.0f),
                        (0.4f, 0.0f)
                    );
                    clip.SetCurve(ChildVisualName, typeof(Transform), "m_LocalPosition.y", posYLight);
                    clip.SetCurve(ChildVisualName, typeof(Transform), "localEulerAnglesRaw.z", rotZLight);
                    SetClipLoop(clip, true);
                    break;

                case EnemyGroup.Group4_FastQuaiVat:
                    // 2 frames with 0.13s spacing
                    SetSpriteFrames(clip, ChildVisualName, new[] { s0, s1 }, 0.13f, true);
                    SetClipLoop(clip, true);
                    break;
            }

            SaveClip(clip, clipPath);
            return clip;
        }

        private static AnimationClip CreateTakeDamageClip(EnemyConfig cfg, string animDir, Sprite s0, Sprite s1)
        {
            string clipPath = $"{animDir}/{cfg.Name}_TakeDamage.anim";
            AnimationClip clip = new AnimationClip { frameRate = 60f };

            bool is2FrameIdle = cfg.Group == EnemyGroup.Group4_SpiritSway || cfg.Group == EnemyGroup.Group4_FastQuaiVat;
            float duration = is2FrameIdle ? 0.4f : 0.3f;

            if (is2FrameIdle)
            {
                // Alternates 2 frames like Idle (0.2s spacing)
                SetSpriteFrames(clip, ChildVisualName, new[] { s0, s1 }, 0.2f, false);
            }
            else
            {
                // Single base sprite
                SetSpriteFrames(clip, ChildVisualName, new[] { s0 }, duration, false);
            }

            // Moves small distance forward (along local Y) then returns to original position
            float peakTime = duration * 0.4f;
            var forwardCurve = CreateSmoothCurve(
                (0.0f, 0.0f),
                (peakTime, 0.16f),
                (duration, 0.0f)
            );
            clip.SetCurve(ChildVisualName, typeof(Transform), "m_LocalPosition.y", forwardCurve);

            SetClipLoop(clip, false);
            SaveClip(clip, clipPath);
            return clip;
        }

        private static AnimationClip CreateTakedDamegeClip(EnemyConfig cfg, string animDir, Sprite s0, Sprite s1)
        {
            string clipPath = $"{animDir}/{cfg.Name}_TakedDamege.anim";
            AnimationClip clip = new AnimationClip { frameRate = 60f };

            bool is2FrameIdle = cfg.Group == EnemyGroup.Group4_SpiritSway || cfg.Group == EnemyGroup.Group4_FastQuaiVat;
            float duration = is2FrameIdle ? 0.4f : 0.32f;

            if (is2FrameIdle)
            {
                // Alternates 2 frames like Idle (0.2s spacing)
                SetSpriteFrames(clip, ChildVisualName, new[] { s0, s1 }, 0.2f, false);
            }
            else
            {
                // Single base sprite
                SetSpriteFrames(clip, ChildVisualName, new[] { s0 }, duration, false);
            }

            // Violent shake with small amplitude
            float t1 = duration * 0.25f;
            float t2 = duration * 0.50f;
            float t3 = duration * 0.75f;
            float t4 = duration;

            var rotCurve = CreateSmoothCurve(
                (0.00f, 0.0f),
                (t1, 5.0f),
                (t2, -5.0f),
                (t3, 3.0f),
                (t4, 0.0f)
            );
            var posXCurve = CreateSmoothCurve(
                (0.00f, 0.0f),
                (t1, 0.06f),
                (t2, -0.06f),
                (t3, 0.03f),
                (t4, 0.0f)
            );
            clip.SetCurve(ChildVisualName, typeof(Transform), "localEulerAnglesRaw.z", rotCurve);
            clip.SetCurve(ChildVisualName, typeof(Transform), "m_LocalPosition.x", posXCurve);

            // Flashes red color (White -> Red -> White -> Red -> White)
            var redGBCurve = CreateSmoothCurve(
                (0.00f, 1.0f),
                (t1, 0.2f),
                (t2, 1.0f),
                (t3, 0.2f),
                (t4, 1.0f)
            );
            clip.SetCurve(ChildVisualName, typeof(SpriteRenderer), "m_Color.g", redGBCurve);
            clip.SetCurve(ChildVisualName, typeof(SpriteRenderer), "m_Color.b", redGBCurve);
            clip.SetCurve(ChildVisualName, typeof(SpriteRenderer), "m_Color.r", CreateConstantCurve(duration, 1.0f));
            clip.SetCurve(ChildVisualName, typeof(SpriteRenderer), "m_Color.a", CreateConstantCurve(duration, 1.0f));

            SetClipLoop(clip, false);
            SaveClip(clip, clipPath);
            return clip;
        }

        #endregion

        #region Animator Controller Generation

        private static AnimatorController CreateOrUpdateController(
            string path,
            AnimationClip idleClip,
            AnimationClip moveClip,
            AnimationClip takeDamageClip,
            AnimationClip takedDamegeClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            EnsureParameter(controller, "MoveSpeed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "TakeDamage", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "TakedDamege", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            // Remove existing states & anyState transitions to rebuild clean
            foreach (var childState in sm.states.ToArray())
            {
                sm.RemoveState(childState.state);
            }
            foreach (var anyT in sm.anyStateTransitions.ToArray())
            {
                sm.RemoveAnyStateTransition(anyT);
            }

            // Idle State
            var idleState = sm.AddState("Idle", new Vector3(250, 50, 0));
            idleState.motion = idleClip;
            sm.defaultState = idleState;

            // Move State (if present)
            if (moveClip != null)
            {
                var moveState = sm.AddState("Move", new Vector3(250, 150, 0));
                moveState.motion = moveClip;

                var toMove = idleState.AddTransition(moveState);
                toMove.hasExitTime = false;
                toMove.duration = 0.2f;
                toMove.AddCondition(AnimatorConditionMode.Greater, 0.05f, "MoveSpeed");

                var toIdle = moveState.AddTransition(idleState);
                toIdle.hasExitTime = false;
                toIdle.duration = 0.2f;
                toIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "MoveSpeed");
            }

            // TakeDamage State
            var takeDamageState = sm.AddState("TakeDamage", new Vector3(500, 50, 0));
            takeDamageState.motion = takeDamageClip;

            var anyToTakeDamage = sm.AddAnyStateTransition(takeDamageState);
            anyToTakeDamage.hasExitTime = false;
            anyToTakeDamage.duration = 0.05f;
            anyToTakeDamage.canTransitionToSelf = false;
            anyToTakeDamage.AddCondition(AnimatorConditionMode.If, 0, "TakeDamage");

            var takeDamageToIdle = takeDamageState.AddTransition(idleState);
            takeDamageToIdle.hasExitTime = true;
            takeDamageToIdle.exitTime = 0.95f;
            takeDamageToIdle.duration = 0.1f;

            // TakedDamege State
            var takedDamegeState = sm.AddState("TakedDamege", new Vector3(500, 150, 0));
            takedDamegeState.motion = takedDamegeClip;

            var anyToTakedDamege = sm.AddAnyStateTransition(takedDamegeState);
            anyToTakedDamege.hasExitTime = false;
            anyToTakedDamege.duration = 0.05f;
            anyToTakedDamege.canTransitionToSelf = false;
            anyToTakedDamege.AddCondition(AnimatorConditionMode.If, 0, "TakedDamege");

            var takedDamegeToIdle = takedDamegeState.AddTransition(idleState);
            takedDamegeToIdle.hasExitTime = true;
            takedDamegeToIdle.exitTime = 0.95f;
            takedDamegeToIdle.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureParameter(AnimatorController controller, string paramName, AnimatorControllerParameterType type)
        {
            if (!controller.parameters.Any(p => p.name == paramName))
            {
                controller.AddParameter(paramName, type);
            }
        }

        #endregion

        #region Prefab Generation

        private static void CreateOrUpdatePrefab(string enemyName, Sprite defaultSprite, AnimatorController controller)
        {
            string targetPath = $"{PrefabOutputDir}/{enemyName}.prefab";

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabTemplatePath);
            if (root == null)
            {
                Debug.LogError($"[EnemyGeneratorBatch] Failed to load template at {PrefabTemplatePath}");
                return;
            }

            root.name = enemyName;
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            var brain = root.GetComponent<NormalEnemyBrain_Test>();
            if (brain != null)
            {
                SerializedObject brainSo = new SerializedObject(brain);
                brainSo.FindProperty("displayName").stringValue = enemyName;
                brainSo.ApplyModifiedPropertiesWithoutUndo();
            }

            Transform modelT = root.transform.Find("EnemyModel");
            if (modelT == null)
            {
                Debug.LogError("[EnemyGeneratorBatch] EnemyModel child not found in template!");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            GameObject modelGo = modelT.gameObject;

            // Extract material from old SpriteRenderer on EnemyModel if present, then remove it
            var oldSr = modelGo.GetComponent<SpriteRenderer>();
            Material defaultMat = null;
            if (oldSr != null)
            {
                defaultMat = oldSr.sharedMaterial;
                UnityEngine.Object.DestroyImmediate(oldSr, true);
            }

            // Ensure Animator on EnemyModel
            var animator = modelGo.GetComponent<Animator>();
            if (animator == null)
            {
                animator = modelGo.AddComponent<Animator>();
            }
            animator.runtimeAnimatorController = controller;

            // Create child "Visual" under EnemyModel
            Transform visualT = modelT.Find(ChildVisualName);
            GameObject visualGo;
            if (visualT == null)
            {
                visualGo = new GameObject(ChildVisualName);
                visualGo.transform.SetParent(modelT, false);
            }
            else
            {
                visualGo = visualT.gameObject;
            }

            visualGo.layer = modelGo.layer;
            visualGo.transform.localPosition = Vector3.zero;
            visualGo.transform.localRotation = Quaternion.identity;
            visualGo.transform.localScale = Vector3.one;

            var sr = visualGo.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = visualGo.AddComponent<SpriteRenderer>();
            }
            sr.sprite = defaultSprite;
            sr.color = Color.white;
            if (defaultMat != null)
            {
                sr.sharedMaterial = defaultMat;
            }

            // Wire EnemyModel component
            var enemyModel = modelGo.GetComponent<EnemyModel>();
            if (enemyModel != null)
            {
                SerializedObject modelSo = new SerializedObject(enemyModel);
                modelSo.FindProperty("spriteRenderer").objectReferenceValue = sr;
                modelSo.FindProperty("bodyCollider").objectReferenceValue = modelGo.GetComponent<BoxCollider2D>();
                modelSo.FindProperty("ownerRef").objectReferenceValue = modelGo.GetComponent<ColliderOwnerRef>();
                modelSo.ApplyModifiedPropertiesWithoutUndo();
            }

            if (brain != null && enemyModel != null)
            {
                SerializedObject brainSo = new SerializedObject(brain);
                brainSo.FindProperty("model").objectReferenceValue = enemyModel;
                brainSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, targetPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log($"[EnemyGeneratorBatch] Created prefab: {targetPath}");
        }

        #endregion

        #region Helpers

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

        private static void SetSpriteFrames(AnimationClip clip, string childPath, Sprite[] sprites, float frameSpacing, bool loop)
        {
            var binding = new EditorCurveBinding
            {
                path = childPath,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            };

            int count = sprites.Length;
            float totalDuration = count * frameSpacing;
            var keys = new List<ObjectReferenceKeyframe>();

            for (int i = 0; i < count; i++)
            {
                keys.Add(new ObjectReferenceKeyframe { time = i * frameSpacing, value = sprites[i] });
            }

            if (loop && count > 1)
            {
                float boundaryTime = totalDuration - (1f / 60f);
                if (boundaryTime > (count - 1) * frameSpacing)
                {
                    keys.Add(new ObjectReferenceKeyframe { time = boundaryTime, value = sprites[count - 1] });
                }
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());

            // Lock clip length via dummy z curve
            var zCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(totalDuration, 0f));
            clip.SetCurve(childPath, typeof(Transform), "m_LocalPosition.z", zCurve);
        }

        private static AnimationCurve CreateSmoothCurve(params (float time, float value)[] points)
        {
            Keyframe[] keys = new Keyframe[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                keys[i] = new Keyframe(points[i].time, points[i].value);
            }
            AnimationCurve curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }
            return curve;
        }

        private static AnimationCurve CreateConstantCurve(float duration, float value)
        {
            return new AnimationCurve(new Keyframe(0f, value), new Keyframe(duration, value));
        }

        private static void SetClipLoop(AnimationClip clip, bool loop)
        {
            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static void SaveClip(AnimationClip clip, string path)
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

        #endregion
    }
}

