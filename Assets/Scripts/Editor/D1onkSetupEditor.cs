using System;
using System.IO;
using IronWasteland.Core;
using IronWasteland.Enemies;
using IronWasteland.Enemies.D1onk;
using IronWasteland.Enemies.D1onk.VFX;
using UnityEditor;
using UnityEngine;

namespace IronWasteland.Editor
{
    public static class D1onkSetupEditor
    {
        private const string RootPath = "Assets/Art/Enemies/TruongQuanNgucD1ONK";
        private const string VfxPath = RootPath + "/VFX";
        private const string TexturesPath = VfxPath + "/Textures";
        private const string MaterialsPath = VfxPath + "/Materials";
        private const string PrefabsPath = VfxPath + "/Prefabs";
        private const string BossPrefabPath = "Assets/Prefabs/Enemies/TruongQuanNgucD1ONK .prefab";

        [MenuItem("Tools/IronWasteland/Setup Boss - Truong Quan Nguc D1onk")]
        public static void SetupAll()
        {
            Debug.Log("[SetupD1onk] Bắt đầu thiết lập toàn diện Animation và VFX cho Trưởng Quản Ngục D1onk...");

            EnsureDirectories();
            GenerateTextures();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            CreateVFXPrefabs();
            ConfigureBossPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Debug.Log("[SetupD1onk] Hoàn tất thiết lập toàn diện Animation và VFX cho Trưởng Quản Ngục D1onk thành công!");
        }

        public static void EnsureDirectories()
        {
            EnsureFolder("Assets/Art/Enemies", "TruongQuanNgucD1ONK");
            EnsureFolder(RootPath, "VFX");
            EnsureFolder(VfxPath, "Textures");
            EnsureFolder(VfxPath, "Materials");
            EnsureFolder(VfxPath, "Prefabs");
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
            CreateMissileTexture(TexturesPath + "/tex_d1onk_missile.png", 256, 64);
            CreateRadialGlowTexture(TexturesPath + "/tex_d1onk_muzzle_flash.png", 128);
            CreateExplosionTexture(TexturesPath + "/tex_d1onk_explosion.png", 256);
            CreateLightningBoltTexture(TexturesPath + "/tex_d1onk_lightning_bolt.png", 128, 512);
            CreateWarningReticleTexture(TexturesPath + "/tex_d1onk_warning_reticle.png", 256);
            CreateLaserCoreTexture(TexturesPath + "/tex_d1onk_laser_core.png", 256, 64);
            CreateHeatRingTexture(TexturesPath + "/tex_d1onk_heat_ring.png", 256);
            CreateMagicCircleTexture(TexturesPath + "/tex_d1onk_summon_circle.png", 512);
            CreateChargingSwirlTexture(TexturesPath + "/tex_d1onk_charging_swirl.png", 256);
            CreateStunStarTexture(TexturesPath + "/tex_d1onk_stun_star.png", 128);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ConfigureSpriteImporter(TexturesPath + "/tex_d1onk_missile.png", new Vector2(0.5f, 0.5f));
            ConfigureSpriteImporter(TexturesPath + "/tex_d1onk_muzzle_flash.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_d1onk_explosion.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_d1onk_lightning_bolt.png", new Vector2(0.5f, 0f));
            ConfigureSpriteImporter(TexturesPath + "/tex_d1onk_warning_reticle.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_d1onk_laser_core.png", new Vector2(0f, 0.5f));
            ConfigureSpriteImporter(TexturesPath + "/tex_d1onk_heat_ring.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_d1onk_summon_circle.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_d1onk_charging_swirl.png");
            ConfigureSpriteImporter(TexturesPath + "/tex_d1onk_stun_star.png");
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

        private static void CreateMissileTexture(string path, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, clear);

            float midY = height * 0.5f;
            float bodyHalfH = height * 0.28f;

            for (int x = 0; x < width; x++)
            {
                float tX = (float)x / width;
                float currentH = bodyHalfH;

                // Mũi nhọn tên lửa
                if (tX > 0.75f)
                {
                    float coneT = (1f - tX) / 0.25f;
                    currentH = bodyHalfH * coneT;
                }

                // Cánh đuôi tên lửa
                bool isFin = (tX < 0.25f);

                for (int y = 0; y < height; y++)
                {
                    float distY = Mathf.Abs(y - midY);

                    if (distY <= currentH)
                    {
                        // Thân tên lửa
                        Color c = Color.Lerp(new Color(0.9f, 0.9f, 0.95f), new Color(0.4f, 0.4f, 0.5f), distY / bodyHalfH);
                        // Đầu đạn màu đỏ cam
                        if (tX > 0.65f)
                        {
                            c = Color.Lerp(c, new Color(0.95f, 0.2f, 0.1f), (tX - 0.65f) / 0.35f);
                        }
                        tex.SetPixel(x, y, c);
                    }
                    else if (isFin && distY <= height * 0.45f)
                    {
                        // Cánh ổn định đuôi tên lửa
                        tex.SetPixel(x, y, new Color(0.3f, 0.3f, 0.35f, 0.9f));
                    }
                }
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
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
                    float alpha = Mathf.Pow(1f - t, 2.5f);
                    tex.SetPixel(x, y, new Color(1f, 0.9f, 0.6f, alpha));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }

        private static void CreateExplosionTexture(string path, int size)
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
                    Color c = Color.Lerp(new Color(1f, 1f, 0.8f), new Color(1f, 0.35f, 0.05f), t);
                    tex.SetPixel(x, y, new Color(c.r, c.g, c.b, alpha));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }

