using System.Collections.Generic;
using System.Text;
using ProjectoRTS.Core;
using ProjectoRTS.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectoRTS.UI
{
    /// <summary>
    /// Bottom-left unit info card shown while friendly units are selected.
    /// It replaces the construction menu and shows the current health plus
    /// the combat stats of the selected unit or group. With several units
    /// selected each one gets a portrait with a mini health bar; clicking a
    /// portrait selects just that unit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitInfoPanel : MonoBehaviour
    {
        private const float PanelWidth = 320f;
        private const int PortraitColumns = 7;
        private const float PortraitCellWidth = 38f;
        private const float PortraitCellHeight = 44f;
        private const int MaxPortraits = 14;
        private const float StatsBottom = 150f;
        private const int PenetrationRowIndex = 6;

        private static UnitInfoPanel _instance;

        private UnitSelectionController _selection;
        private GameObject _root;
        private RectTransform _panelRect;
        private Image _iconImage;
        private Text _nameLabel;
        private Text _roleLabel;
        private Text _countLabel;
        private Text _compositionLabel;
        private Image _hpFill;
        private Text _hpText;
        private Text _portraitsTitle;
        private readonly List<StatRow> _rows = new List<StatRow>(6);
        private readonly List<PortraitEntry> _portraits = new List<PortraitEntry>();
        private string _signature = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!SceneFlow.IsGameplaySceneName(scene.name) || _instance != null)
            {
                return;
            }

            GameObject host = new GameObject("Unit Info Panel");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<UnitInfoPanel>();
        }

        private void Awake()
        {
            _instance = this;

            // Blindaje por si Awake se invoca dos veces (edicion o reload).
            if (_root == null)
            {
                BuildUi();
            }

            Hide();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void LateUpdate()
        {
            if (_selection == null)
            {
                _selection = FindFirstObjectByType<UnitSelectionController>();
            }

            IReadOnlyList<UnitMovement> selected = _selection != null
                ? _selection.SelectedUnits
                : null;

            bool any = selected != null && selected.Count > 0;

            if (_root != null && _root.activeSelf != any)
            {
                _root.SetActive(any);
            }

            if (!any)
            {
                return;
            }

            // Rebuild the card only when the selection itself changes; the
            // health bars refresh every frame without recreating anything.
            string signature = BuildSignature(selected);
            if (signature != _signature)
            {
                _signature = signature;
                Rebuild(selected);
            }

            RefreshLive(selected);
        }

        private void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private static string BuildSignature(IReadOnlyList<UnitMovement> selected)
        {
            StringBuilder builder = new StringBuilder(selected.Count * 12);
            builder.Append(selected.Count);

            for (int i = 0; i < selected.Count; i++)
            {
                UnitMovement unit = selected[i];
                builder.Append('|');

                if (unit == null)
                {
                    builder.Append('-');
                    continue;
                }

                builder.Append(unit.GetInstanceID());

                UnitRuntime runtime = unit.Runtime;
                if (runtime != null && runtime.Definition != null)
                {
                    builder.Append(':').Append(runtime.Definition.GetInstanceID());
                }
            }

            return builder.ToString();
        }

        private void Rebuild(IReadOnlyList<UnitMovement> selected)
        {
            UnitDefinition primary = ResolvePrimary(selected, out Dictionary<string, int> composition);
            bool group = selected.Count > 1;

            UiIconFactory.Apply(
                _iconImage,
                primary != null ? UiIconCatalog.GetUnitIcon(primary) : null);

            _nameLabel.text = primary != null
                ? primary.DisplayName
                : (selected.Count > 0 && selected[0] != null ? selected[0].name : "Unidad");
            _roleLabel.text = GetRoleName(primary != null ? primary.Role : UnitRole.Infantry);

            if (group)
            {
                _countLabel.gameObject.SetActive(true);
                _countLabel.text = selected.Count + " unidades";
            }
            else
            {
                _countLabel.gameObject.SetActive(false);
            }

            if (group)
            {
                StringBuilder line = new StringBuilder(composition.Count * 24);
                foreach (KeyValuePair<string, int> pair in composition)
                {
                    if (line.Length > 0)
                    {
                        line.Append(" · ");
                    }

                    line.Append(pair.Key).Append(" ×").Append(pair.Value);
                }

                _compositionLabel.gameObject.SetActive(true);
                _compositionLabel.text = line.ToString();
            }
            else
            {
                _compositionLabel.gameObject.SetActive(false);
            }

            bool penetrationVisible = primary != null && primary.ArmorPenetration > 0f;
            float extra = penetrationVisible ? 22f : 0f;

            for (int i = 0; i < _rows.Count; i++)
            {
                StatRow row = _rows[i];

                if (primary == null)
                {
                    row.Root.SetActive(false);
                    continue;
                }

                bool active = i != PenetrationRowIndex || penetrationVisible;
                row.Root.SetActive(active);

                if (!active)
                {
                    continue;
                }

                switch (i)
                {
                    case 0:
                        row.Value.text = primary.AttackDamage.ToString("0.#");
                        break;
                    case 1:
                        row.Value.text = primary.AttackInterval.ToString("0.#") + " s";
                        break;
                    case 2:
                        row.Value.text =
                            (primary.AttackDamage / primary.AttackInterval).ToString("0.#");
                        break;
                    case 3:
                        row.Value.text = primary.AttackRange.ToString("0.#");
                        break;
                    case 4:
                        row.Value.text = (primary.DamageReduction * 100f).ToString("0") + "%";
                        break;
                    case 5:
                        row.Value.text = primary.MoveSpeed.ToString("0.#");
                        break;
                    case PenetrationRowIndex:
                        row.Value.text = (primary.ArmorPenetration * 100f).ToString("0") + "%";
                        break;
                }
            }

            int shown = Mathf.Min(selected.Count, MaxPortraits);
            int portraitRows = Mathf.CeilToInt(shown / (float)PortraitColumns);
            bool capped = selected.Count > MaxPortraits;

            _portraitsTitle.gameObject.SetActive(group);
            if (group)
            {
                _portraitsTitle.text = capped
                    ? "Unidades (+" + (selected.Count - MaxPortraits) + ")"
                    : "Unidades";
            }

            // Con la fila de penetracion visible, titulo y retratos bajan.
            _portraitsTitle.GetComponent<RectTransform>().anchoredPosition =
                new Vector2(12f, -(StatsBottom + 10f + extra));

            for (int i = 0; i < _portraits.Count; i++)
            {
                _portraits[i].Root.SetActive(false);
            }

            for (int i = 0; i < shown; i++)
            {
                PortraitEntry entry = GetPortrait(i);
                entry.Unit = selected[i];

                UnitRuntime runtime = entry.Unit != null ? entry.Unit.Runtime : null;
                UnitDefinition definition = runtime != null ? runtime.Definition : null;
                UiIconFactory.Apply(
                    entry.Icon,
                    definition != null ? UiIconCatalog.GetUnitIcon(definition) : null);

                int column = i % PortraitColumns;
                int rowIndex = i / PortraitColumns;

                RectTransform rect = entry.Root.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(
                    12f + column * PortraitCellWidth,
                    -(StatsBottom + 26f + extra + rowIndex * PortraitCellHeight));

                entry.Root.SetActive(true);
            }

            float height = group
                ? StatsBottom + 26f + extra + portraitRows * PortraitCellHeight + 12f
                : StatsBottom + extra + 14f;

            if (_panelRect != null)
            {
                _panelRect.sizeDelta = new Vector2(PanelWidth, height);
            }
        }

        private void RefreshLive(IReadOnlyList<UnitMovement> selected)
        {
            bool single = selected.Count == 1;
            float totalHp = 0f;
            float totalMax = 0f;
            UnitRuntime singleRuntime = null;

            for (int i = 0; i < selected.Count; i++)
            {
                UnitMovement unit = selected[i];
                if (unit == null)
                {
                    continue;
                }

                UnitRuntime runtime = unit.Runtime;
                if (runtime == null)
                {
                    continue;
                }

                totalHp += runtime.CurrentHealth;
                totalMax += runtime.MaxHealth;

                if (single)
                {
                    singleRuntime = runtime;
                }
            }

            if (single)
            {
                if (singleRuntime != null)
                {
                    _hpText.text = Mathf.RoundToInt(singleRuntime.HealthNormalized * 100f) + "%";
                    SetFill(_hpFill, singleRuntime.HealthNormalized);
                }
                else
                {
                    _hpText.text = "—";
                    SetFill(_hpFill, 0f);
                }
            }
            else
            {
                float average = totalMax > 0f ? totalHp / totalMax : 0f;
                _hpText.text = "Media " + Mathf.RoundToInt(average * 100f) + "%";
                SetFill(_hpFill, average);
            }

            for (int i = 0; i < _portraits.Count; i++)
            {
                PortraitEntry entry = _portraits[i];

                if (!entry.Root.activeSelf)
                {
                    continue;
                }

                UnitRuntime runtime = entry.Unit != null ? entry.Unit.Runtime : null;
                SetFill(entry.Fill, runtime != null ? runtime.HealthNormalized : 0f);
            }
        }

        private static void SetFill(Image fill, float normalized)
        {
            if (fill == null)
            {
                return;
            }

            normalized = Mathf.Clamp01(normalized);
            fill.fillAmount = normalized;
            fill.color = GetHealthColor(normalized);
        }

        private static Color GetHealthColor(float normalized)
        {
            if (normalized > 0.5f)
            {
                return new Color(0.25f, 0.72f, 0.33f, 1f);
            }

            if (normalized > 0.25f)
            {
                return new Color(0.87f, 0.7f, 0.2f, 1f);
            }

            return new Color(0.82f, 0.2f, 0.15f, 1f);
        }

        private static UnitDefinition ResolvePrimary(
            IReadOnlyList<UnitMovement> selected,
            out Dictionary<string, int> composition)
        {
            composition = new Dictionary<string, int>();
            int primaryCount = -1;
            UnitDefinition primary = null;

            for (int i = 0; i < selected.Count; i++)
            {
                UnitMovement unit = selected[i];
                if (unit == null)
                {
                    continue;
                }

                UnitRuntime runtime = unit.Runtime;
                UnitDefinition definition = runtime != null ? runtime.Definition : null;
                string key = definition != null
                    ? definition.DisplayName
                    : unit.name + " (desconocida)";

                int count = composition.TryGetValue(key, out int current) ? current + 1 : 1;
                composition[key] = count;

                if (count > primaryCount)
                {
                    primaryCount = count;
                    primary = definition;
                }
            }

            return primary;
        }

        private static string GetRoleName(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Infantry: return "Infantería";
                case UnitRole.Ranged: return "A distancia";
                case UnitRole.Cavalry: return "Caballería";
                case UnitRole.Siege: return "Asedio";
                default: return "Unidad";
            }
        }

        private PortraitEntry GetPortrait(int index)
        {
            while (_portraits.Count <= index)
            {
                _portraits.Add(CreatePortrait(_portraits.Count));
            }

            return _portraits[index];
        }

        private PortraitEntry CreatePortrait(int index)
        {
            GameObject go = new GameObject("Unit " + index);
            go.transform.SetParent(_root.transform, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(36f, 42f);

            Image background = go.AddComponent<Image>();
            background.color = new Color(0.13f, 0.15f, 0.19f, 1f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = UiButtonStyle.CreateIconColors();
            button.transition = Selectable.Transition.ColorTint;

            Image icon = UiIconFactory.CreateIcon(go.transform, "Icon", null);
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.offsetMin = new Vector2(2f, 7f);
            iconRect.offsetMax = new Vector2(-2f, -2f);

            // Mini health bar under the portrait.
            GameObject barObject = new GameObject("Bar");
            barObject.transform.SetParent(go.transform, false);

            RectTransform barRect = barObject.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0f, 0f);
            barRect.anchoredPosition = new Vector2(2f, 2f);
            barRect.sizeDelta = new Vector2(-4f, 4f);

            Image barBackground = barObject.AddComponent<Image>();
            barBackground.color = new Color(0.06f, 0.06f, 0.09f, 1f);

            GameObject fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(barObject.transform, false);

            RectTransform fillRect = fillObject.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fill = fillObject.AddComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.raycastTarget = false;

            PortraitEntry entry = new PortraitEntry
            {
                Root = go,
                Icon = icon,
                Fill = fill
            };

            button.onClick.AddListener(() => SelectPortrait(entry));
            return entry;
        }

        private void SelectPortrait(PortraitEntry entry)
        {
            if (_selection == null)
            {
                _selection = FindFirstObjectByType<UnitSelectionController>();
            }

            if (entry != null && entry.Unit != null && _selection != null)
            {
                _selection.SelectUnit(entry.Unit);
            }
        }

        private void BuildUi()
        {
            GameObject canvasObject = new GameObject("Unit Info Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 16;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();

            _root = new GameObject("Panel");
            _root.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = _root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(20f, 20f);
            rect.sizeDelta = new Vector2(PanelWidth, 170f);
            _panelRect = rect;

            Image background = _root.AddComponent<Image>();
            background.color = new Color(0.08f, 0.09f, 0.12f, 0.92f);

            Font font = UiFonts.Default;

            _iconImage = UiIconFactory.CreateIcon(_root.transform, "Icon", null);
            RectTransform iconRect = _iconImage.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(12f, -8f);
            iconRect.sizeDelta = new Vector2(40f, 40f);

            _nameLabel = CreateLabel(
                _root.transform,
                font,
                string.Empty,
                TextAnchor.MiddleLeft,
                15,
                FontStyle.Bold,
                new Color(0.94f, 0.92f, 0.86f, 1f));
            RectTransform nameRect = _nameLabel.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(0f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(58f, -6f);
            nameRect.sizeDelta = new Vector2(150f, 22f);

            _roleLabel = CreateLabel(
                _root.transform,
                font,
                string.Empty,
                TextAnchor.MiddleLeft,
                10,
                FontStyle.Normal,
                new Color(0.78f, 0.6f, 0.35f, 1f));
            RectTransform roleRect = _roleLabel.GetComponent<RectTransform>();
            roleRect.anchorMin = new Vector2(0f, 1f);
            roleRect.anchorMax = new Vector2(0f, 1f);
            roleRect.pivot = new Vector2(0f, 1f);
            roleRect.anchoredPosition = new Vector2(58f, -28f);
            roleRect.sizeDelta = new Vector2(150f, 14f);

            _countLabel = CreateLabel(
                _root.transform,
                font,
                string.Empty,
                TextAnchor.MiddleRight,
                12,
                FontStyle.Bold,
                new Color(1f, 0.87f, 0.55f, 1f));
            RectTransform countRect = _countLabel.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(1f, 1f);
            countRect.anchorMax = new Vector2(1f, 1f);
            countRect.pivot = new Vector2(1f, 1f);
            countRect.anchoredPosition = new Vector2(-12f, -6f);
            countRect.sizeDelta = new Vector2(100f, 18f);

            _compositionLabel = CreateLabel(
                _root.transform,
                font,
                string.Empty,
                TextAnchor.UpperLeft,
                10,
                FontStyle.Normal,
                new Color(0.85f, 0.83f, 0.75f, 1f));
            RectTransform compRect = _compositionLabel.GetComponent<RectTransform>();
            compRect.anchorMin = new Vector2(0f, 1f);
            compRect.anchorMax = new Vector2(0f, 1f);
            compRect.pivot = new Vector2(0f, 1f);
            compRect.anchoredPosition = new Vector2(12f, -44f);
            compRect.sizeDelta = new Vector2(296f, 16f);

            // Health bar (compacta, con porcentaje).
            GameObject hpObject = new GameObject("HP");
            hpObject.transform.SetParent(_root.transform, false);

            RectTransform hpRect = hpObject.AddComponent<RectTransform>();
            hpRect.anchorMin = new Vector2(0f, 1f);
            hpRect.anchorMax = new Vector2(0f, 1f);
            hpRect.pivot = new Vector2(0f, 1f);
            hpRect.anchoredPosition = new Vector2(12f, -64f);
            hpRect.sizeDelta = new Vector2(190f, 14f);

            Image hpBackground = hpObject.AddComponent<Image>();
            hpBackground.color = new Color(0.16f, 0.08f, 0.08f, 1f);

            GameObject hpFillObject = new GameObject("Fill");
            hpFillObject.transform.SetParent(hpObject.transform, false);

            RectTransform hpFillRect = hpFillObject.AddComponent<RectTransform>();
            hpFillRect.anchorMin = Vector2.zero;
            hpFillRect.anchorMax = Vector2.one;
            hpFillRect.offsetMin = new Vector2(2f, 2f);
            hpFillRect.offsetMax = new Vector2(-2f, -2f);

            _hpFill = hpFillObject.AddComponent<Image>();
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.fillOrigin = 0;
            _hpFill.raycastTarget = false;
            _hpFill.color = GetHealthColor(1f);

            _hpText = CreateLabel(
                hpObject.transform,
                font,
                string.Empty,
                TextAnchor.MiddleCenter,
                11,
                FontStyle.Bold,
                Color.white);
            RectTransform hpTextRect = _hpText.GetComponent<RectTransform>();
            hpTextRect.anchorMin = Vector2.zero;
            hpTextRect.anchorMax = Vector2.one;
            hpTextRect.offsetMin = Vector2.zero;
            hpTextRect.offsetMax = Vector2.zero;

            // Stat rows (dos columnas, valor pegado a la etiqueta).
            string[] rowTitles =
            {
                "Daño", "Cadencia", "DPS", "Alcance", "Armadura", "Velocidad"
            };

            for (int i = 0; i < rowTitles.Length; i++)
            {
                int column = i % 2;
                int rowIndex = i / 2;

                GameObject rowObject = new GameObject("Row " + rowTitles[i]);
                rowObject.transform.SetParent(_root.transform, false);

                RectTransform rowRect = rowObject.AddComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(0f, 1f);
                rowRect.pivot = new Vector2(0f, 1f);
                rowRect.anchoredPosition = new Vector2(
                    12f + column * 154f,
                    -(84f + rowIndex * 22f));
                rowRect.sizeDelta = new Vector2(140f, 20f);

                Text rowLabel = CreateLabel(
                    rowObject.transform,
                    font,
                    rowTitles[i] + ":",
                    TextAnchor.MiddleLeft,
                    12,
                    FontStyle.Normal,
                    new Color(0.78f, 0.6f, 0.35f, 1f));
                RectTransform rowLabelRect = rowLabel.GetComponent<RectTransform>();
                rowLabelRect.anchorMin = new Vector2(0f, 0.5f);
                rowLabelRect.anchorMax = new Vector2(0f, 0.5f);
                rowLabelRect.pivot = new Vector2(0f, 0.5f);
                rowLabelRect.anchoredPosition = Vector2.zero;
                rowLabelRect.sizeDelta = new Vector2(82f, 18f);

                Text rowValue = CreateLabel(
                    rowObject.transform,
                    font,
                    "—",
                    TextAnchor.MiddleLeft,
                    12,
                    FontStyle.Bold,
                    new Color(0.94f, 0.92f, 0.86f, 1f));
                RectTransform rowValueRect = rowValue.GetComponent<RectTransform>();
                rowValueRect.anchorMin = new Vector2(0f, 0.5f);
                rowValueRect.anchorMax = new Vector2(0f, 0.5f);
                rowValueRect.pivot = new Vector2(0f, 0.5f);
                rowValueRect.anchoredPosition = new Vector2(82f, 0f);
                rowValueRect.sizeDelta = new Vector2(58f, 18f);

                _rows.Add(new StatRow
                {
                    Root = rowObject,
                    Value = rowValue
                });
            }

            // Fila condicional: penetracion de armadura (columna derecha,
            // debajo de Velocidad). Solo se muestra si la unidad la tiene.
            GameObject penObject = new GameObject("Row Penetración");
            penObject.transform.SetParent(_root.transform, false);

            RectTransform penRect = penObject.AddComponent<RectTransform>();
            penRect.anchorMin = new Vector2(0f, 1f);
            penRect.anchorMax = new Vector2(0f, 1f);
            penRect.pivot = new Vector2(0f, 1f);
            penRect.anchoredPosition = new Vector2(166f, -150f);
            penRect.sizeDelta = new Vector2(140f, 20f);

            Text penLabel = CreateLabel(
                penObject.transform,
                font,
                "Penetración:",
                TextAnchor.MiddleLeft,
                12,
                FontStyle.Normal,
                new Color(0.78f, 0.6f, 0.35f, 1f));
            RectTransform penLabelRect = penLabel.GetComponent<RectTransform>();
            penLabelRect.anchorMin = new Vector2(0f, 0.5f);
            penLabelRect.anchorMax = new Vector2(0f, 0.5f);
            penLabelRect.pivot = new Vector2(0f, 0.5f);
            penLabelRect.anchoredPosition = Vector2.zero;
            penLabelRect.sizeDelta = new Vector2(82f, 18f);

            Text penValue = CreateLabel(
                penObject.transform,
                font,
                "—",
                TextAnchor.MiddleLeft,
                12,
                FontStyle.Bold,
                new Color(0.94f, 0.92f, 0.86f, 1f));
            RectTransform penValueRect = penValue.GetComponent<RectTransform>();
            penValueRect.anchorMin = new Vector2(0f, 0.5f);
            penValueRect.anchorMax = new Vector2(0f, 0.5f);
            penValueRect.pivot = new Vector2(0f, 0.5f);
            penValueRect.anchoredPosition = new Vector2(82f, 0f);
            penValueRect.sizeDelta = new Vector2(58f, 18f);

            _rows.Add(new StatRow
            {
                Root = penObject,
                Value = penValue
            });

            _portraitsTitle = CreateLabel(
                _root.transform,
                font,
                "Unidades",
                TextAnchor.MiddleLeft,
                11,
                FontStyle.Bold,
                new Color(0.78f, 0.6f, 0.35f, 1f));
            RectTransform titleRect = _portraitsTitle.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(12f, -(StatsBottom + 10f));
            titleRect.sizeDelta = new Vector2(180f, 16f);
        }

        private static Text CreateLabel(
            Transform parent,
            Font font,
            string content,
            TextAnchor alignment,
            int fontSize,
            FontStyle style,
            Color color)
        {
            GameObject go = new GameObject("Label");
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200f, 20f);

            Text label = go.AddComponent<Text>();
            label.font = font;
            label.text = content;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>One label/value pair of the stats block.</summary>
        private sealed class StatRow
        {
            public GameObject Root;
            public Text Value;
        }

        /// <summary>One selectable portrait with its mini health bar.</summary>
        private sealed class PortraitEntry
        {
            public GameObject Root;
            public Image Icon;
            public Image Fill;
            public UnitMovement Unit;
        }
    }
}
