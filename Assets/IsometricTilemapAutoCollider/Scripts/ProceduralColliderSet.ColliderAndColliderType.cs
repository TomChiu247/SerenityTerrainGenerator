using UnityEngine;
using UnityEngine.Tilemaps;

namespace GoblinsInteractive.ProceduralIsometricMapGenerator
{
    public partial class ProceduralColliderSet : ScriptableObject
    {
        [System.Serializable]
        public struct ColliderAndColliderType
        {
            public Tile Tile;
            public ColliderType TileType;
        }
    }
}