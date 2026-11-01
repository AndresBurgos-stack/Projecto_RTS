using System;
using UnityEngine;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Logical grid used to validate and reserve building footprints.
    /// The grid lies on the XZ plane: cell.y maps to world Z.
    /// Cell (0,0) starts at <see cref="origin"/>.
    /// </summary>
    public sealed class BuildGrid : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField, Min(0.25f)] private float cellSize = 1f;
        [SerializeField, Min(1)] private int width = 40;
        [SerializeField, Min(1)] private int height = 40;
        [SerializeField] private Vector3 origin = Vector3.zero;

        [Header("Debug")]
        [SerializeField] private bool drawGridGizmos = true;
        [SerializeField] private Color freeCellColor = new Color(0.2f, 0.8f, 0.2f, 0.15f);
        [SerializeField] private Color occupiedCellColor = new Color(0.9f, 0.2f, 0.2f, 0.35f);

        private BuildingDefinition[,] _cells;

        /// <summary>Raised after a successful placement or removal.</summary>
        public event Action GridChanged;

        public float CellSize => cellSize;
        public int Width => width;
        public int Height => height;
        public Vector3 Origin => origin;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Reset()
        {
            origin = transform.position;
        }

        /// <summary>Returns true when the cell is inside the grid bounds.</summary>
        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
        }

        /// <summary>World position of the center of a cell.</summary>
        public Vector3 CellToWorld(Vector2Int cell)
        {
            return origin + new Vector3(
                (cell.x + 0.5f) * cellSize,
                0f,
                (cell.y + 0.5f) * cellSize);
        }

        /// <summary>
        /// World position of the center of a rectangular footprint whose
        /// minimum cell is <paramref name="originCell"/>.
        /// </summary>
        public Vector3 FootprintCenterWorld(Vector2Int originCell, Vector2Int footprint)
        {
            return origin + new Vector3(
                (originCell.x + footprint.x * 0.5f) * cellSize,
                0f,
                (originCell.y + footprint.y * 0.5f) * cellSize);
        }

        /// <summary>
        /// Minimum cell of a footprint centered as closely as possible on
        /// <paramref name="centerCell"/>. Even footprints cannot be perfectly
        /// centered on a single cell, so the extra cell extends toward +X/+Z.
        /// </summary>
        public Vector2Int FootprintOriginFromCenter(Vector2Int centerCell, Vector2Int footprint)
        {
            return centerCell - new Vector2Int(
                Mathf.Max(0, (footprint.x - 1) / 2),
                Mathf.Max(0, (footprint.y - 1) / 2));
        }

        /// <summary>Grid cell containing a world position.</summary>
        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            Vector3 local = worldPosition - origin;
            return new Vector2Int(
                Mathf.FloorToInt(local.x / cellSize),
                Mathf.FloorToInt(local.z / cellSize));
        }

        /// <summary>
        /// Checks the whole footprint without reserving it.
        /// </summary>
        public bool CanPlace(BuildingDefinition definition, Vector2Int originCell)
        {
            if (definition == null)
            {
                return false;
            }

            EnsureInitialized();
            Vector2Int footprint = definition.Footprint;

            for (int x = 0; x < footprint.x; x++)
            {
                for (int y = 0; y < footprint.y; y++)
                {
                    Vector2Int cell = originCell + new Vector2Int(x, y);
                    if (!IsInside(cell) || _cells[cell.x, cell.y] != null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Reserves the footprint. This only updates the logical grid; the caller
        /// is responsible for instantiating the building prefab.
        /// </summary>
        public bool TryPlace(BuildingDefinition definition, Vector2Int originCell)
        {
            if (!CanPlace(definition, originCell))
            {
                return false;
            }

            Vector2Int footprint = definition.Footprint;
            for (int x = 0; x < footprint.x; x++)
            {
                for (int y = 0; y < footprint.y; y++)
                {
                    Vector2Int cell = originCell + new Vector2Int(x, y);
                    _cells[cell.x, cell.y] = definition;
                }
            }

            GridChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Clears the building whose origin is <paramref name="originCell"/>.
        /// </summary>
        public bool TryRemove(Vector2Int originCell)
        {
            EnsureInitialized();

            if (!IsInside(originCell))
            {
                return false;
            }

            BuildingDefinition definition = _cells[originCell.x, originCell.y];
            if (definition == null)
            {
                return false;
            }

            Vector2Int footprint = definition.Footprint;
            for (int x = 0; x < footprint.x; x++)
            {
                for (int y = 0; y < footprint.y; y++)
                {
                    Vector2Int cell = originCell + new Vector2Int(x, y);
                    if (IsInside(cell))
                    {
                        _cells[cell.x, cell.y] = null;
                    }
                }
            }

            GridChanged?.Invoke();
            return true;
        }

        /// <summary>Building definition occupying a cell, or null when free.</summary>
        public BuildingDefinition GetOccupant(Vector2Int cell)
        {
            EnsureInitialized();
            return IsInside(cell) ? _cells[cell.x, cell.y] : null;
        }

        public bool IsOccupied(Vector2Int cell)
        {
            return GetOccupant(cell) != null;
        }

        private void EnsureInitialized()
        {
            if (_cells == null || _cells.GetLength(0) != width || _cells.GetLength(1) != height)
            {
                _cells = new BuildingDefinition[width, height];
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGridGizmos)
            {
                return;
            }

            EnsureInitialized();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Gizmos.color = _cells[x, y] == null ? freeCellColor : occupiedCellColor;
                    Gizmos.DrawCube(
                        CellToWorld(new Vector2Int(x, y)),
                        new Vector3(cellSize * 0.95f, 0.02f, cellSize * 0.95f));
                }
            }
        }
    }
}
