using UnityEngine;
using UnityEngine.UI;

namespace ProjectoRTS.UI
{
    /// <summary>
    /// Draws the ground footprint of the RTS camera as a translucent quad on
    /// the minimap. Corners are clipped to the map bounds so a shallow view
    /// that reaches the horizon does not produce enormous vertices.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinimapFrustumGraphic : MaskableGraphic
    {
        private static readonly Plane GroundPlane = new Plane(Vector3.up, Vector3.zero);

        private Camera _sourceCamera;
        private Vector2 _worldCenter;
        private float _worldExtent = 200f;

        public void Configure(Camera sourceCamera, Vector2 worldCenter, float worldExtent)
        {
            _sourceCamera = sourceCamera;
            _worldCenter = worldCenter;
            _worldExtent = Mathf.Max(1f, worldExtent);
            SetVerticesDirty();
        }

        public void Refresh()
        {
            if (isActiveAndEnabled)
            {
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (_sourceCamera == null)
            {
                return;
            }

            Vector2 min = _worldCenter - Vector2.one * _worldExtent;
            Vector2 max = _worldCenter + Vector2.one * _worldExtent;
            Vector2 size = rectTransform.rect.size;

            for (int corner = 0; corner < 4; corner++)
            {
                float viewportX = (corner & 1) == 0 ? 0f : 1f;
                float viewportY = (corner & 2) == 0 ? 0f : 1f;
                Ray ray = _sourceCamera.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));

                Vector3 point;
                if (GroundPlane.Raycast(ray, out float distance) && distance > 0f)
                {
                    point = ray.GetPoint(distance);
                }
                else
                {
                    // The corner is looking above the horizon. Push it far
                    // away and let the map clamp below handle it.
                    Vector3 flat = ray.direction;
                    flat.y = 0f;
                    if (flat.sqrMagnitude < 0.0001f)
                    {
                        flat = Vector3.forward;
                    }

                    point = ray.origin + flat.normalized * 1000f;
                }

                point.x = Mathf.Clamp(point.x, min.x, max.x);
                point.z = Mathf.Clamp(point.z, min.y, max.y);

                Vector2 uv = new Vector2(
                    (point.x - _worldCenter.x) / (2f * _worldExtent) + 0.5f,
                    (point.z - _worldCenter.y) / (2f * _worldExtent) + 0.5f);

                UIVertex vertex = UIVertex.simpleVert;
                vertex.color = color;
                vertex.position = new Vector3(
                    (uv.x - 0.5f) * size.x,
                    (uv.y - 0.5f) * size.y,
                    0f);
                vh.AddVert(vertex);
            }

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);
        }
    }
}
