#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace IronWasteland.Tests
{
    /// <summary>
    /// Đo bề rộng mesh mà một <see cref="Image"/> sinh ra, chia cho bề rộng rect của nó.
    /// Dùng để kiểm chứng thanh máu kiểu Filled có thật sự cắt theo <c>fillAmount</c> hay không:
    /// Image không có sprite sẽ bỏ qua fillAmount và vẽ đủ khung (đo được 1.0 thay vì 0.4).
    /// </summary>
    internal static class UiMeshProbe
    {
        internal static float MeasureFillRatio(Image image)
        {
            RectTransform rect = image.rectTransform;
            rect.ForceUpdateRectTransforms();
            float full = rect.rect.width;
            if (full <= 0.001f) return -1f;

            MethodInfo populate = typeof(Image).GetMethod(
                "OnPopulateMesh",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(VertexHelper) },
                null);
            if (populate == null) return -1f;

            VertexHelper helper = new VertexHelper();
            try
            {
                populate.Invoke(image, new object[] { helper });
                List<UIVertex> vertices = new List<UIVertex>();
                helper.GetUIVertexStream(vertices);
                if (vertices.Count == 0) return -1f;

                float minX = float.MaxValue;
                float maxX = float.MinValue;
                for (int i = 0; i < vertices.Count; i++)
                {
                    float x = vertices[i].position.x;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                }

                return (maxX - minX) / full;
            }
            finally
            {
                helper.Dispose();
            }
        }
    }
}
#endif
