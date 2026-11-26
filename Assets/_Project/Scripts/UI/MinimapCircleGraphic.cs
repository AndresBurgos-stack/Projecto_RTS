using UnityEngine;
using UnityEngine.UI;

namespace ProjectoRTS.UI
{
    /// <summary>
    /// Filled circle mesh used as the medallion mask. A normal Image mask
    /// clips to its rectangle, so the minimap needs an actual circular mesh
    /// to get a round viewport.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinimapCircleGraphic : MaskableGraphic
    {
        private const int Segments = 64;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0f)
            {
                return;
            }

            Vector2 center = rect.center;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = center;
            vh.AddVert(vertex);

            for (int i = 0; i <= Segments; i++)
            {
                float angle = (i / (float)Segments) * Mathf.PI * 2f;
                Vector2 position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                vertex.position = position;
                vh.AddVert(vertex);
            }

            for (int i = 0; i < Segments; i++)
            {
                vh.AddTriangle(0, i + 1, i + 2);
            }
        }
    }
}
