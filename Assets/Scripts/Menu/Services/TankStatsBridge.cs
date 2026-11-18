using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using IronWasteland.Tanks;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Lấy chỉ số cơ bản của một xe ở cấp L để HIỂN THỊ trong Menu / Nhà xe.
    /// </summary>
    public static class TankStatsBridge
    {
        private const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<Type, Func<TankDefinition, int, TankStats>> Cache =
            new Dictionary<Type, Func<TankDefinition, int, TankStats>>();

        private static readonly Dictionary<Type, string> Sources = new Dictionary<Type, string>();

        // Sửa kiểu key từ int sang EntityId để khớp với def.GetEntityId()
        private static readonly Dictionary<EntityId, string> Reports = new Dictionary<EntityId, string>();

        private static readonly HashSet<Type> Warned = new HashSet<Type>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            Sources.Clear();
            Reports.Clear();
            Warned.Clear();
        }

        public static TankStats GetStats(TankDefinition definition, int level)
        {
            if (definition == null) return Zero();

            level = Mathf.Clamp(level, TankDefinition.MinLevel, TankDefinition.MaxLevel);
            Func<TankDefinition, int, TankStats> resolver = GetResolver(definition);

            if (resolver == null)
            {
                Type type = definition.GetType();
                if (Warned.Add(type))
                {
                    Debug.LogWarning(
                        $"[{nameof(TankStatsBridge)}] Không tìm thấy cách đọc chỉ số theo cấp trong {type.Name}. " +
                        "Hãy sửa TankStatsBridge.GetStats để gọi đúng hàm của TankDefinition.", definition);
                }
                return Zero();
            }

            DiagnoseOnce(definition, resolver);
            return resolver(definition, level) ?? Zero();
        }

        /// <summary>Một dòng mô tả: nguồn đọc chỉ số + chỉ số cấp 1 / cấp 90. Rỗng nếu chưa đọc được.</summary>
        public static string GetReport(TankDefinition definition)
        {
            if (definition == null) return "";

            Func<TankDefinition, int, TankStats> resolver = GetResolver(definition);
            if (resolver == null) return "KHÔNG TÌM THẤY CÁCH ĐỌC CHỈ SỐ THEO CẤP TRONG " + definition.GetType().Name;

            DiagnoseOnce(definition, resolver);
            return Reports.TryGetValue(definition.GetEntityId(), out string report) ? report : "";
        }

        private static Func<TankDefinition, int, TankStats> GetResolver(TankDefinition definition)
        {
            Type type = definition.GetType();
            if (!Cache.TryGetValue(type, out Func<TankDefinition, int, TankStats> resolver))
            {
                resolver = BuildResolver(type);
                Cache[type] = resolver;
            }
            return resolver;
        }

        // ============================================================
        // CHẨN ĐOÁN
        // ============================================================

        private static void DiagnoseOnce(TankDefinition def, Func<TankDefinition, int, TankStats> resolver)
        {
            EntityId id = def.GetEntityId();
            if (Reports.ContainsKey(id)) return;

            TankStats a = resolver(def, TankDefinition.MinLevel) ?? Zero();
            TankStats b = resolver(def, TankDefinition.MaxLevel) ?? Zero();

            bool same =
                Mathf.Approximately(a.Attack, b.Attack) && Mathf.Approximately(a.MaxHealth, b.MaxHealth) &&
                Mathf.Approximately(a.MaxEnergy, b.MaxEnergy) && Mathf.Approximately(a.Defense, b.Defense) &&
                Mathf.Approximately(a.MoveSpeed, b.MoveSpeed) && Mathf.Approximately(a.CooldownReduction, b.CooldownReduction) &&
                Mathf.Approximately(a.DamageRate, b.DamageRate) && Mathf.Approximately(a.ArmorPenetration, b.ArmorPenetration);

            string source = Sources.TryGetValue(def.GetType(), out string s) ? s : "?";
            string report =
                $"{source} | CẤP {TankDefinition.MinLevel}: ATK {a.Attack} HP {a.MaxHealth} EN {a.MaxEnergy} DEF {a.Defense} SPD {a.MoveSpeed}" +
                $" | CẤP {TankDefinition.MaxLevel}: ATK {b.Attack} HP {b.MaxHealth} EN {b.MaxEnergy} DEF {b.Defense} SPD {b.MoveSpeed}";

            if (same)
            {
                report += " | HAI MỐC GIỐNG NHAU -> CHỈ SỐ KHÔNG ĐỔI KHI NÂNG CẤP";
                Debug.LogWarning(
                    $"[{nameof(TankStatsBridge)}] {def.name}: {report}\n" +
                    "=> Asset TankDefinition chưa nhập chỉ số cấp 90 (hoặc hai mốc đang là giá trị mặc định). " +
                    "Chạy IronWasteland > Menu > 3. Điền chỉ số cơ bản từ GDD vào TankDefinition, hoặc nhập tay.", def);
            }
            else
            {
                Debug.Log($"[{nameof(TankStatsBridge)}] {def.name}: {report}", def);
            }

            Reports[id] = report;
        }

        // ============================================================
        // TÌM CÁCH ĐỌC
        // ============================================================

        private static Func<TankDefinition, int, TankStats> BuildResolver(Type type)
        {
            Func<TankDefinition, TankStats> getLv1 = FindStatsMember(type, "1", out string name1);
            Func<TankDefinition, TankStats> getLv90 = FindStatsMember(type, "90", out string name90);
            if (getLv1 != null && getLv90 != null)
            {
                Sources[type] = $"FIELD {name1} / {name90}";
                return (def, lvl) =>
                {
                    float span = TankDefinition.MaxLevel - TankDefinition.MinLevel;
                    float k = span > 0f ? (lvl - TankDefinition.MinLevel) / span : 0f;
                    return Lerp(getLv1(def), getLv90(def), k);
                };
            }

            for (Type cur = type; cur != null && cur != typeof(ScriptableObject); cur = cur.BaseType)
            {
                foreach (MethodInfo m in cur.GetMethods(Flags))
                {
                    if (m.ReturnType != typeof(TankStats)) continue;

                    ParameterInfo[] ps = m.GetParameters();
                    if (ps.Length != 1 || ps[0].ParameterType != typeof(int)) continue;
                    if (m.Name.IndexOf("Stat", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    MethodInfo method = m;
                    Sources[type] = $"HÀM {method.Name}(int)";
                    return (def, lvl) => method.Invoke(def, new object[] { lvl }) as TankStats;
                }
            }

            return null;
        }

        private static Func<TankDefinition, TankStats> FindStatsMember(Type type, string levelNumber, out string memberName)
        {
            string a = "level" + levelNumber;
            string b = "lv" + levelNumber;
            memberName = "";

            for (Type cur = type; cur != null && cur != typeof(ScriptableObject); cur = cur.BaseType)
            {
                foreach (FieldInfo f in cur.GetFields(Flags))
                {
                    if (f.FieldType != typeof(TankStats)) continue;

                    string n = Normalize(f.Name);
                    if (!n.EndsWith(a, StringComparison.Ordinal) && !n.EndsWith(b, StringComparison.Ordinal)) continue;

                    FieldInfo field = f;
                    memberName = f.Name;
                    return def => field.GetValue(def) as TankStats;
                }

                foreach (PropertyInfo p in cur.GetProperties(Flags))
                {
                    if (p.PropertyType != typeof(TankStats) || !p.CanRead || p.GetIndexParameters().Length > 0) continue;

                    string n = Normalize(p.Name);
                    if (!n.EndsWith(a, StringComparison.Ordinal) && !n.EndsWith(b, StringComparison.Ordinal)) continue;

                    PropertyInfo prop = p;
                    memberName = p.Name;
                    return def => prop.GetValue(def) as TankStats;
                }
            }

            return null;
        }

        private static string Normalize(string name) => name.Replace("_", "").ToLowerInvariant();

        public static TankStats Lerp(TankStats a, TankStats b, float t)
        {
            a = a ?? Zero();
            b = b ?? a;

            return new TankStats
            {
                Attack = Mathf.Lerp(a.Attack, b.Attack, t),
                Defense = Mathf.Lerp(a.Defense, b.Defense, t),
                DamageRate = Mathf.Lerp(a.DamageRate, b.DamageRate, t),
                ArmorPenetration = Mathf.Lerp(a.ArmorPenetration, b.ArmorPenetration, t),
                MaxHealth = Mathf.Lerp(a.MaxHealth, b.MaxHealth, t),
                MaxEnergy = Mathf.Lerp(a.MaxEnergy, b.MaxEnergy, t),
                MoveSpeed = Mathf.Lerp(a.MoveSpeed, b.MoveSpeed, t),
                CooldownReduction = Mathf.Lerp(a.CooldownReduction, b.CooldownReduction, t),
            };
        }

        private static TankStats Zero() => TankStats.Zeroed();
    }
}