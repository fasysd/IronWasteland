using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    [DisallowMultipleComponent]
    public class GreenFox_Skill2 : MonoBehaviour
    {
        [Header("Effect Settings")]
        [SerializeField, Min(0.1f)] private float lifetime = 1f;

        public void Initialize()
        {
            StartCoroutine(AutoDisableRoutine());
        }

        private IEnumerator AutoDisableRoutine()
        {
            yield return new WaitForSeconds(lifetime);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}