        private static void CreateLightningBoltTexture(string path, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, clear);

            float midX = width * 0.5f;
            for (int y = 0; y < height; y++)
            {
                float tY = (float)y / height;
                // Tạo gợn sóng zigzag lôi quang
                float offsetX = Mathf.Sin(tY * 30f) * (width * 0.15f) + Mathf.Sin(tY * 70f) * (width * 0.08f);
                float centerX = midX + offsetX;

                for (int x = 0; x < width; x++)
                {
                    float distX = Mathf.Abs(x - centerX);
                    float coreW = width * 0.12f;
                    float glowW = width * 0.4f;

                    if (distX <= glowW)
                    {
                        float alpha = Mathf.Pow(1f - (distX / glowW), 2f);
                        Color c = distX <= coreW ? new Color(1f, 1f, 1f, 1f) : new Color(0.2f, 0.8f, 1f, alpha);
                        tex.SetPixel(x, y, c);
                    }
                }
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }

        private static void CreateWarningReticleTexture(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float maxR = size * 0.46f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float ringDist = Mathf.Abs(dist - maxR);
                    float innerRingDist = Mathf.Abs(dist - maxR * 0.6f);

                    float alpha = 0f;
                    if (ringDist < size * 0.04f)
                        alpha = 1f - (ringDist / (size * 0.04f));
                    else if (innerRingDist < size * 0.03f)
                        alpha = 0.8f - (innerRingDist / (size * 0.03f));
                    else if (dist < maxR)
                        alpha = 0.15f;

                    tex.SetPixel(x, y, new Color(0.2f, 0.85f, 1f, alpha));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }

        private static void CreateLaserCoreTexture(string path, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float midY = height * 0.5f;
            for (int y = 0; y < height; y++)
            {
                float distY = Mathf.Abs(y - midY);
                float halfH = height * 0.48f;
                float t = Mathf.Clamp01(distY / halfH);
                float alpha = Mathf.Pow(1f - t, 2f);
                Color c = Color.Lerp(Color.white, new Color(0.1f, 0.7f, 1f), t);

                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, new Color(c.r, c.g, c.b, alpha));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }

