using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using IronWasteland.Tanks;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Lấy prefab <see cref="TankView"/> (phần hình ảnh của xe) để hiển thị trong Menu.
    ///
    /// Thứ tự ưu tiên:
    ///   1. <see cref="TankEntry.viewPrefab"/> nếu bạn gán tay trong TankCatalog (chắc chắn nhất).
    ///   2. Tự tìm trong TankDefinition một field/property kiểu IList&lt;TankView&gt; (List/mảng) hoặc
    ///      IList&lt;GameObject&gt; rồi lấy phần tử theo <see cref="TankEntry.viewId"/>.
    ///
    /// Không tìm thấy thì trả null: Menu quay về hiển thị ảnh tĩnh (portrait) + cảnh báo một lần.
    /// Khi có TankDefinition.cs, thay <see cref="GetViewPrefab"/> bằng lời gọi thẳng là xong.
    /// </summary>
    public static class TankViewBridge
    {
        private const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<Type, Func<TankDefinition, IList<TankView>>> Cache =
            new Dictionary<Type, Func<TankDefinition, IList<TankView>>>();

        private static readonly HashSet<Type> Warned = new HashSet<Type>();

        public static TankView GetViewPrefab(TankEntry entry)
        {
            if (entry == null) return null;
            if (entry.viewPrefab != null) return entry.viewPrefab;
            if (entry.definition == null) return null;

            Type type = entry.definition.GetType();
            if (!Cache.TryGetValue(type, out Func<TankDefinition, IList<TankView>> getter))
            {
                getter = BuildGetter(type);
                Cache[type] = getter;
            }

            if (getter == null)
            {
                if (Warned.Add(type))
                {
                    Debug.LogWarning(
                        $"[{nameof(TankViewBridge)}] Không tìm thấy danh sách TankView trong {type.Name}. " +
                        "Hãy gán tay `viewPrefab` cho từng xe trong TankCatalog (hoặc sửa TankViewBridge).");
                }
                return null;
            }

            IList<TankView> list = getter(entry.definition);
            if (list == null || list.Count == 0) return null;

            return list[Mathf.Clamp(entry.viewId, 0, list.Count - 1)];
        }

        private static Func<TankDefinition, IList<TankView>> BuildGetter(Type type)
        {
            for (Type cur = type; cur != null && cur != typeof(ScriptableObject); cur = cur.BaseType)
            {
                foreach (FieldInfo f in cur.GetFields(Flags))
                {
                    FieldInfo field = f;
                    Func<TankDefinition, IList<TankView>> g = MakeGetter(field.FieldType, d => field.GetValue(d));
                    if (g != null) return g;
                }

                foreach (PropertyInfo p in cur.GetProperties(Flags))
                {
                    if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;

                    PropertyInfo prop = p;
                    Func<TankDefinition, IList<TankView>> g = MakeGetter(prop.PropertyType, d => prop.GetValue(d));
                    if (g != null) return g;
                }
            }

            return null;
        }

        private static Func<TankDefinition, IList<TankView>> MakeGetter(Type memberType, Func<TankDefinition, object> read)
        {
            if (typeof(IList<TankView>).IsAssignableFrom(memberType))
                return d => read(d) as IList<TankView>;

            if (typeof(IList<GameObject>).IsAssignableFrom(memberType))
            {
                return d =>
                {
                    var source = read(d) as IList<GameObject>;
                    if (source == null) return null;

                    var result = new List<TankView>(source.Count);
                    for (int i = 0; i < source.Count; i++)
                        result.Add(source[i] != null ? source[i].GetComponent<TankView>() : null);
                    return result;
                };
            }

            return null;
        }
    }
}
