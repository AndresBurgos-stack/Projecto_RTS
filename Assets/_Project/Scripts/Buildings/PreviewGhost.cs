using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Converts an instantiated building prefab into a non-interactive placement
    /// ghost. Colliders, gameplay behaviours and physics are disabled, and all
    /// renderers share the supplied transparent material.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PreviewGhost : MonoBehaviour
    {
        private Renderer[] _renderers;
        private Material _currentMaterial;

        public void Initialize(Material material)
        {
            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycast >= 0)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = ignoreRaycast;
                }
            }

            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            foreach (Behaviour behaviour in GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour != this)
                {
                    behaviour.enabled = false;
                }
            }

            _renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in _renderers)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            ApplyMaterial(material);
        }

        public void SetMaterial(Material material)
        {
            ApplyMaterial(material);
        }

        private void ApplyMaterial(Material material)
        {
            if (material == null || material == _currentMaterial || _renderers == null)
            {
                return;
            }

            _currentMaterial = material;

            foreach (Renderer renderer in _renderers)
            {
                int count = Mathf.Max(1, renderer.sharedMaterials.Length);
                Material[] materials = new Material[count];
                for (int i = 0; i < count; i++)
                {
                    materials[i] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }
    }
}
