using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Inspector-friendly tuning for the unit visuals built by
    /// PlacementDemoBuilder. Each part can override the automatically
    /// computed scale and position with absolute values, so artists can
    /// adjust helmets, armor and weapons without touching code.
    /// </summary>
    [CreateAssetMenu(
        fileName = "UnitVisualTuning",
        menuName = "ProjectoRTS/Units/Unit Visual Tuning")]
    public sealed class UnitVisualTuning : ScriptableObject
    {
        [Serializable]
        public sealed class Part
        {
            public bool overrideScale;
            public Vector3 scale = Vector3.one;
            public bool overridePosition;
            public Vector3 position = Vector3.zero;
            public bool overrideRotation;
            public Vector3 rotation = Vector3.zero;
        }

        [Serializable]
        public sealed class UnitEntry
        {
            public string unitName;
            public string clothesModel;
            public Part body = new Part();
            public Part armor = new Part();
            public Part helmet = new Part();
            public Part shield = new Part();
            public Part spear = new Part();
            public Part bow = new Part();
            public Part quiver = new Part();
            public Part buckler = new Part();
        }

        public List<UnitEntry> units = new List<UnitEntry>();
    }
}
