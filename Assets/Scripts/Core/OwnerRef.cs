using UnityEngine;

namespace IronWasteland
{
    /// <summary>
    /// Giu tham chieu toi "cai gi dang so huu object nay".
    /// Owner kieu UnityEngine.Object nen dung chung duoc cho nhieu he thong
    /// (TankBrain, EnemyBrain, PlayerData, GameObject...) ma khong phai doi code.
    ///
    /// Ben doc tu kiem tra type bang TryGetOwner<T>().
    /// </summary>
    [DisallowMultipleComponent]
    public class OwnerRef : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Object owner;

        /// <summary>Object dang so huu. Neu de trong thi mac dinh la chinh GameObject nay.</summary>
        public UnityEngine.Object Owner => owner != null ? owner : gameObject;

        public bool HasExplicitOwner => owner != null;

        public void SetOwner(UnityEngine.Object value)
        {
            owner = value;
        }

        /// <summary>Lay Owner neu dung kieu T, nguoc lai tra false.</summary>
        public bool TryGetOwner<T>(out T result) where T : UnityEngine.Object
        {
            result = Owner as T;
            return result != null;
        }

        /// <summary>Lay Owner neu dung kieu T, neu sai kieu tra null.</summary>
        public T GetOwner<T>() where T : UnityEngine.Object => Owner as T;

        /// <summary>Lay OwnerRef tren mot component bat ky (doi voi raycast).</summary>
        public static OwnerRef From(Component component)
        {
            if (component == null) return null;
            return component.GetComponent<OwnerRef>();
        }

        /// <summary>Lay OwnerRef tren GameObject bat ky.</summary>
        public static OwnerRef From(GameObject go)
        {
            if (go == null) return null;
            return go.GetComponent<OwnerRef>();
        }
    }
}
