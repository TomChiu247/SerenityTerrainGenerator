using UnityEngine;
using System;

namespace GoblinsInteractive.IsometricTilemapAutoCollider
{
    public partial class IsometricTilemapAutoCollider : MonoBehaviour
    {
        [Serializable]
        public enum TilemapLevelDetectionMode
        {
            TilemapSortingOrderInLayer,
            HigherLevelsAtBottomOfHierarchy,
            HigherLevelsAtBottomOfHierarchySkipOne,
        }
    }
}