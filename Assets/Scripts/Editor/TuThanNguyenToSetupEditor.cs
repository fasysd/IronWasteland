using System;
using System.IO;
using System.Reflection;
using IronWasteland.Core;
using IronWasteland.Enemies;
using IronWasteland.Enemies.DeadlandPhantom;
using IronWasteland.Enemies.ElementalReaper;
using IronWasteland.Enemies.ElementalReaper.Hitboxes;
using IronWasteland.Enemies.ElementalReaper.VFX;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace IronWasteland.Editor
{
    public static class TuThanNguyenToSetupEditor
    {
        private const string RootPath = "Assets/Art/Enemies/TuThanNguyenTo";
        private const string VfxPath = RootPath + "/VFX";
        private const string TexturesPath = VfxPath + "/Textures";
        private const string MaterialsPath = VfxPath + "/Materials";
        private const string PrefabsPath = VfxPath + "/Prefabs";
        private const string AnimPath = RootPath + "/Animations";
        private const string BossPrefabPath = "Assets/Prefabs/Enemies/TuThanNguyenTo.prefab";
        private const string TextureSpritePath = RootPath + "/TuThanNguyenTo-Photoroom.png";

        [MenuItem("Tools/IronWasteland/Setup Boss 8 - Tu Than Nguyen To")]
        public static void SetupAll()
        {
            Debug.Log("[SetupBoss8] Starting complete setup for Boss 8...");
            EnsureDirectories();
            GenerateTextures();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CreateVFXPrefabs();
            CreateAnimationClipsAndController();
            BuildBossPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[SetupBoss8] Complete setup for Boss 8 finished successfully!");
        }

        public static void EnsureDirectories()
        {
            EnsureFolder("Assets/Art/Enemies", "TuThanNguyenTo");
            EnsureFolder(RootPath, "VFX");
            EnsureFolder(VfxPath, "Textures");
            EnsureFolder(VfxPath, "Materials");
            EnsureFolder(VfxPath, "Prefabs");
            EnsureFolder(RootPath, "Animations");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Enemies");
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        #region 1. Procedural Texture Generation

        public static void GenerateTextures()
        {
            CreateRadialGlowTexture(TexturesPath + "/tex_elemental_radial_glow.png", 256);
            CreateWaterWaveConeTexture(TexturesPath + "/tex_thuy_trieu_cone.png", 512, 60f);
            CreateWaterSwirlTexture(TexturesPath + "/tex_water_swirl_ring.png", 512);
            CreateCrescentSlashTexture(TexturesPath + "/tex_doc_quat_crescent.png", 512);
            CreateSmokePuffTexture(TexturesPath + "/tex_poison_smoke_puff.png", 256);
            CreateLavaBeamTexture(TexturesPath + "/tex_lava_beam_core.png", 512, 128);
            CreateFireTornadoTexture(TexturesPath + "/tex_fire_tornado_swirl.png", 512);
            CreateLavaTrailTexture(TexturesPath + "/tex_lava_trail_fissure.png", 512, 128);
            CreateMagicCircleTexture(TexturesPath + "/tex_burst_magic_circle_outer.png", 512, true);
            CreateMagicCircleTexture(TexturesPath + "/tex_burst_magic_circle_inner.png", 512, false);
            CreateShieldDomeTexture(TexturesPath + "/tex_shield_dome_hex.png", 512);
            CreateCrystalCircleTexture(TexturesPath + "/tex_crystal_circle.png", 128);
            CreateCrystalSquareTexture(TexturesPath + "/tex_crystal_square.png", 128);
            CreateCrystalTriangleTexture(TexturesPath + "/tex_crystal_triangle.png", 128);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // Configure all as Sprites
            ConfigureSpriteImporter(TexturesPath + "/tex_elemental_radial_glow.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_thuy_trieu_cone.png", new Vector2(0f, 0.5f));
            ConfigureSpriteImporter(TexturesPath + "/tex_water_swirl_ring.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_doc_quat_crescent.png", new Vector2(0f, 0.5f));
            ConfigureSpriteImporter(TexturesPath + "/tex_poison_smoke_puff.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_lava_beam_core.png", new Vector2(0f, 0.5f));
            ConfigureSpriteImporter(TexturesPath + "/tex_fire_tornado_swirl.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_lava_trail_fissure.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_burst_magic_circle_outer.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_burst_magic_circle_inner.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_shield_dome_hex.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_crystal_circle.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_crystal_square.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_crystal_triangle.png");
        }

        private static void ConfigureSpriteImporter(string path, Vector2? pivot = null)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                if (pivot.HasValue)
                {
                    TextureImporterSettings tis = new TextureImporterSettings();
                    importer.ReadTextureSettings(tis);
                    tis.spriteAlignment = (int)SpriteAlignment.Custom;
                    importer.SetTextureSettings(tis);
                    importer.spritePivot = pivot.Value;
                }
                importer.SaveAndReimport();
            }
        }

        private static void CreateRadialGlowTexture(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float maxR = size * 0.48f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float t = Mathf.Clamp01(dist / maxR);
                    float alpha = Mathf.Pow(1f - t, 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void CreateWaterWaveConeTexture(string path, int size, float angleDeg)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float halfAngle = angleDeg * 0.5f * Mathf.Deg2Rad;
            Vector2 origin = new Vector2(size * 0.05f, size * 0.5f);
            float maxR = size * 0.9f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    Vector2 d = pos - origin;
                    float dist = d.magnitude;
                    float angle = Mathf.Abs(Mathf.Atan2(d.y, d.x));

                    if (dist > 0f && dist <= maxR && angle <= halfAngle)
                    {
                        float rNorm = dist / maxR;
                        float wave = Mathf.Sin(rNorm * Mathf.PI * 4f) * 0.5f + 0.5f;
                        float angleFade = 1f - (angle / halfAngle);
                        float edgeFade = Mathf.Sin(rNorm * Mathf.PI);
                        float alpha = angleFade * edgeFade * (0.6f + 0.4f * wave);
                        // Blue-cyan gradient
                        Color c = Color.Lerp(new Color(0f, 0.6f, 1f, alpha), new Color(0.5f, 0.95f, 1f, alpha), wave);
                        tex.SetPixel(x, y, c);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void CreateWaterSwirlTexture(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float maxR = size * 0.45f;
            float minR = size * 0.15f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x - center, y - center);
                    float dist = d.magnitude;
                    float angle = Mathf.Atan2(d.y, d.x);

                    if (dist >= minR && dist <= maxR)
                    {
                        float rNorm = (dist - minR) / (maxR - minR);
                        float swirl = Mathf.Sin(angle * 3f + rNorm * 6f) * 0.5f + 0.5f;
                        float ringFade = Mathf.Sin(rNorm * Mathf.PI);
                        float alpha = ringFade * (0.4f + 0.6f * swirl);
                        Color c = Color.Lerp(new Color(0f, 0.8f, 0.9f, alpha), new Color(0.2f, 1f, 0.7f, alpha), swirl);
                        tex.SetPixel(x, y, c);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void CreateCrescentSlashTexture(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 origin = new Vector2(size * 0.1f, size * 0.5f);
            float maxR = size * 0.85f;
            float minR = size * 0.35f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x, y) - origin;
                    float dist = d.magnitude;
                    float angle = Mathf.Atan2(d.y, d.x); // [-pi, pi]

                    // Half-moon arc in front (angle between -90 and +90 deg)
                    if (d.x >= 0f && dist >= minR && dist <= maxR)
                    {
                        float rNorm = (dist - minR) / (maxR - minR);
                        float ringFade = Mathf.Sin(rNorm * Mathf.PI);
                        float angleFade = Mathf.Cos(angle); // 1 at center, 0 at top/bottom 90 deg
                        float alpha = ringFade * angleFade;
                        // Toxic emerald green into deep purple
                        Color c = Color.Lerp(new Color(0.2f, 0.9f, 0.2f, alpha), new Color(0.6f, 0.1f, 0.9f, alpha), rNorm);
                        tex.SetPixel(x, y, c);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void CreateSmokePuffTexture(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float maxR = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x - center, y - center);
                    float dist = d.magnitude;
                    float angle = Mathf.Atan2(d.y, d.x);
                    // Turbulent puff edge
                    float noise = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.3f;
                    float rEff = maxR * (0.85f + noise);

                    if (dist <= rEff)
                    {
                        float alpha = Mathf.Pow(1f - (dist / rEff), 1.5f);
                        Color c = new Color(0.3f, 0.1f, 0.5f, alpha * 0.85f);
                        tex.SetPixel(x, y, c);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void CreateLavaBeamTexture(string path, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float cy = (height - 1) * 0.5f;

            for (int y = 0; y < height; y++)
            {
                float dyNorm = Mathf.Abs(y - cy) / cy; // 0 at center, 1 at edge
                float verticalFade = Mathf.Pow(1f - Mathf.Clamp01(dyNorm), 2f);

                for (int x = 0; x < width; x++)
                {
                    float xNorm = (float)x / width;
                    // Core is white-yellow, border is fiery red-orange
                    Color coreColor = new Color(1f, 0.95f, 0.6f, verticalFade);
                    Color outerColor = new Color(1f, 0.2f, 0f, verticalFade * 0.8f);
                    Color c = Color.Lerp(coreColor, outerColor, dyNorm);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void CreateFireTornadoTexture(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float maxR = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x - center, y - center);
                    float dist = d.magnitude;
                    float angle = Mathf.Atan2(d.y, d.x);

                    if (dist <= maxR)
                    {
                        float rNorm = dist / maxR;
                        float spiral = Mathf.Sin(angle * 4f + rNorm * 10f) * 0.5f + 0.5f;
                        float radialFade = 1f - rNorm;
                        float alpha = radialFade * (0.4f + 0.6f * spiral);
                        Color c = Color.Lerp(new Color(1f, 0.2f, 0f, alpha), new Color(1f, 0.85f, 0.1f, alpha), spiral);
                        tex.SetPixel(x, y, c);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void CreateLavaTrailTexture(string path, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float cy = (height - 1) * 0.5f;

            for (int y = 0; y < height; y++)
            {
                float dyNorm = Mathf.Abs(y - cy) / cy;
                float vFade = 1f - Mathf.Clamp01(dyNorm);

                for (int x = 0; x < width; x++)
                {
                    float crack = Mathf.Sin(x * 0.1f) * 0.15f;
                    float fade = Mathf.Clamp01(vFade + crack);
                    Color c = Color.Lerp(new Color(0.2f, 0.05f, 0.02f, fade * 0.7f), new Color(1f, 0.4f, 0f, fade), fade);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void CreateMagicCircleTexture(string path, int size, bool outer)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float maxR = size * 0.46f;
            float ringR = outer ? size * 0.42f : size * 0.28f;
            float ringWidth = size * 0.04f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x - center, y - center);
                    float dist = d.magnitude;
                    float angle = Mathf.Atan2(d.y, d.x);

                    float distToRing = Mathf.Abs(dist - ringR);
                    float ringAlpha = Mathf.Clamp01(1f - (distToRing / ringWidth));

                    // 3 elemental sectors: Water (0-120°), Poison (120-240°), Fire (240-360°)
                    float deg = (angle * Mathf.Rad2Deg + 360f) % 360f;
                    Color sectorColor;
                    if (deg < 120f) sectorColor = new Color(0f, 0.75f, 1f, ringAlpha);
                    else if (deg < 240f) sectorColor = new Color(0.2f, 0.85f, 0.2f, ringAlpha);
                    else sectorColor = new Color(1f, 0.3f, 0f, ringAlpha);

                    // Add runes / glyph notches
                    float notch = Mathf.Abs(Mathf.Sin(angle * (outer ? 12f : 6f)));
                    if (notch > 0.85f && distToRing < ringWidth * 1.5f)
                    {
                        ringAlpha = Mathf.Max(ringAlpha, 0.85f);
                    }

                    sectorColor.a = ringAlpha;
                    tex.SetPixel(x, y, ringAlpha > 0.01f ? sectorColor : Color.clear);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void CreateShieldDomeTexture(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float maxR = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x - center, y - center);
                    float dist = d.magnitude;
                    if (dist <= maxR)
                    {
                        float rNorm = dist / maxR;
                        float rim = Mathf.Pow(rNorm, 3f); // bright rim
                        float inner = (1f - rNorm) * 0.2f;
                        float alpha = Mathf.Clamp01(rim * 0.8f + inner);
                        Color c = new Color(0.3f, 0.8f, 1f, alpha);
                        tex.SetPixel(x, y, c);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void CreateCrystalCircleTexture(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float maxR = size * 0.44f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (dist <= maxR)
                    {
                        float t = 1f - (dist / maxR);
                        Color c = Color.Lerp(new Color(0f, 0.7f, 1f, 1f), Color.white, t * t);
                        tex.SetPixel(x, y, c);
                    }
                    else tex.SetPixel(x, y, Color.clear);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void CreateCrystalSquareTexture(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float halfSize = size * 0.38f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center);
                    float dy = Mathf.Abs(y - center);
                    if (dx <= halfSize && dy <= halfSize)
                    {
                        float edgeDist = Mathf.Max(dx / halfSize, dy / halfSize);
                        float t = 1f - edgeDist;
                        Color c = Color.Lerp(new Color(0.2f, 0.85f, 0.2f, 1f), Color.white, t * t);
                        tex.SetPixel(x, y, c);
                    }
                    else tex.SetPixel(x, y, Color.clear);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void CreateCrystalTriangleTexture(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            Vector2 p1 = new Vector2(center, size * 0.85f);
            Vector2 p2 = new Vector2(size * 0.15f, size * 0.2f);
            Vector2 p3 = new Vector2(size * 0.85f, size * 0.2f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pt = new Vector2(x, y);
                    if (PointInTriangle(pt, p1, p2, p3))
                    {
                        float distToCenter = Vector2.Distance(pt, new Vector2(center, size * 0.45f)) / (size * 0.4f);
                        float t = 1f - Mathf.Clamp01(distToCenter);
                        Color c = Color.Lerp(new Color(1f, 0.25f, 0.05f, 1f), Color.white, t * t);
                        tex.SetPixel(x, y, c);
                    }
                    else tex.SetPixel(x, y, Color.clear);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static bool PointInTriangle(Vector2 pt, Vector2 v1, Vector2 v2, Vector2 v3)
        {
            float d1 = Sign(pt, v1, v2);
            float d2 = Sign(pt, v2, v3);
            float d3 = Sign(pt, v3, v1);
            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        #endregion

        #region 2. VFX Prefabs Creation

        public static void CreateVFXPrefabs()
        {
            // Prefab 1: VFX_ElementalAura.prefab
            CreateElementalAuraPrefab();

            // Prefab 2: VFX_ThuyTrieuHitbox.prefab
            CreateThuyTrieuPrefab();

            // Prefab 3: VFX_ThuyLieu.prefab
            CreateThuyLieuPrefab();

            // Prefab 4: VFX_DocQuatHitbox.prefab
            CreateDocQuatPrefab();

            // Prefab 5: VFX_LoanChuyenBurstHitbox.prefab
            CreateLoanChuyenBurstPrefab();

            // Prefab 6: VFX_PhunDungNhamHitbox.prefab
            CreatePhunDungNhamPrefab();

            // Prefab 7: VFX_FireTornadoZone.prefab
            CreateFireTornadoPrefab();

            // Prefab 8: VFX_HoaTocFireTrailZone.prefab
            CreateHoaTocFireTrailPrefab();

            // Prefab 9: VFX_BurstChargingAura.prefab
            CreateBurstChargingAuraPrefab();

            // Prefab 10: VFX_BurstShieldAndCrystals.prefab
            CreateBurstShieldAndCrystalsPrefab();

            AssetDatabase.SaveAssets();
        }

        private static void CreateElementalAuraPrefab()
        {
            GameObject root = new GameObject("VFX_ElementalAura");
            var vfx = root.AddComponent<ElementalAuraVFX>();

            var glowObj = new GameObject("AuraGlow");
            glowObj.transform.SetParent(root.transform, false);
            var sr = glowObj.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_elemental_radial_glow.png");
            sr.sortingOrder = -1;
            glowObj.transform.localScale = new Vector3(4.5f, 4.5f, 1f);

            var partObj = new GameObject("AuraParticles");
            partObj.transform.SetParent(root.transform, false);
            var ps = partObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.2f;
            main.startSpeed = 0.5f;
            main.startSize = 0.25f;
            main.maxParticles = 50;
            var emission = ps.emission;
            emission.rateOverTime = 25;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.8f;

            var mistObj = new GameObject("MistParticles");
            mistObj.transform.SetParent(root.transform, false);
            var psMist = mistObj.AddComponent<ParticleSystem>();
            var mainMist = psMist.main;
            mainMist.startLifetime = 1.5f;
            mainMist.startSpeed = 0.3f;
            mainMist.startSize = 0.6f;
            mainMist.maxParticles = 30;
            var emissionMist = psMist.emission;
            emissionMist.rateOverTime = 15;
            var shapeMist = psMist.shape;
            shapeMist.shapeType = ParticleSystemShapeType.Circle;
            shapeMist.radius = 1.5f;

            // Wire serialized fields via reflection / serialized object
            SerializedObject so = new SerializedObject(vfx);
            so.FindProperty("auraGlowSprite").objectReferenceValue = sr;
            so.FindProperty("auraParticles").objectReferenceValue = ps;
            so.FindProperty("mistParticles").objectReferenceValue = psMist;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, PrefabsPath + "/VFX_ElementalAura.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateThuyTrieuPrefab()
        {
            GameObject root = new GameObject("VFX_ThuyTrieuHitbox");

            // Polygon Collider 2D cone (4m radius, 60 deg) - Must add before ColliderOwnerRef
            var poly = root.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            int pointCount = 13;
            Vector2[] points = new Vector2[pointCount + 1];
            points[0] = Vector2.zero;
            float startAngle = -30f * Mathf.Deg2Rad;
            float endAngle = 30f * Mathf.Deg2Rad;
            for (int i = 0; i < pointCount; i++)
            {
                float a = Mathf.Lerp(startAngle, endAngle, (float)i / (pointCount - 1));
                points[i + 1] = new Vector2(Mathf.Cos(a) * 4f, Mathf.Sin(a) * 4f);
            }
            poly.points = points;

            var hitbox = root.AddComponent<ThuyTrieuHitbox>();
            var vfx = root.AddComponent<ThuyTrieuVFX>();

            var waveObj = new GameObject("WaveCone");
            waveObj.transform.SetParent(root.transform, false);
            var sr = waveObj.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_thuy_trieu_cone.png");
            sr.sortingOrder = 5;

            var splashObj = new GameObject("WaterSplash");
            splashObj.transform.SetParent(root.transform, false);
            var ps = splashObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.5f;
            main.startSpeed = 4f;
            main.startSize = 0.2f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.5f;

            SerializedObject so = new SerializedObject(vfx);
            so.FindProperty("waveConeSprite").objectReferenceValue = sr;
            so.FindProperty("waterSplashParticles").objectReferenceValue = ps;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, PrefabsPath + "/VFX_ThuyTrieuHitbox.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateThuyLieuPrefab()
        {
            GameObject root = new GameObject("VFX_ThuyLieu");
            var vfx = root.AddComponent<ThuyLieuVFX>();

            var swirlObj = new GameObject("SwirlRing");
            swirlObj.transform.SetParent(root.transform, false);
            var sr = swirlObj.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_water_swirl_ring.png");
            sr.sortingOrder = 5;
            swirlObj.transform.localScale = new Vector3(4f, 4f, 1f);

            var bubbleObj = new GameObject("JadeBubbles");
            bubbleObj.transform.SetParent(root.transform, false);
            var ps = bubbleObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.2f;
            main.startSpeed = 1f;
            main.startSize = 0.25f;
            main.startColor = new Color(0.2f, 1f, 0.7f, 0.8f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.5f;

            SerializedObject so = new SerializedObject(vfx);
            so.FindProperty("swirlSprite").objectReferenceValue = sr;
            so.FindProperty("jadeBubbleParticles").objectReferenceValue = ps;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, PrefabsPath + "/VFX_ThuyLieu.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateDocQuatPrefab()
        {
            GameObject root = new GameObject("VFX_DocQuatHitbox");

            // Polygon Collider 2D semicircle (4m radius, 180 deg) - Must add before ColliderOwnerRef
            var poly = root.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            int count = 17;
            Vector2[] points = new Vector2[count + 1];
            points[0] = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, (float)i / (count - 1));
                points[i + 1] = new Vector2(Mathf.Cos(a) * 4f, Mathf.Sin(a) * 4f);
            }
            poly.points = points;

            var hitbox = root.AddComponent<DocQuatHitbox>();
            var vfx = root.AddComponent<DocQuatVFX>();

            var crescentObj = new GameObject("CrescentSlash");
            crescentObj.transform.SetParent(root.transform, false);
            var sr = crescentObj.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_doc_quat_crescent.png");
            sr.sortingOrder = 5;

            var mistObj = new GameObject("PoisonMist");
            mistObj.transform.SetParent(root.transform, false);
            var ps = mistObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.5f;
            main.startSpeed = 2f;
            main.startSize = 0.4f;
            main.startColor = new Color(0.3f, 0.85f, 0.2f, 0.7f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 2.5f;

            SerializedObject so = new SerializedObject(vfx);
            so.FindProperty("crescentSprite").objectReferenceValue = sr;
            so.FindProperty("poisonMistParticles").objectReferenceValue = ps;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, PrefabsPath + "/VFX_DocQuatHitbox.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateLoanChuyenBurstPrefab()
        {
            GameObject root = new GameObject("VFX_LoanChuyenBurstHitbox");
            var circle = root.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 3.0f;

            var hitbox = root.AddComponent<LoanChuyenBurstHitbox>();

            var shockwaveObj = new GameObject("Shockwave");
            shockwaveObj.transform.SetParent(root.transform, false);
            var sr = shockwaveObj.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_elemental_radial_glow.png");
            sr.color = new Color(0.48f, 0.12f, 0.64f, 0.85f);
            sr.sortingOrder = 4;
            shockwaveObj.transform.localScale = new Vector3(6f, 6f, 1f);

            var smokeObj = new GameObject("SmokeBurst");
            smokeObj.transform.SetParent(root.transform, false);
            var ps = smokeObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.8f;
            main.startSpeed = 4f;
            main.startSize = 0.8f;
            main.startColor = new Color(0.2f, 0.8f, 0.3f, 0.8f);
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 40) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 2.0f;

            root.AddComponent<SimpleVFXAutoCleanup>().lifetime = 0.8f;

            SavePrefab(root, PrefabsPath + "/VFX_LoanChuyenBurstHitbox.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreatePhunDungNhamPrefab()
        {
            GameObject root = new GameObject("VFX_PhunDungNhamHitbox");
            var box = root.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(16f, 1.2f);
            box.offset = new Vector2(8f, 0f);

            var hitbox = root.AddComponent<PhunDungNhamHitbox>();
            var vfx = root.AddComponent<LavaBeamVFX>();

            var coreObj = new GameObject("BeamCore");
            coreObj.transform.SetParent(root.transform, false);
            var sr = coreObj.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_lava_beam_core.png");
            sr.sortingOrder = 6;
            coreObj.transform.localScale = new Vector3(16f, 1.2f, 1f);

            var flameObj = new GameObject("FlameParticles");
            flameObj.transform.SetParent(root.transform, false);
            var ps = flameObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.4f;
            main.startSpeed = 1f;
            main.startSize = 0.5f;
            main.startColor = new Color(1f, 0.4f, 0f, 0.8f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(16f, 1.2f, 0.1f);
            shape.position = new Vector3(8f, 0f, 0f);

            SerializedObject so = new SerializedObject(vfx);
            so.FindProperty("beamCoreSprite").objectReferenceValue = sr;
            so.FindProperty("flameParticles").objectReferenceValue = ps;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, PrefabsPath + "/VFX_PhunDungNhamHitbox.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateFireTornadoPrefab()
        {
            GameObject root = new GameObject("VFX_FireTornadoZone");
            var col = root.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 2.0f;

            var zone = root.AddComponent<FireTornadoZone>();
            var vfx = root.AddComponent<FireTornadoVFX>();

            var tornadoObj = new GameObject("TornadoSprite");
            tornadoObj.transform.SetParent(root.transform, false);
            var sr = tornadoObj.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_fire_tornado_swirl.png");
            sr.sortingOrder = 5;
            tornadoObj.transform.localScale = new Vector3(4f, 4f, 1f);

            var vortexObj = new GameObject("VortexParticles");
            vortexObj.transform.SetParent(root.transform, false);
            var ps = vortexObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.6f;
            main.startSpeed = 2f;
            main.startSize = 0.35f;
            main.startColor = new Color(1f, 0.5f, 0.1f, 0.8f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.5f;

            SerializedObject so = new SerializedObject(vfx);
            so.FindProperty("tornadoSprite").objectReferenceValue = sr;
            so.FindProperty("vortexParticles").objectReferenceValue = ps;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, PrefabsPath + "/VFX_FireTornadoZone.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateHoaTocFireTrailPrefab()
        {
            GameObject root = new GameObject("VFX_HoaTocFireTrailZone");
            var box = root.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(4f, 2f);

            var zone = root.AddComponent<HoaTocFireTrailZone>();

            var spriteObj = new GameObject("TrailSprite");
            spriteObj.transform.SetParent(root.transform, false);
            var sr = spriteObj.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_lava_trail_fissure.png");
            sr.sortingOrder = 1;
            spriteObj.transform.localScale = new Vector3(4f, 2f, 1f);

            var flameObj = new GameObject("GroundFlames");
            flameObj.transform.SetParent(root.transform, false);
            var ps = flameObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.8f;
            main.startSpeed = 0.5f;
            main.startSize = 0.4f;
            main.startColor = new Color(1f, 0.3f, 0f, 0.8f);
            var emission = ps.emission;
            emission.rateOverTime = 20;

            SerializedObject so = new SerializedObject(zone);
            so.FindProperty("trailSprite").objectReferenceValue = sr;
            so.FindProperty("groundFlamesParticles").objectReferenceValue = ps;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, PrefabsPath + "/VFX_HoaTocFireTrailZone.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateBurstChargingAuraPrefab()
        {
            GameObject root = new GameObject("VFX_BurstChargingAura");
            var vfx = root.AddComponent<IronWasteland.Enemies.ElementalReaper.VFX.BurstChargingAuraVFX>();

            var outerObj = new GameObject("MagicCircleOuter");
            outerObj.transform.SetParent(root.transform, false);
            var srOuter = outerObj.AddComponent<SpriteRenderer>();
            srOuter.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_burst_magic_circle_outer.png");
            srOuter.sortingOrder = 2;
            outerObj.transform.localScale = new Vector3(6f, 6f, 1f);

            var innerObj = new GameObject("MagicCircleInner");
            innerObj.transform.SetParent(root.transform, false);
            var srInner = innerObj.AddComponent<SpriteRenderer>();
            srInner.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_burst_magic_circle_inner.png");
            srInner.sortingOrder = 3;
            innerObj.transform.localScale = new Vector3(4.5f, 4.5f, 1f);

            var sparksObj = new GameObject("TriElementSparks");
            sparksObj.transform.SetParent(root.transform, false);
            var ps = sparksObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.8f;
            main.startSpeed = 1.5f;
            main.startSize = 0.3f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 2.5f;

            SerializedObject so = new SerializedObject(vfx);
            so.FindProperty("magicCircleOuter").objectReferenceValue = srOuter;
            so.FindProperty("magicCircleInner").objectReferenceValue = srInner;
            so.FindProperty("triElementSparks").objectReferenceValue = ps;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, PrefabsPath + "/VFX_BurstChargingAura.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateBurstShieldAndCrystalsPrefab()
        {
            GameObject root = new GameObject("VFX_BurstShieldAndCrystals");
            var vfx = root.AddComponent<BurstShieldVFX>();

            var domeObj = new GameObject("ShieldDome");
            domeObj.transform.SetParent(root.transform, false);
            var srDome = domeObj.AddComponent<SpriteRenderer>();
            srDome.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_shield_dome_hex.png");
            srDome.sortingOrder = 8;
            domeObj.transform.localScale = new Vector3(5f, 5f, 1f);

            var cWater = new GameObject("Crystal_Water");
            cWater.transform.SetParent(root.transform, false);
            var srW = cWater.AddComponent<SpriteRenderer>();
            srW.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_crystal_circle.png");
            srW.sortingOrder = 9;
            cWater.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            var cPoison = new GameObject("Crystal_Poison");
            cPoison.transform.SetParent(root.transform, false);
            var srP = cPoison.AddComponent<SpriteRenderer>();
            srP.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_crystal_square.png");
            srP.sortingOrder = 9;
            cPoison.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            var cFire = new GameObject("Crystal_Fire");
            cFire.transform.SetParent(root.transform, false);
            var srF = cFire.AddComponent<SpriteRenderer>();
            srF.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_crystal_triangle.png");
            srF.sortingOrder = 9;
            cFire.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            SerializedObject so = new SerializedObject(vfx);
            so.FindProperty("shieldDomeSprite").objectReferenceValue = srDome;
            so.FindProperty("crystalWaterCircle").objectReferenceValue = cWater.transform;
            so.FindProperty("crystalPoisonSquare").objectReferenceValue = cPoison.transform;
            so.FindProperty("crystalFireTriangle").objectReferenceValue = cFire.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, PrefabsPath + "/VFX_BurstShieldAndCrystals.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
        }

        #endregion

        #region 3. Animation Clips & Animator Controller Creation

        public static void CreateAnimationClipsAndController()
        {
            // 1. Idle (2.0s, Loop)
            CreateClip_Idle();

            // 2. Move (1.0s, Loop)
            CreateClip_Move();

            // 3. ThuyTrieu (1.2s)
            CreateClip_ThuyTrieu();

            // 4. ThuyLieu (1.5s)
            CreateClip_ThuyLieu();

            // 5. DocQuat (1.0s)
            CreateClip_DocQuat();

            // 6. LoanChuyen (1.2s)
            CreateClip_LoanChuyen();

            // 7. PhunDungNham (1.6s)
            CreateClip_PhunDungNham();

            // 8. HoaToc_Charge (1.0s)
            CreateClip_HoaToc_Charge();

            // 9. HoaToc_Dash (0.5s, Loop)
            CreateClip_HoaToc_Dash();

            // 10. BurstCharging (1.5s, Loop)
            CreateClip_BurstCharging();

            // 11. Stun (1.0s, Loop)
            CreateClip_Stun();

            // 12. Die (2.0s)
            CreateClip_Die();

            // Build Animator Controller
            BuildAnimatorController();

            AssetDatabase.SaveAssets();
        }

        private static void CreateClip_Idle()
        {
            var clip = new AnimationClip { frameRate = 30 };
            float duration = 2.0f;

            // ThanTren nhấp nhô nhẹ Y và xoay Z nhẹ
            AddEulerCurve(clip, "ThanTren", 178.95f, duration, t => Mathf.Sin(t * Mathf.PI * 2f) * 1.5f);
            AddPositionCurveY(clip, "ThanTren", -0.1f, duration, t => Mathf.Sin(t * Mathf.PI * 2f) * 0.05f);

            // Chuỗi đuôi 7 đốt uốn lượn hình chữ S mềm mại (độ trễ pha)
            float[] tailBases = { 337.16f, 320.53f, 335.54f, 324.32f, 331.43f, 331.96f, 329.12f };
            float[] tailAmps = { 3.5f, 5.5f, 7.5f, 9.5f, 11.5f, 13.5f, 16.0f };
            string tailPath = "ThanTren/ThanDuoi";
            for (int i = 0; i < 7; i++)
            {
                tailPath += (i == 0) ? "/Duoi1" : $"/Duoi{i + 1}";
                int idx = i;
                AddEulerCurve(clip, tailPath, tailBases[idx], duration, t => Mathf.Sin((t + idx * 0.15f) * Mathf.PI * 2f) * tailAmps[idx]);
            }

            // 2 càng khép mở chậm rãi
            AddEulerCurve(clip, "ThanTren/CangTrai1", 288.96f, duration, t => Mathf.Sin(t * Mathf.PI * 2f) * -6f);
            AddEulerCurve(clip, "ThanTren/CangTrai1/CangTrai2", 272.38f, duration, t => Mathf.Sin(t * Mathf.PI * 2f) * -4f);
            AddEulerCurve(clip, "ThanTren/CangPhai1", 69.39f, duration, t => Mathf.Sin(t * Mathf.PI * 2f) * 6f);
            AddEulerCurve(clip, "ThanTren/CangPhai1/CangPhai2", 87.00f, duration, t => Mathf.Sin(t * Mathf.PI * 2f) * 4f);

            SetLoop(clip, true);
            SaveClip(clip, "TuThanNguyenTo_Idle.anim");
        }

        private static void CreateClip_Move()
        {
            var clip = new AnimationClip { frameRate = 30 };
            float duration = 1.0f;

            // Thân trên nhấp nhô nhịp bước (frequency 2x)
            AddEulerCurve(clip, "ThanTren", 178.95f, duration, t => Mathf.Sin(t * Mathf.PI * 4f) * 2f);
            AddPositionCurveY(clip, "ThanTren", -0.1f, duration, t => Mathf.Sin(t * Mathf.PI * 4f) * 0.08f);

            // Đuôi vểnh cao 35° giữ thăng bằng
            AddEulerCurve(clip, "ThanTren/ThanDuoi/Duoi1", 337.16f + 20f, duration, t => Mathf.Sin(t * Mathf.PI * 4f) * 3f);
            AddEulerCurve(clip, "ThanTren/ThanDuoi/Duoi1/Duoi2", 320.53f + 15f, duration, t => Mathf.Sin(t * Mathf.PI * 4f) * 4f);

            // 6 chân bước so le nhịp 3 (Tripod gait)
            // Nhóm A: ChanTrai1, ChanPhai2, ChanTrai3 (phase 0)
            AddEulerCurve(clip, "ThanTren/ChanTrai1_1", 303.18f, duration, t => Mathf.Sin(t * Mathf.PI * 2f) * 18f);
            AddEulerCurve(clip, "ThanTren/ThanDuoi/ChanPhai2_1", 47.94f, duration, t => Mathf.Sin(t * Mathf.PI * 2f) * -18f);
            AddEulerCurve(clip, "ThanTren/ThanDuoi/ChanTrai3_1", 335.09f, duration, t => Mathf.Sin(t * Mathf.PI * 2f) * 18f);

            // Nhóm B: ChanPhai1, ChanTrai2, ChanPhai3 (phase pi)
            AddEulerCurve(clip, "ThanTren/ChanPhai1_1", 57.87f, duration, t => Mathf.Sin((t + 0.5f) * Mathf.PI * 2f) * 18f);
            AddEulerCurve(clip, "ThanTren/ThanDuoi/ChanTrai2_1", 309.57f, duration, t => Mathf.Sin((t + 0.5f) * Mathf.PI * 2f) * -18f);
            AddEulerCurve(clip, "ThanTren/ThanDuoi/ChanPhai3_1", 33.01f, duration, t => Mathf.Sin((t + 0.5f) * Mathf.PI * 2f) * -18f);

            // 2 càng khép bớt về trước
            AddEulerCurve(clip, "ThanTren/CangTrai1", 288.96f + 10f, duration, t => Mathf.Sin(t * Mathf.PI * 4f) * 3f);
            AddEulerCurve(clip, "ThanTren/CangPhai1", 69.39f - 10f, duration, t => Mathf.Sin(t * Mathf.PI * 4f) * -3f);

            SetLoop(clip, true);
            SaveClip(clip, "TuThanNguyenTo_Move.anim");
        }

        private static void CreateClip_ThuyTrieu()
        {
            var clip = new AnimationClip { frameRate = 30 };
            float duration = 1.2f;

            // 2 càng giương rộng rồi quật mạnh về trước; thân cúi gập
            AddEulerCurve(clip, "ThanTren", 178.95f, duration, t =>
            {
                if (t < 0.35f) return Mathf.Lerp(0f, 15f, t / 0.35f); // ngửa
                if (t < 0.65f) return Mathf.Lerp(15f, -25f, (t - 0.35f) / 0.3f); // gập mạnh
                return Mathf.Lerp(-25f, 0f, (t - 0.65f) / 0.55f); // hồi
            });

            AddEulerCurve(clip, "ThanTren/CangTrai1", 288.96f, duration, t =>
            {
                if (t < 0.35f) return Mathf.Lerp(0f, -30f, t / 0.35f); // giương rộng
                if (t < 0.65f) return Mathf.Lerp(-30f, 25f, (t - 0.35f) / 0.3f); // quật mạnh
                return Mathf.Lerp(25f, 0f, (t - 0.65f) / 0.55f);
            });

            AddEulerCurve(clip, "ThanTren/CangPhai1", 69.39f, duration, t =>
            {
                if (t < 0.35f) return Mathf.Lerp(0f, 30f, t / 0.35f); // giương rộng
                if (t < 0.65f) return Mathf.Lerp(30f, -25f, (t - 0.35f) / 0.3f); // quật mạnh
                return Mathf.Lerp(-25f, 0f, (t - 0.65f) / 0.55f);
            });

            // Chuỗi đuôi quật mạnh hình sóng về phía trước
            AddEulerCurve(clip, "ThanTren/ThanDuoi/Duoi1", 337.16f, duration, t =>
            {
                if (t < 0.4f) return Mathf.Lerp(0f, -20f, t / 0.4f);
                if (t < 0.7f) return Mathf.Lerp(-20f, 40f, (t - 0.4f) / 0.3f);
                return Mathf.Lerp(40f, 0f, (t - 0.7f) / 0.5f);
            });

            SetLoop(clip, false);
            SaveClip(clip, "TuThanNguyenTo_ThuyTrieu.anim");
        }

        private static void CreateClip_ThuyLieu()
        {
            var clip = new AnimationClip { frameRate = 30 };
            float duration = 1.5f;

            // 2 càng đan chéo che ngực
            AddEulerCurve(clip, "ThanTren/CangTrai1", 288.96f, duration, t =>
            {
                float peak = (t > 0.3f && t < 1.1f) ? 35f : Mathf.Sin(t / duration * Mathf.PI) * 35f;
                return peak;
            });
            AddEulerCurve(clip, "ThanTren/CangPhai1", 69.39f, duration, t =>
            {
                float peak = (t > 0.3f && t < 1.1f) ? -35f : -Mathf.Sin(t / duration * Mathf.PI) * 35f;
                return peak;
            });

            // Chuỗi đuôi cuộn tròn bọc thân dưới (mỗi đốt uốn +20°)
            string tail = "ThanTren/ThanDuoi";
            float[] bases = { 337.16f, 320.53f, 335.54f, 324.32f, 331.43f, 331.96f, 329.12f };
            for (int i = 0; i < 7; i++)
            {
                tail += (i == 0) ? "/Duoi1" : $"/Duoi{i + 1}";
                int idx = i;
                AddEulerCurve(clip, tail, bases[idx], duration, t => Mathf.Sin(t / duration * Mathf.PI) * 22f);
            }

            SetLoop(clip, false);
            SaveClip(clip, "TuThanNguyenTo_ThuyLieu.anim");
        }

        private static void CreateClip_DocQuat()
        {
            var clip = new AnimationClip { frameRate = 30 };
            float duration = 1.0f;

            // Càng phải giương cao ra sau rồi quạt mạnh 180° quét sạch vùng phía trước
            AddEulerCurve(clip, "ThanTren/CangPhai1", 69.39f, duration, t =>
            {
                if (t < 0.35f) return Mathf.Lerp(0f, 65f, t / 0.35f); // giương cao ra sau
                if (t < 0.55f) return Mathf.Lerp(65f, -80f, (t - 0.35f) / 0.2f); // quạt mạnh 180°
                return Mathf.Lerp(-80f, 0f, (t - 0.55f) / 0.45f); // hồi về
            });
            AddEulerCurve(clip, "ThanTren/CangPhai1/CangPhai2", 87.00f, duration, t =>
            {
                if (t < 0.35f) return Mathf.Lerp(0f, 40f, t / 0.35f);
                if (t < 0.55f) return Mathf.Lerp(40f, -50f, (t - 0.35f) / 0.2f);
                return Mathf.Lerp(-50f, 0f, (t - 0.55f) / 0.45f);
            });

            // Thân xoay theo lực vung
            AddEulerCurve(clip, "ThanTren", 178.95f, duration, t =>
            {
                if (t < 0.35f) return Mathf.Lerp(0f, 15f, t / 0.35f);
                if (t < 0.55f) return Mathf.Lerp(15f, -25f, (t - 0.35f) / 0.2f);
                return Mathf.Lerp(-25f, 0f, (t - 0.55f) / 0.45f);
            });

            SetLoop(clip, false);
            SaveClip(clip, "TuThanNguyenTo_DocQuat.anim");
        }

        private static void CreateClip_LoanChuyen()
        {
            var clip = new AnimationClip { frameRate = 30 };
            float duration = 1.2f;

            // Rung giật sụp xuống biến mất -> tái xuất hiện bật nảy và đập mạnh 2 càng xuống đất
            AddPositionCurveY(clip, "ThanTren", -0.1f, duration, t =>
            {
                if (t < 0.3f) return Mathf.Lerp(0f, -0.6f, t / 0.3f); // sụp xuống
                if (t < 0.6f) return -0.6f; // biến mất
                if (t < 0.8f) return Mathf.Lerp(-0.6f, 0.4f, (t - 0.6f) / 0.2f); // bật nảy
                if (t < 0.95f) return Mathf.Lerp(0.4f, -0.4f, (t - 0.8f) / 0.15f); // dộng mạnh sàn
                return Mathf.Lerp(-0.4f, 0f, (t - 0.95f) / 0.25f);
            });

            // 2 càng đập mạnh
            AddEulerCurve(clip, "ThanTren/CangTrai1", 288.96f, duration, t =>
            {
                if (t >= 0.6f && t < 0.8f) return 40f;
                if (t >= 0.8f && t < 0.95f) return -30f;
                return 0f;
            });
            AddEulerCurve(clip, "ThanTren/CangPhai1", 69.39f, duration, t =>
            {
                if (t >= 0.6f && t < 0.8f) return -40f;
                if (t >= 0.8f && t < 0.95f) return 30f;
                return 0f;
            });

            SetLoop(clip, false);
            SaveClip(clip, "TuThanNguyenTo_LoanChuyen.anim");
        }

        private static void CreateClip_PhunDungNham()
        {
            var clip = new AnimationClip { frameRate = 30 };
            float duration = 1.6f;

            // Ngửa thân trên 30° lấy đà rồi gập mạnh phóng tia dung nham 16m; đuôi cắm xuống đất
            AddEulerCurve(clip, "ThanTren", 178.95f, duration, t =>
            {
                if (t < 0.4f) return Mathf.Lerp(0f, 30f, t / 0.4f); // ngửa 30 độ
                if (t < 1.2f) return -35f + Mathf.Sin(t * 50f) * 2f; // gập mạnh + rung bần bật
                return Mathf.Lerp(-35f, 0f, (t - 1.2f) / 0.4f);
            });

            // Đuôi cắm xuống đất chịu lực đẩy
            AddEulerCurve(clip, "ThanTren/ThanDuoi/Duoi1", 337.16f, duration, t =>
            {
                if (t < 0.4f) return Mathf.Lerp(0f, -30f, t / 0.4f);
                if (t < 1.2f) return -30f;
                return Mathf.Lerp(-30f, 0f, (t - 1.2f) / 0.4f);
            });

            // 2 càng khép song song giữ hướng
            AddEulerCurve(clip, "ThanTren/CangTrai1", 288.96f, duration, t => (t >= 0.4f && t < 1.2f) ? 15f : 0f);
            AddEulerCurve(clip, "ThanTren/CangPhai1", 69.39f, duration, t => (t >= 0.4f && t < 1.2f) ? -15f : 0f);

            SetLoop(clip, false);
            SaveClip(clip, "TuThanNguyenTo_PhunDungNham.anim");
        }

        private static void CreateClip_HoaToc_Charge()
        {
            var clip = new AnimationClip { frameRate = 30 };
            float duration = 1.0f;

            // Đuôi dựng đứng rung bần bật
            AddEulerCurve(clip, "ThanTren/ThanDuoi/Duoi1", 337.16f, duration, t => 60f + Mathf.Sin(t * 60f) * 6f);
            AddEulerCurve(clip, "ThanTren/ThanDuoi/Duoi1/Duoi2", 320.53f, duration, t => 25f + Mathf.Sin(t * 60f) * 8f);

            // 2 càng giơ cao tích tụ lốc xoáy
            AddEulerCurve(clip, "ThanTren/CangTrai1", 288.96f, duration, t => -40f + Mathf.Sin(t * 30f) * 4f);
            AddEulerCurve(clip, "ThanTren/CangPhai1", 69.39f, duration, t => 40f + Mathf.Sin(t * 30f) * -4f);

            SetLoop(clip, false);
            SaveClip(clip, "TuThanNguyenTo_HoaToc_Charge.anim");
        }

        private static void CreateClip_HoaToc_Dash()
        {
            var clip = new AnimationClip { frameRate = 30 };
            float duration = 0.5f;

            // Toàn thân ép sát đất, đuôi duỗi thẳng, 2 càng chụm nhọn
            AddPositionCurveY(clip, "ThanTren", -0.1f, duration, t => -0.25f);
            AddEulerCurve(clip, "ThanTren/ThanDuoi/Duoi1", 337.16f, duration, t => 5f);
            AddEulerCurve(clip, "ThanTren/CangTrai1", 288.96f, duration, t => 20f);
            AddEulerCurve(clip, "ThanTren/CangPhai1", 69.39f, duration, t => -20f);

            // 6 chân guồng cực nhanh
            AddEulerCurve(clip, "ThanTren/ChanTrai1_1", 303.18f, duration, t => Mathf.Sin(t * Mathf.PI * 4f) * 25f);
            AddEulerCurve(clip, "ThanTren/ChanPhai1_1", 57.87f, duration, t => Mathf.Sin((t + 0.25f) * Mathf.PI * 4f) * 25f);

            SetLoop(clip, true);
            SaveClip(clip, "TuThanNguyenTo_HoaToc_Dash.anim");
        }

        private static void CreateClip_BurstCharging()
        {
            var clip = new AnimationClip { frameRate = 30 };
            float duration = 2.0f; // Chuẩn 2.0s theo GDD

            // Boss bay bổng lên 0.5m, 2 càng dang rộng cực đại, toàn thân tỏa sáng năng lượng tam nguyên
            AddPositionCurveY(clip, "ThanTren", -0.1f, duration, t =>
            {
                if (t < 0.2f) return Mathf.Lerp(0f, 0.5f, t / 0.2f);
                if (t < 0.85f) return 0.5f + Mathf.Sin(t * Mathf.PI * 4f) * 0.05f;
                return Mathf.Lerp(0.5f, 0f, (t - 0.85f) / 0.15f);
            });

            AddEulerCurve(clip, "ThanTren/CangTrai1", 288.96f, duration, t =>
            {
                if (t < 0.25f) return Mathf.Lerp(0f, -45f, t / 0.25f);
                if (t < 0.85f) return -45f + Mathf.Sin(t * 20f) * 3f;
                return Mathf.Lerp(-45f, 0f, (t - 0.85f) / 0.15f);
            });

            AddEulerCurve(clip, "ThanTren/CangPhai1", 69.39f, duration, t =>
            {
                if (t < 0.25f) return Mathf.Lerp(0f, 45f, t / 0.25f);
                if (t < 0.85f) return 45f - Mathf.Sin(t * 20f) * 3f;
                return Mathf.Lerp(45f, 0f, (t - 0.85f) / 0.15f);
            });

            // Đuôi xòe cong uy nghi
            AddEulerCurve(clip, "ThanTren/ThanDuoi/Duoi1", 337.16f, duration, t => (t < 0.85f) ? 25f : Mathf.Lerp(25f, 0f, (t - 0.85f) / 0.15f));
            AddEulerCurve(clip, "ThanTren/ThanDuoi/Duoi1/Duoi2", 320.53f, duration, t => (t < 0.85f) ? 25f : Mathf.Lerp(25f, 0f, (t - 0.85f) / 0.15f));

            SetLoop(clip, false);
            SaveClip(clip, "TuThanNguyenTo_BurstCharging.anim");
        }

        private static void CreateClip_Stun()
        {
            var clip = new AnimationClip { frameRate = 30 };
            float duration = 3.0f; // Chuẩn 3.0s theo GDD

            // Toàn bộ khớp xương mất lực gục rũ xuống sát đất, đuôi nằm bẹp dưới sàn
            AddPositionCurveY(clip, "ThanTren", -0.1f, duration, t =>
            {
                if (t < 0.15f) return Mathf.Lerp(0f, -0.5f, t / 0.15f);
                if (t < 0.85f) return -0.5f + Mathf.Sin(t * 10f) * 0.02f;
                return Mathf.Lerp(-0.5f, 0f, (t - 0.85f) / 0.15f);
            });

            AddEulerCurve(clip, "ThanTren/CangTrai1", 288.96f, duration, t =>
            {
                if (t < 0.15f) return Mathf.Lerp(0f, -40f, t / 0.15f);
                if (t < 0.85f) return -40f;
                return Mathf.Lerp(-40f, 0f, (t - 0.85f) / 0.15f);
            });

            AddEulerCurve(clip, "ThanTren/CangPhai1", 69.39f, duration, t =>
            {
                if (t < 0.15f) return Mathf.Lerp(0f, 40f, t / 0.15f);
                if (t < 0.85f) return 40f;
                return Mathf.Lerp(40f, 0f, (t - 0.85f) / 0.15f);
            });

            AddEulerCurve(clip, "ThanTren/ThanDuoi/Duoi1", 337.16f, duration, t =>
            {
                if (t < 0.15f) return Mathf.Lerp(0f, -25f, t / 0.15f);
                if (t < 0.85f) return -25f;
                return Mathf.Lerp(-25f, 0f, (t - 0.85f) / 0.15f);
            });

            SetLoop(clip, false);
            SaveClip(clip, "TuThanNguyenTo_Stun.anim");
        }

        private static void CreateClip_Die()
        {
            var clip = new AnimationClip { frameRate = 30 };
            float duration = 2.0f;

            // Co giật quằn quại rồi sụp đổ hoàn toàn
            AddPositionCurveY(clip, "ThanTren", -0.1f, duration, t =>
            {
                if (t < 0.6f) return Mathf.Sin(t * 30f) * 0.15f; // quằn quại
                return Mathf.Lerp(0f, -0.6f, (t - 0.6f) / 0.8f); // sụp đổ
            });

            AddEulerCurve(clip, "ThanTren/CangTrai1", 288.96f, duration, t =>
            {
                if (t < 0.6f) return Mathf.Sin(t * 30f) * 30f;
                return -50f;
            });
            AddEulerCurve(clip, "ThanTren/CangPhai1", 69.39f, duration, t =>
            {
                if (t < 0.6f) return -Mathf.Sin(t * 30f) * 30f;
                return 50f;
            });

            SetLoop(clip, false);
            SaveClip(clip, "TuThanNguyenTo_Die.anim");
        }

        private static void AddEulerCurve(AnimationClip clip, string path, float baseZ, float duration, Func<float, float> deltaZFunc)
        {
            int samples = 30;
            Keyframe[] keysX = new Keyframe[samples + 1];
            Keyframe[] keysY = new Keyframe[samples + 1];
            Keyframe[] keysZ = new Keyframe[samples + 1];

            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples;
                float time = t * duration;
                float zVal = baseZ + deltaZFunc(t);
                keysX[i] = new Keyframe(time, 0f);
                keysY[i] = new Keyframe(time, 0f);
                keysZ[i] = new Keyframe(time, zVal);
            }

            clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.x", new AnimationCurve(keysX));
            clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.y", new AnimationCurve(keysY));
            clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.z", new AnimationCurve(keysZ));
        }

        private static void AddPositionCurveY(AnimationClip clip, string path, float baseY, float duration, Func<float, float> deltaYFunc)
        {
            int samples = 30;
            Keyframe[] keysY = new Keyframe[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples;
                float time = t * duration;
                float yVal = baseY + deltaYFunc(t);
                keysY[i] = new Keyframe(time, yVal);
            }
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", new AnimationCurve(keysY));
        }

        private static void SetLoop(AnimationClip clip, bool loop)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static void SaveClip(AnimationClip clip, string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            clip.name = name;
            string fullPath = AnimPath + "/" + fileName;
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(clip, fullPath);
            }
        }

        private static void BuildAnimatorController()
        {
            string ctrlPath = AnimPath + "/TuThanNguyenTo.controller";
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null)
            {
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            }

            // Ensure parameters exist
            EnsureParam(ctrl, "MoveSpeed", AnimatorControllerParameterType.Float);
            EnsureParam(ctrl, "ThuyTrieu", AnimatorControllerParameterType.Trigger);
            EnsureParam(ctrl, "ThuyLieu", AnimatorControllerParameterType.Trigger);
            EnsureParam(ctrl, "DocQuat", AnimatorControllerParameterType.Trigger);
            EnsureParam(ctrl, "LoanChuyen", AnimatorControllerParameterType.Trigger);
            EnsureParam(ctrl, "PhunDungNham", AnimatorControllerParameterType.Trigger);
            EnsureParam(ctrl, "HoaToc_Charge", AnimatorControllerParameterType.Trigger);
            EnsureParam(ctrl, "HoaToc_Dash", AnimatorControllerParameterType.Trigger);
            EnsureParam(ctrl, "BurstCharging", AnimatorControllerParameterType.Trigger);
            EnsureParam(ctrl, "Stun", AnimatorControllerParameterType.Trigger);
            EnsureParam(ctrl, "Die", AnimatorControllerParameterType.Trigger);

            var rootSm = ctrl.layers[0].stateMachine;

            // Load clips
            var clipIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath + "/TuThanNguyenTo_Idle.anim");
            var clipMove = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath + "/TuThanNguyenTo_Move.anim");
            var clipThuyTrieu = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath + "/TuThanNguyenTo_ThuyTrieu.anim");
            var clipThuyLieu = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath + "/TuThanNguyenTo_ThuyLieu.anim");
            var clipDocQuat = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath + "/TuThanNguyenTo_DocQuat.anim");
            var clipLoanChuyen = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath + "/TuThanNguyenTo_LoanChuyen.anim");
            var clipPhunDungNham = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath + "/TuThanNguyenTo_PhunDungNham.anim");
            var clipHoaTocCharge = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath + "/TuThanNguyenTo_HoaToc_Charge.anim");
            var clipHoaTocDash = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath + "/TuThanNguyenTo_HoaToc_Dash.anim");
            var clipBurstCharging = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath + "/TuThanNguyenTo_BurstCharging.anim");
            var clipStun = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath + "/TuThanNguyenTo_Stun.anim");
            var clipDie = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath + "/TuThanNguyenTo_Die.anim");

            // Create / update states
            var stateIdle = GetOrCreateState(rootSm, "Idle", clipIdle);
            var stateMove = GetOrCreateState(rootSm, "Move", clipMove);
            rootSm.defaultState = stateIdle;

            // Transitions between Idle <-> Move
            SetupMoveTransitions(stateIdle, stateMove);

            // Skill states
            AddTriggerState(rootSm, "ThuyTrieu", clipThuyTrieu, stateIdle, 0.9f);
            AddTriggerState(rootSm, "ThuyLieu", clipThuyLieu, stateIdle, 0.9f);
            AddTriggerState(rootSm, "DocQuat", clipDocQuat, stateIdle, 0.9f);
            AddTriggerState(rootSm, "LoanChuyen", clipLoanChuyen, stateIdle, 0.9f);
            AddTriggerState(rootSm, "PhunDungNham", clipPhunDungNham, stateIdle, 0.9f);
            AddTriggerState(rootSm, "HoaToc_Charge", clipHoaTocCharge, stateIdle, 0.9f);
            AddTriggerState(rootSm, "HoaToc_Dash", clipHoaTocDash, stateIdle, 0.9f);
            AddTriggerState(rootSm, "BurstCharging", clipBurstCharging, stateIdle, 1.0f);
            AddTriggerState(rootSm, "Stun", clipStun, stateIdle, 1.0f);
            AddTriggerState(rootSm, "Die", clipDie, null, 0.9f);

            EditorUtility.SetDirty(ctrl);
        }

        private static void EnsureParam(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in ctrl.parameters)
            {
                if (p.name == name) return;
            }
            ctrl.AddParameter(name, type);
        }

        private static AnimatorState GetOrCreateState(AnimatorStateMachine sm, string name, Motion motion)
        {
            foreach (var s in sm.states)
            {
                if (s.state.name == name)
                {
                    s.state.motion = motion;
                    return s.state;
                }
            }
            var newState = sm.AddState(name);
            newState.motion = motion;
            return newState;
        }

        private static void SetupMoveTransitions(AnimatorState stateIdle, AnimatorState stateMove)
        {
            // Clear existing
            stateIdle.transitions = new AnimatorStateTransition[0];
            stateMove.transitions = new AnimatorStateTransition[0];

            var toMove = stateIdle.AddTransition(stateMove);
            toMove.hasExitTime = false;
            toMove.duration = 0.2f;
            toMove.canTransitionToSelf = false;
            toMove.AddCondition(AnimatorConditionMode.Greater, 0.05f, "MoveSpeed");

            var toIdle = stateMove.AddTransition(stateIdle);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.2f;
            toIdle.canTransitionToSelf = false;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "MoveSpeed");
        }

        private static void AddTriggerState(AnimatorStateMachine sm, string triggerName, Motion motion, AnimatorState returnState, float exitTime = 0.9f)
        {
            var state = GetOrCreateState(sm, triggerName, motion);

            // AnyState -> state
            AnimatorStateTransition anyTrans = null;
            foreach (var trans in sm.anyStateTransitions)
            {
                if (trans.destinationState == state)
                {
                    anyTrans = trans;
                    break;
                }
            }
            if (anyTrans == null)
            {
                anyTrans = sm.AddAnyStateTransition(state);
            }
            anyTrans.hasExitTime = false;
            anyTrans.duration = 0.1f;
            anyTrans.canTransitionToSelf = false;
            anyTrans.conditions = new AnimatorCondition[0];
            anyTrans.AddCondition(AnimatorConditionMode.If, 0, triggerName);

            // Return to returnState after finish
            state.transitions = new AnimatorStateTransition[0];
            if (returnState != null)
            {
                var exitTrans = state.AddTransition(returnState);
                exitTrans.hasExitTime = true;
                exitTrans.exitTime = exitTime;
                exitTrans.duration = 0.15f;
                exitTrans.canTransitionToSelf = false;
            }
        }

        #endregion

        #region 4. Boss Prefab Assembly

        public static void BuildBossPrefab()
        {
            // Root GameObject
            GameObject root = new GameObject("TuThanNguyenTo");
            root.layer = 7; // Enemy

            var rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;

            var vfxCtrl = root.AddComponent<ElementalReaperVFXController>();

            // Child 1: EnemyModel
            GameObject modelObj = new GameObject("EnemyModel");
            modelObj.layer = 7;
            modelObj.transform.SetParent(root.transform, false);

            var sr = modelObj.AddComponent<SpriteRenderer>();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureSpritePath);
            sr.sprite = sprite;
            sr.sortingOrder = 0;

            var skin = modelObj.AddComponent<SpriteSkin>();

            // Invoke SpriteSkinUtility.CreateBoneHierarchy via reflection
            var utilType = Type.GetType("UnityEngine.U2D.Animation.SpriteSkinUtility, Unity.2D.Animation.Runtime");
            if (utilType != null)
            {
                var m = utilType.GetMethod("CreateBoneHierarchy", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                m?.Invoke(null, new object[] { skin });
            }

            var anim = modelObj.AddComponent<Animator>();
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimPath + "/TuThanNguyenTo.controller");

            var modelComp = modelObj.AddComponent<EnemyModel>();
            var capCol = modelObj.AddComponent<CapsuleCollider2D>();
            capCol.size = new Vector2(2.5f, 3.2f);
            capCol.isTrigger = false;

            var modelOwnerRef = modelObj.AddComponent<ColliderOwnerRef>();

            // Wire EnemyModel fields
            SerializedObject modelSo = new SerializedObject(modelComp);
            modelSo.FindProperty("spriteRenderer").objectReferenceValue = sr;
            modelSo.FindProperty("animator").objectReferenceValue = anim;
            modelSo.FindProperty("bodyCollider").objectReferenceValue = capCol;
            modelSo.FindProperty("ownerRef").objectReferenceValue = modelOwnerRef;
            modelSo.ApplyModifiedPropertiesWithoutUndo();

            // Child 2: EnemySensorArea
            GameObject sensorObj = new GameObject("EnemySensorArea");
            sensorObj.layer = 7;
            sensorObj.transform.SetParent(root.transform, false);
            var sensorCol = sensorObj.AddComponent<CircleCollider2D>();
            sensorCol.isTrigger = true;
            sensorCol.radius = 20.0f;
            sensorObj.AddComponent<ColliderOwnerRef>();
            sensorObj.AddComponent<EnemySensorArea>();

            // Child 3: HoaTocDashHitbox (Trigger càn quét khi lướt)
            GameObject dashObj = new GameObject("HoaTocDashHitbox");
            dashObj.layer = 7;
            dashObj.transform.SetParent(root.transform, false);
            var dashCol = dashObj.AddComponent<CircleCollider2D>();
            dashCol.isTrigger = true;
            dashCol.radius = 1.8f;
            var dashHitbox = dashObj.AddComponent<HoaTocDashHitbox>();
            dashObj.SetActive(false); // Inactive cho tới khi Boss kích hoạt lướt

            // Wire ElementalReaperVFXController fields with the 10 VFX prefabs and dash hitbox
            SerializedObject vfxSo = new SerializedObject(vfxCtrl);
            vfxSo.FindProperty("elementalAuraPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_ElementalAura.prefab");
            vfxSo.FindProperty("thuyTrieuHitboxPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_ThuyTrieuHitbox.prefab");
            vfxSo.FindProperty("thuyLieuVFXPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_ThuyLieu.prefab");
            vfxSo.FindProperty("docQuatHitboxPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_DocQuatHitbox.prefab");
            vfxSo.FindProperty("loanChuyenBurstHitboxPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_LoanChuyenBurstHitbox.prefab");
            vfxSo.FindProperty("phunDungNhamHitboxPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_PhunDungNhamHitbox.prefab");
            vfxSo.FindProperty("fireTornadoZonePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_FireTornadoZone.prefab");
            vfxSo.FindProperty("hoaTocFireTrailZonePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_HoaTocFireTrailZone.prefab");
            vfxSo.FindProperty("burstChargingAuraPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_BurstChargingAura.prefab");
            vfxSo.FindProperty("burstShieldAndCrystalsPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_BurstShieldAndCrystals.prefab");
            vfxSo.FindProperty("hoaTocDashHitbox").objectReferenceValue = dashHitbox;
            vfxSo.ApplyModifiedPropertiesWithoutUndo();

            // Save Prefab
            SavePrefab(root, BossPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("[SetupBoss8] Built TuThanNguyenTo.prefab successfully!");
        }

        #endregion
    }
}