        private static void CreateHeatRingTexture(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float targetR = size * 0.4f;
            float ringThickness = size * 0.15f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float diff = Mathf.Abs(dist - targetR);
                    float alpha = 0f;
                    if (diff < ringThickness)
                    {
                        alpha = Mathf.Pow(1f - (diff / ringThickness), 1.8f);
                    }
                    Color c = Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.25f, 0.05f), diff / ringThickness);
                    tex.SetPixel(x, y, new Color(c.r, c.g, c.b, alpha));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }

        private static void CreateMagicCircleTexture(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float maxR = size * 0.46f;
            float midR = size * 0.35f;
            float innerR = size * 0.22f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pt = new Vector2(x, y);
                    float dist = Vector2.Distance(pt, new Vector2(center, center));
                    float angle = Mathf.Atan2(y - center, x - center);

                    float alpha = 0f;
                    // Vòng ngoài
                    float dMax = Mathf.Abs(dist - maxR);
                    if (dMax < size * 0.02f) alpha = 1f - (dMax / (size * 0.02f));

                    // Vòng giữa
                    float dMid = Mathf.Abs(dist - midR);
                    if (dMid < size * 0.015f) alpha = Mathf.Max(alpha, 1f - (dMid / (size * 0.015f)));

                    // Vòng trong
                    float dInner = Mathf.Abs(dist - innerR);
                    if (dInner < size * 0.012f) alpha = Mathf.Max(alpha, 1f - (dInner / (size * 0.012f)));

                    // Hoa văn ngôi sao 6 cánh
                    float starR = (midR + innerR) * 0.5f;
                    float modA = Mathf.Repeat(angle, Mathf.PI / 3f);
                    if (Mathf.Abs(modA - Mathf.PI / 6f) < 0.04f && dist > innerR && dist < maxR)
                    {
                        alpha = Mathf.Max(alpha, 0.85f);
                    }

                    Color c = Color.Lerp(new Color(0.6f, 0.2f, 1f), new Color(0.2f, 0.8f, 1f), dist / maxR);
                    tex.SetPixel(x, y, new Color(c.r, c.g, c.b, alpha));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }

        private static void CreateChargingSwirlTexture(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float maxR = size * 0.48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float angle = Mathf.Atan2(y - center, x - center);
                    float swirl = Mathf.Sin(angle * 3f + (dist / maxR) * 10f);

                    float alpha = 0f;
                    if (dist < maxR)
                    {
                        float edgeFade = Mathf.Sin((dist / maxR) * Mathf.PI);
                        alpha = Mathf.Clamp01(swirl * 0.5f + 0.5f) * edgeFade;
                    }
                    Color c = Color.Lerp(new Color(0.2f, 0.7f, 1f), new Color(0.8f, 0.95f, 1f), alpha);
                    tex.SetPixel(x, y, new Color(c.r, c.g, c.b, alpha));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }

        private static void CreateStunStarTexture(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float maxR = size * 0.46f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distX = Mathf.Abs(x - center);
                    float distY = Mathf.Abs(y - center);
                    // Hình sao 4 cánh (astroid: x^(2/3) + y^(2/3) <= r^(2/3))
                    float starVal = Mathf.Pow(distX / maxR, 0.6f) + Mathf.Pow(distY / maxR, 0.6f);
                    float alpha = 0f;
                    if (starVal <= 1f)
                    {
                        alpha = Mathf.Pow(1f - starVal, 1.5f);
                    }
                    Color c = Color.Lerp(new Color(1f, 0.9f, 0.2f), Color.white, alpha);
                    tex.SetPixel(x, y, new Color(c.r, c.g, c.b, alpha));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }

        #endregion

        #region 2. VFX Prefabs Creation

        public static void CreateVFXPrefabs()
        {
            var defaultMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Sprite.mat") ??
                             AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

            // 1. VFX_D1onkExplosion.prefab
            string explosionPath = PrefabsPath + "/VFX_D1onkExplosion.prefab";
            var goExplosion = new GameObject("VFX_D1onkExplosion");
            var srExplosion = goExplosion.AddComponent<SpriteRenderer>();
            srExplosion.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_d1onk_explosion.png");
            srExplosion.sortingLayerName = "VFX";
            srExplosion.sortingOrder = 10;
            var cleanupExplosion = goExplosion.AddComponent<D1onkSimpleAutoCleanup>();
            cleanupExplosion.Lifetime = 0.4f;
            PrefabUtility.SaveAsPrefabAsset(goExplosion, explosionPath);
            GameObject.DestroyImmediate(goExplosion);

            // 2. VFX_D1onkMuzzleFlash.prefab
            string muzzlePath = PrefabsPath + "/VFX_D1onkMuzzleFlash.prefab";
            var goMuzzle = new GameObject("VFX_D1onkMuzzleFlash");
            var srMuzzle = goMuzzle.AddComponent<SpriteRenderer>();
            srMuzzle.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_d1onk_muzzle_flash.png");
            srMuzzle.sortingLayerName = "VFX";
            srMuzzle.sortingOrder = 12;
            var cleanupMuzzle = goMuzzle.AddComponent<D1onkSimpleAutoCleanup>();
            cleanupMuzzle.Lifetime = 0.12f;
            PrefabUtility.SaveAsPrefabAsset(goMuzzle, muzzlePath);
            GameObject.DestroyImmediate(goMuzzle);

            // 3. VFX_D1onkMissileProjectile.prefab (RÀNG BUỘC: TUYỆT ĐỐI KHÔNG có ColliderOwnerRef của Boss)
            string missilePath = PrefabsPath + "/VFX_D1onkMissileProjectile.prefab";
            var goMissile = new GameObject("VFX_D1onkMissileProjectile");
            var srMissile = goMissile.AddComponent<SpriteRenderer>();
            srMissile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_d1onk_missile.png");
            srMissile.sortingLayerName = "VFX";
            srMissile.sortingOrder = 8;
            goMissile.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

            var colMissile = goMissile.AddComponent<CircleCollider2D>();
            colMissile.isTrigger = true;
            colMissile.radius = 0.4f;

            var rbMissile = goMissile.AddComponent<Rigidbody2D>();
            rbMissile.bodyType = RigidbodyType2D.Kinematic;
            rbMissile.gravityScale = 0f;

            var trailObj = new GameObject("Trail");
            trailObj.transform.SetParent(goMissile.transform);
            trailObj.transform.localPosition = new Vector3(-0.35f, 0f, 0f);
            var trail = trailObj.AddComponent<TrailRenderer>();
            trail.startWidth = 0.25f;
            trail.endWidth = 0f;
            trail.time = 0.35f;
            trail.material = defaultMat;
            trail.startColor = new Color(1f, 0.7f, 0.2f, 0.8f);
            trail.endColor = new Color(1f, 0.2f, 0.1f, 0f);

            var missileComp = goMissile.AddComponent<D1onkMissileProjectile>();
            var explosionAsset = AssetDatabase.LoadAssetAtPath<GameObject>(explosionPath);
            SetPrivateField(missileComp, "missileSprite", srMissile);
            SetPrivateField(missileComp, "thrusterTrail", trail);
            SetPrivateField(missileComp, "impactExplosionPrefab", explosionAsset);
            PrefabUtility.SaveAsPrefabAsset(goMissile, missilePath);
            GameObject.DestroyImmediate(goMissile);

            // 4. VFX_D1onkTrungPhatLightning.prefab (RÀNG BUỘC: TUYỆT ĐỐI KHÔNG có ColliderOwnerRef)
            string lightningPath = PrefabsPath + "/VFX_D1onkTrungPhatLightning.prefab";
            var goLightning = new GameObject("VFX_D1onkTrungPhatLightning");

            var reticleObj = new GameObject("WarningReticle");
            reticleObj.transform.SetParent(goLightning.transform);
            reticleObj.transform.localPosition = Vector3.zero;
            var srReticle = reticleObj.AddComponent<SpriteRenderer>();
            srReticle.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_d1onk_warning_reticle.png");
            srReticle.sortingLayerName = "VFX";
            srReticle.sortingOrder = 5;

            var boltObj = new GameObject("LightningBolt");
            boltObj.transform.SetParent(goLightning.transform);
            boltObj.transform.localPosition = Vector3.zero;
            var srBolt = boltObj.AddComponent<SpriteRenderer>();
            srBolt.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_d1onk_lightning_bolt.png");
            srBolt.sortingLayerName = "VFX";
            srBolt.sortingOrder = 15;
            srBolt.size = new Vector2(1.5f, 16f);

            var lineObj = new GameObject("LightningLine");
            lineObj.transform.SetParent(goLightning.transform);
            lineObj.transform.localPosition = Vector3.zero;
            var lr = lineObj.AddComponent<LineRenderer>();
            lr.material = defaultMat;
            lr.startColor = Color.white;
            lr.endColor = new Color(0.2f, 0.8f, 1f, 0.8f);
            lr.startWidth = 0.5f;
            lr.endWidth = 0.3f;
            lr.enabled = false;

            var trungPhatComp = goLightning.AddComponent<D1onkTrungPhatVFX>();
            SetPrivateField(trungPhatComp, "warningReticle", srReticle);
            SetPrivateField(trungPhatComp, "lightningBoltSprite", srBolt);
            SetPrivateField(trungPhatComp, "lightningBoltLine", lr);
            PrefabUtility.SaveAsPrefabAsset(goLightning, lightningPath);
            GameObject.DestroyImmediate(goLightning);

            // 5. VFX_D1onkLazeNguyenTo.prefab (RÀNG BUỘC: TUYỆT ĐỐI KHÔNG có ColliderOwnerRef)
            string lazePath = PrefabsPath + "/VFX_D1onkLazeNguyenTo.prefab";
            var goLaze = new GameObject("VFX_D1onkLazeNguyenTo");

            var coreObj = new GameObject("LaserCore");
            coreObj.transform.SetParent(goLaze.transform);
            coreObj.transform.localPosition = Vector3.zero;
            var lrLaser = coreObj.AddComponent<LineRenderer>();
            lrLaser.material = defaultMat;
            lrLaser.startColor = Color.white;
            lrLaser.endColor = new Color(0.1f, 0.75f, 1f, 0.9f);
            lrLaser.startWidth = 0.8f;
            lrLaser.endWidth = 0.7f;

            var spriteBeamObj = new GameObject("LaserBeamSprite");
            spriteBeamObj.transform.SetParent(goLaze.transform);
            spriteBeamObj.transform.localPosition = Vector3.zero;
            var srLaser = spriteBeamObj.AddComponent<SpriteRenderer>();
            srLaser.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_d1onk_laser_core.png");
            srLaser.drawMode = SpriteDrawMode.Sliced;
            srLaser.size = new Vector2(25f, 0.8f);
            srLaser.sortingLayerName = "VFX";
            srLaser.sortingOrder = 14;

            var lazeComp = goLaze.AddComponent<D1onkLazeNguyenToVFX>();
            SetPrivateField(lazeComp, "laserCoreLine", lrLaser);
            SetPrivateField(lazeComp, "laserBeamSprite", srLaser);
            PrefabUtility.SaveAsPrefabAsset(goLaze, lazePath);
            GameObject.DestroyImmediate(goLaze);

            // 6. VFX_D1onkToaNhietDienRong.prefab (RÀNG BUỘC: TUYỆT ĐỐI KHÔNG có ColliderOwnerRef)
            string toaNhietPath = PrefabsPath + "/VFX_D1onkToaNhietDienRong.prefab";
            var goToaNhiet = new GameObject("VFX_D1onkToaNhietDienRong");

            var ringObj = new GameObject("HeatRing");
            ringObj.transform.SetParent(goToaNhiet.transform);
            ringObj.transform.localPosition = Vector3.zero;
            var srHeat = ringObj.AddComponent<SpriteRenderer>();
            srHeat.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_d1onk_heat_ring.png");
            srHeat.sortingLayerName = "VFX";
            srHeat.sortingOrder = 6;

            var toaNhietComp = goToaNhiet.AddComponent<D1onkToaNhietDienRongVFX>();
            SetPrivateField(toaNhietComp, "heatRingSprite", srHeat);
            PrefabUtility.SaveAsPrefabAsset(goToaNhiet, toaNhietPath);
            GameObject.DestroyImmediate(goToaNhiet);

            // 7. VFX_D1onkVienBinhSummon.prefab (RÀNG BUỘC: TUYỆT ĐỐI KHÔNG có ColliderOwnerRef)
            string vienBinhPath = PrefabsPath + "/VFX_D1onkVienBinhSummon.prefab";
            var goVienBinh = new GameObject("VFX_D1onkVienBinhSummon");

            var circleObj = new GameObject("MagicCircle");
            circleObj.transform.SetParent(goVienBinh.transform);
            circleObj.transform.localPosition = Vector3.zero;
            var srCircle = circleObj.AddComponent<SpriteRenderer>();
            srCircle.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_d1onk_summon_circle.png");
            srCircle.sortingLayerName = "VFX";
            srCircle.sortingOrder = 5;
            circleObj.transform.localScale = Vector3.one * 1.5f;

            var summonComp = goVienBinh.AddComponent<D1onkVienBinhSummonVFX>();
            SetPrivateField(summonComp, "magicCircleSprite", srCircle);
            PrefabUtility.SaveAsPrefabAsset(goVienBinh, vienBinhPath);
            GameObject.DestroyImmediate(goVienBinh);

            // 8. VFX_D1onkChargingAura.prefab
            string chargingPath = PrefabsPath + "/VFX_D1onkChargingAura.prefab";
            var goCharging = new GameObject("VFX_D1onkChargingAura");

            var outerObj = new GameObject("SwirlOuter");
            outerObj.transform.SetParent(goCharging.transform);
            outerObj.transform.localPosition = Vector3.zero;
            var srOuter = outerObj.AddComponent<SpriteRenderer>();
            srOuter.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_d1onk_charging_swirl.png");
            srOuter.sortingLayerName = "VFX";
            srOuter.sortingOrder = 7;
            outerObj.transform.localScale = Vector3.one * 3.5f;

            var innerObj = new GameObject("SwirlInner");
            innerObj.transform.SetParent(goCharging.transform);
            innerObj.transform.localPosition = Vector3.zero;
            var srInner = innerObj.AddComponent<SpriteRenderer>();
            srInner.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_d1onk_charging_swirl.png");
            srInner.sortingLayerName = "VFX";
            srInner.sortingOrder = 8;
            innerObj.transform.localScale = Vector3.one * 2.2f;

            var chargingComp = goCharging.AddComponent<D1onkChargingAuraVFX>();
            SetPrivateField(chargingComp, "swirlSpriteOuter", srOuter);
            SetPrivateField(chargingComp, "swirlSpriteInner", srInner);
            PrefabUtility.SaveAsPrefabAsset(goCharging, chargingPath);
            GameObject.DestroyImmediate(goCharging);

            // 9. VFX_D1onkStun.prefab
            string stunPath = PrefabsPath + "/VFX_D1onkStun.prefab";
            var goStun = new GameObject("VFX_D1onkStun");

            Transform[] stars = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var sObj = new GameObject("Star" + (i + 1));
                sObj.transform.SetParent(goStun.transform);
                var srStar = sObj.AddComponent<SpriteRenderer>();
                srStar.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesPath + "/tex_d1onk_stun_star.png");
                srStar.sortingLayerName = "VFX";
                srStar.sortingOrder = 20;
                sObj.transform.localScale = Vector3.one * 0.4f;
                stars[i] = sObj.transform;
            }

            var stunComp = goStun.AddComponent<D1onkStunVFX>();
            SetPrivateField(stunComp, "star1", stars[0]);
            SetPrivateField(stunComp, "star2", stars[1]);
            SetPrivateField(stunComp, "star3", stars[2]);
            PrefabUtility.SaveAsPrefabAsset(goStun, stunPath);
            GameObject.DestroyImmediate(goStun);
        }

        #endregion

        #region 3. Configure Boss Prefab

        public static void ConfigureBossPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath) ??
                         AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/TruongQuanNgucD1ONK.prefab");
            if (prefab == null)
            {
                Debug.LogError($"[SetupD1onk] Boss prefab not found at: {BossPrefabPath} or Assets/Prefabs/Enemies/TruongQuanNgucD1ONK.prefab!");
                return;
            }

            // Gán layer Enemy (7)
            prefab.layer = 7;

            // 1. Procedural Animator
            var anim = prefab.GetComponent<D1onkProceduralAnimator>();
            if (anim == null)
            {
                anim = prefab.AddComponent<D1onkProceduralAnimator>();
            }

            Transform root = prefab.transform;
            Transform model = root.Find("EnemyModel") ?? root;
            Transform ship = model.Find("Chiến hạm");
            Transform than = ship.Find("Thân");
            Transform dau = than.Find("Đầu");
            Transform vaiTrai = than.Find("Vai trái");
            Transform vaiPhai = than.Find("Vai phải");
            Transform tayTrai = than.Find("Tay trái");
            Transform tayPhai = than.Find("Tay phải");
            Transform vaiMatTrongTrai = vaiTrai.Find("Vai mặt trong");
            Transform vaiMatTrongPhai = vaiPhai.Find("Vai mặt trong");
            Transform sung2Nong = ship.Find("Súng 2 nòng");
            Transform sung3Nong = ship.Find("Súng 3 nòng");

            var guns = new System.Collections.Generic.List<Transform>();
            foreach (Transform g in sung2Nong) guns.Add(g);
            foreach (Transform g in sung3Nong) guns.Add(g);

            SetPrivateField(anim, "than", than);
            SetPrivateField(anim, "dau", dau);
            SetPrivateField(anim, "vaiTrai", vaiTrai);
            SetPrivateField(anim, "vaiPhai", vaiPhai);
            SetPrivateField(anim, "vaiMatTrongTrai", vaiMatTrongTrai);
            SetPrivateField(anim, "vaiMatTrongPhai", vaiMatTrongPhai);
            SetPrivateField(anim, "tayTrai", tayTrai);
            SetPrivateField(anim, "tayPhai", tayPhai);
            SetPrivateField(anim, "chienHam", ship);
            SetPrivateField(anim, "sung2NongParent", sung2Nong);
            SetPrivateField(anim, "sung3NongParent", sung3Nong);
            SetPrivateField(anim, "allGuns", guns.ToArray());

            // 2. VFX Controller
            var vfxCtrl = prefab.GetComponent<D1onkVFXController>();
            if (vfxCtrl == null)
            {
                vfxCtrl = prefab.AddComponent<D1onkVFXController>();
            }

            SetPrivateField(vfxCtrl, "proceduralAnimator", anim);
            SetPrivateField(vfxCtrl, "missileProjectilePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_D1onkMissileProjectile.prefab"));
            SetPrivateField(vfxCtrl, "muzzleFlashPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_D1onkMuzzleFlash.prefab"));
            SetPrivateField(vfxCtrl, "explosionVFXPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_D1onkExplosion.prefab"));
            SetPrivateField(vfxCtrl, "trungPhatLightningPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_D1onkTrungPhatLightning.prefab"));
            SetPrivateField(vfxCtrl, "lazeNguyenToPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_D1onkLazeNguyenTo.prefab"));
            SetPrivateField(vfxCtrl, "toaNhietDienRongPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_D1onkToaNhietDienRong.prefab"));
            SetPrivateField(vfxCtrl, "vienBinhSummonVFXPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_D1onkVienBinhSummon.prefab"));
            SetPrivateField(vfxCtrl, "chargingAuraPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_D1onkChargingAura.prefab"));
            SetPrivateField(vfxCtrl, "stunVFXPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/VFX_D1onkStun.prefab"));
            SetPrivateField(vfxCtrl, "gunBarrels", guns.ToArray());
            SetPrivateField(vfxCtrl, "laserEmitterPoint", than);
            SetPrivateField(vfxCtrl, "headPoint", dau);

            // Nạp toàn bộ 12 quái thường trong Assets/Prefabs/Enemies/Normal
            string[] normalGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Enemies/Normal" });
            var normalList = new System.Collections.Generic.List<GameObject>();
            foreach (var guid in normalGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (enemyPrefab != null) normalList.Add(enemyPrefab);
            }
            SetPrivateField(vfxCtrl, "normalEnemyPrefabs", normalList.ToArray());

            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
            Debug.Log($"[SetupD1onk] Đã cấu hình và lưu prefab thành công: {BossPrefabPath} với {guns.Count} nòng súng và {normalList.Count} quái thường viện binh!");
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;
            var f = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (f != null)
            {
                f.SetValue(target, value);
            }
            else
            {
                Debug.LogWarning($"[SetupD1onk] Field '{fieldName}' not found on type {target.GetType().Name}!");
            }
        }

        #endregion
    }
}
