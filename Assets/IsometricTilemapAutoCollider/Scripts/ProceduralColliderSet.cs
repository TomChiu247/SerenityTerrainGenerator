using UnityEngine;

namespace GoblinsInteractive.ProceduralIsometricMapGenerator
{
    [System.Serializable, CreateAssetMenu(menuName = "Isometric Tilemap Auto Collider/Collider Set")]
    public partial class ProceduralColliderSet : ScriptableObject
    {
        public ColliderAndColliderType[] ColliderAndColliderTypes = default;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Awake()
        {
            foreach (var tile in ColliderAndColliderTypes)
            {
                Debug.Assert(tile.TileType != ColliderType.None);
            }
        }
#endif
    }
}