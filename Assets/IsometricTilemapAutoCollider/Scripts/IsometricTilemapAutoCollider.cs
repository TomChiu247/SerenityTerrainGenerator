using UnityEngine.Tilemaps;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using GoblinsInteractive.ProceduralIsometricMapGenerator;
using System.Linq;

namespace GoblinsInteractive.IsometricTilemapAutoCollider
{
    public partial class IsometricTilemapAutoCollider : MonoBehaviour
    {
        [SerializeField, Tooltip("The grid of the tilemaps. You can leave this empty if this game object is also the grid.")] Grid _grid = default;
        [SerializeField, Tooltip("Level detection mode for tilemaps. For more details look at the documentation")]
        TilemapLevelDetectionMode _levelDetectionMode = TilemapLevelDetectionMode.HigherLevelsAtBottomOfHierarchy;

        [SerializeField, Tooltip("Additional colliders to add to the outer border of the map." +
            " Needed to stop a character from teleporting outside the map.")]
        int _additionalInsetAmount = 3;

        [SerializeField] ProceduralColliderSet _colliderSet = default;

        [SerializeField, Tooltip("Write an existing layer.")]
        string _tilemapColliderLayerName = "Default";

        [SerializeField, Tooltip("Sprite(s) of stairs or elevation that is rising towards the top of the grid. \n" +
            "Leave this empty if there there is no such tile in your tilemap")]
        Sprite[] _stairsTowardsTop = new Sprite[1];
        [SerializeField, Tooltip("Sprite(s) of stairs or elevation that is rising towards the left of the grid. \n" +
            "Leave this empty if there there is no such tile in your tilemap")]
        Sprite[] _stairsTowardsLeft = new Sprite[1];
        [SerializeField, Tooltip("Sprite(s) of stairs or elevation that is rising towards the bottom of the grid. \n" +
            "Leave this empty if there there is no such tile in your tilemap")]
        Sprite[] _stairsTowardsBottom = new Sprite[1];
        [SerializeField, Tooltip("Sprite(s) of stairs or elevation that is rising towards the right of the grid. \n" +
            "Leave this empty if there there is no such tile in your tilemap")]
        Sprite[] _stairsTowardsRight = new Sprite[1];

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField, Tooltip("Enables debug statements for this component")]
        bool _debug = true;
#endif

        /// <summary>
        /// used for not putting another stair nearby and adding colliders. Contains position on map, not noise matrix
        /// </summary>
        protected List<Vector2Int> _topStairPositions = new List<Vector2Int>();
        /// <summary>
        /// used for not putting another stair nearby and adding colliders. Contains position on map, not noise matrix
        /// </summary>
        protected List<Vector2Int> _rightStairPositions = new List<Vector2Int>();
        /// <summary>
        /// used for not putting another stair nearby and adding colliders. Contains position on map, not noise matrix
        /// </summary>
        protected List<Vector2Int> _bottomStairPositions = new List<Vector2Int>();
        /// <summary>
        /// used for not putting another stair nearby and adding colliders. Contains position on map, not noise matrix
        /// </summary>
        protected List<Vector2Int> _leftStairPositions = new List<Vector2Int>();

        int[,] _noiseMatrix;
        int _levelCount;
        Vector2Int _xBounds, _yBounds;
        Tilemap _colliderTilemap;
        Vector3 _tilemapAnchor = default;

        public void Start()
        {
            BuildColliders();
            Destroy(_colliderTilemap.GetComponent<TilemapRenderer>());
        }

        public void BuildColliders()
        {
            if (_grid == null)
            {
                gameObject.TryGetComponent<Grid>(out _grid);
            }

            GenerateNoiseFromTilemaps();
            CreateColliders();
            _noiseMatrix = null;//release memory
        }

        /// <summary>
        /// Returns the level of the x,y position on noise matrix.
        /// <br></br>
        /// To get the level of x,y position on grid, use GetVisualLevelFromCoordinates
        /// </summary>
        public int GetLevelFromCoordinates(int x, int y)
        {
            if (x >= _xBounds.y || x < _xBounds.x ||
                y >= _yBounds.y || y < _yBounds.x)
            {
                return -1;
            }
            return _noiseMatrix[x - _xBounds.x, y - _yBounds.x];
        }

        /// <summary>
        /// Returns whether or not the center of the tile(i.e. excluding the cliffs) are FULLY obstructed by higher levels. 
        /// <br></br>
        /// Use IsCentralFullyVisible to check if the tile is half invisible
        /// <br></br>
        /// If the central tile is half visible and half invisible returns true
        /// <br></br>
        /// Use x,y values of the grid. Do NOT use x,y values of the noise.
        /// </summary>
        protected bool IsCentralFullyInvisible(int x, int y)
        {
            for (int levelOffset = _levelCount - 1; levelOffset > 0; levelOffset--)
            {
                var bottomLeftLevel = GetLevelFromCoordinates(x - levelOffset, y - levelOffset);
                var topOfBottomLeftLevel = GetLevelFromCoordinates(x - levelOffset, y - levelOffset + 1);
                var rightOfBottomLeftLevel = GetLevelFromCoordinates(x - levelOffset + 1, y - levelOffset);
                if (bottomLeftLevel >= levelOffset ||
                    (topOfBottomLeftLevel >= levelOffset && rightOfBottomLeftLevel >= levelOffset))//the offset tile is obstructing the central tile of x,y
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if there is a visible cliff on top of x,y coordinates of the grid.
        /// </summary>
        protected bool IsThereVisibleCliffOnTop(int x, int y)
        {
            for (int levelOffset = _levelCount - 1; levelOffset >= 0; levelOffset--)
            {
                var level = GetLevelFromCoordinates(x - levelOffset, y - levelOffset);
                var topLevel = GetLevelFromCoordinates(x - levelOffset, y - levelOffset + 1);

                if (level <= levelOffset)
                {
                    if (topLevel > levelOffset)
                    {
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if there is a visible cliff on right of x,y coordinates of the grid.
        /// </summary>
        protected bool IsThereVisibleCliffOnRight(int x, int y)
        {
            for (int levelOffset = _levelCount - 1; levelOffset >= 0; levelOffset--)
            {
                var level = GetLevelFromCoordinates(x - levelOffset, y - levelOffset);
                var rightLevel = GetLevelFromCoordinates(x - levelOffset + 1, y - levelOffset);

                if (level <= levelOffset)
                {
                    if (rightLevel > levelOffset)
                    {
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns whether or not the top of the tile is half visible. Only call this if top cliff is visible.
        /// </summary>
        protected bool IsTopCliffHalfVisible(int x, int y)
        {
            for (int levelOffset = _levelCount - 2; levelOffset >= 0; levelOffset--)
            {
                var leftLevel = GetLevelFromCoordinates(x - levelOffset - 1, y - levelOffset);
                if (leftLevel > levelOffset + 1)
                {
                    return false;
                }

                if (leftLevel == levelOffset + 1)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns whether or not the right side of tile is half visible. Only call this if right cliff is visible.
        /// </summary>
        protected bool IsRightCliffHalfVisible(int x, int y)
        {
            for (int levelOffset = _levelCount - 1; levelOffset >= 0; levelOffset--)
            {
                var bottomLevel = GetLevelFromCoordinates(x - levelOffset, y - levelOffset - 1);

                if (bottomLevel > levelOffset + 1)
                {
                    return false;
                }

                if (bottomLevel == levelOffset + 1)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if there is a visible edge on the left side of the tile.
        /// </summary>
        protected bool IsThereVisibleEdgeOnLeft(int x, int y)
        {
            for (int levelOffset = _levelCount - 1; levelOffset >= 0; levelOffset--)
            {
                var level = GetLevelFromCoordinates(x - levelOffset, y - levelOffset);
                var leftLevel = GetLevelFromCoordinates(x - levelOffset - 1, y - levelOffset);

                if (level == levelOffset)
                {
                    if (level > leftLevel)
                    {
                        return true;
                    }
                    if (level <= leftLevel)
                    {
                        return false;
                    }
                }
                if (level > levelOffset)
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if there is a visible edge on the bottom side of the tile.
        /// </summary>
        protected bool IsThereVisibleEdgeOnBottom(int x, int y)
        {
            for (int levelOffset = _levelCount - 1; levelOffset >= 0; levelOffset--)
            {
                var level = GetLevelFromCoordinates(x - levelOffset, y - levelOffset);
                var bottomLevel = GetLevelFromCoordinates(x - levelOffset, y - levelOffset - 1);

                if (level == levelOffset)
                {
                    if (level > bottomLevel)
                    {
                        return true;
                    }
                    if (level <= bottomLevel)
                    {
                        return false;
                    }
                }
                if (level > levelOffset)
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if there is a visible edge on the right side of the tile.
        /// </summary>
        protected bool IsThereVisibleEdgeOnRight(int x, int y)
        {
            for (int levelOffset = _levelCount - 1; levelOffset >= 0; levelOffset--)
            {
                var level = GetLevelFromCoordinates(x - levelOffset, y - levelOffset);
                var rightLevel = GetLevelFromCoordinates(x - levelOffset + 1, y - levelOffset);

                if (level == levelOffset)
                {
                    if (level > rightLevel)
                    {
                        return true;
                    }
                    if (level <= rightLevel)
                    {
                        return false;
                    }
                }
                if (level > levelOffset)
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if there is a visible edge on the top side of the tile.
        /// </summary>
        protected bool IsThereVisibleEdgeOnTop(int x, int y)
        {
            for (int levelOffset = _levelCount - 1; levelOffset >= 0; levelOffset--)
            {
                var level = GetLevelFromCoordinates(x - levelOffset, y - levelOffset);
                var topLevel = GetLevelFromCoordinates(x - levelOffset, y - levelOffset + 1);

                if (level == levelOffset)
                {
                    if (level > topLevel)
                    {
                        return true;
                    }
                    if (level <= topLevel)
                    {
                        return false;
                    }
                }
                if (level > levelOffset)
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if there is a tile on the position with greater or equal level than currentLevel
        /// </summary>
        protected bool NoiseMatrixHasGreaterOrEqualTile(int x, int y, int currentLevel)
        {
            if (x < _xBounds.x || y < _yBounds.x ||
                x >= _xBounds.y ||
                y >= _yBounds.y)
            {
                return false;
            }
            return GetLevelFromCoordinates(x, y) >= currentLevel;
        }

        private void GenerateNoiseFromTilemaps()
        {
            _levelCount = -1;
            _xBounds = new Vector2Int();
            _yBounds = new Vector2Int();
            _tilemapAnchor = default;
            _topStairPositions = new List<Vector2Int>();
            _rightStairPositions = new List<Vector2Int>();
            _bottomStairPositions = new List<Vector2Int>();
            _leftStairPositions = new List<Vector2Int>();

            var tilemaps = _grid.GetComponentsInChildren<Tilemap>();
            for (int i = 0; i < tilemaps.Length; i++)
            {
                if (tilemaps[i].name == "AutoCollider")
                {
                    DestroyImmediate(tilemaps[i].gameObject);
                    tilemaps = tilemaps.Where((source, index) => index != i).ToArray();
                    break;
                }
            }

            if (tilemaps.Length == 0)
            {
                return;
            }

            //find the boundries of all tilemaps
            foreach (var tilemap in tilemaps)
            {
                if (tilemap.cellBounds.xMin < _xBounds.x)
                {
                    _xBounds.x = tilemap.cellBounds.xMin;
                }
                if (tilemap.cellBounds.xMax > _xBounds.y)
                {
                    _xBounds.y = tilemap.cellBounds.xMax;
                }
                if (tilemap.cellBounds.yMin < _yBounds.x)
                {
                    _yBounds.x = tilemap.cellBounds.yMin;
                }
                if (tilemap.cellBounds.yMax > _yBounds.y)
                {
                    _yBounds.y = tilemap.cellBounds.yMax;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD //find tile anchor errors
                if (_tilemapAnchor == Vector3.zero)
                {
                    _tilemapAnchor = tilemap.tileAnchor;
                }
                if (tilemap.tileAnchor != _tilemapAnchor)
                {
                    if (_debug)
                    {
                        Debug.LogWarning("Tile anchor of " + tilemap.name + " is different from the other tile anchors, this can cause collider displacement errors.");
                    }
                }
#endif
                var tilemapSortOrder = tilemap.GetComponent<TilemapRenderer>().sortingOrder;
                if (tilemapSortOrder >= _levelCount)
                {
                    _levelCount = tilemapSortOrder + 1;
                }
            }

            //add levelCount to offset the lower part of the map for bases higher levels
            var _mapSize = new Vector2Int(_xBounds.y - _xBounds.x + _levelCount, _yBounds.y - _yBounds.x + _levelCount);

            _noiseMatrix = new int[_mapSize.x, _mapSize.y];//will be used for setting the colliders
            //initialize noise matrix
            for (int x = 0; x < _mapSize.x; x++)
            {
                for (int y = 0; y < _mapSize.y; y++)
                {
                    _noiseMatrix[x, y] = -1;
                }
            }

            switch (_levelDetectionMode)
            {
                case TilemapLevelDetectionMode.TilemapSortingOrderInLayer:
                    GenerateNoiseBasedOnSortingOrder(tilemaps);
                    break;
                case TilemapLevelDetectionMode.HigherLevelsAtBottomOfHierarchy:
                    GenerateNoiseBasedOnOrderInHierarchy(tilemaps, false);
                    break;
                case TilemapLevelDetectionMode.HigherLevelsAtBottomOfHierarchySkipOne:
                    GenerateNoiseBasedOnOrderInHierarchy(tilemaps, true);
                    break;
                default:
                    break;
            }

            //Debug.Log(tilemaps[0].HasTile(new Vector3Int(0, 0, 0));
        }

        private void GenerateNoiseBasedOnSortingOrder(Tilemap[] tilemaps)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD //find tilemap sorting order errors
            if (tilemaps[0].GetComponent<TilemapRenderer>().sortingOrder != 0)
            {
                if (_debug)
                {
                    Debug.LogWarning("Sorting order of the first tilemap is not 0. This can cause collider displacement errors.");
                }
            }
#endif
            //set the noise values to sorting order of the tiles.
            foreach (var tilemap in tilemaps)
            {
                var level = tilemap.GetComponent<TilemapRenderer>().sortingOrder;
                GenerateNoise(tilemap, level);
            }
        }

        private void GenerateNoiseBasedOnOrderInHierarchy(Tilemap[] tilemaps, bool skipOne)
        {
            //set the noise values to hierarchial order of the tilemaps.
            for (int i = 0; i < tilemaps.Length; i++)
            {
                if (skipOne && i % 2 == 1)
                {
                    continue;
                }
                var tilemap = tilemaps[i];
                var level = skipOne ? i / 2 : i;

                GenerateNoise(tilemap, level);
            }
        }

        private void GenerateNoise(Tilemap tilemap, int level)
        {
            var position = new Vector3Int();
            Sprite sprite;

            for (int x = tilemap.cellBounds.xMin; x < tilemap.cellBounds.xMax; x++)
            {
                position.x = x;
                for (int y = tilemap.cellBounds.yMin; y < tilemap.cellBounds.yMax; y++)
                {
                    position.y = y;
                    if (tilemap.HasTile(position))
                    {
                        sprite = tilemap.GetSprite(position);
                        if (level >= 1)
                        {
                            if (_stairsTowardsTop.Contains(sprite))
                            {
                                _topStairPositions.Add((Vector2Int)position);
                            }
                            else if (_stairsTowardsLeft.Contains(sprite))
                            {
                                _leftStairPositions.Add((Vector2Int)position);
                            }
                            else if (_stairsTowardsBottom.Contains(sprite))
                            {
                                _bottomStairPositions.Add((Vector2Int)position);
                            }
                            else if (_stairsTowardsRight.Contains(sprite))
                            {
                                _rightStairPositions.Add((Vector2Int)position);
                            }
                            else
                            {
                                _noiseMatrix[x - _xBounds.x - (level - 1), y - _yBounds.x - (level - 1)] = level;
                            }
                        }
                        else
                        {
                            _noiseMatrix[x - _xBounds.x, y - _yBounds.x] = level;
                        }
                    }
                }
            }
        }

        private void CreateColliders()
        {
            CreateColliderTilemap();

            #region finding collider tiles of current biome
            //find the collider tiles - also cache them
            var colliderAndColliderTypes = _colliderSet.ColliderAndColliderTypes;

            var centralCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.Central).Tile;
            var cliffTopCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffTop).Tile;
            var cliffRightCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffRight).Tile;
            var cliffDoubleCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDouble).Tile;
            var stairsTopCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsTop).Tile;
            var stairsRightCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsRight).Tile;
            var cliffHalfTopCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffHalfTop).Tile;
            var cliffHalfRightCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffHalfRight).Tile;
            var edgeDoubleCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.EdgeDouble).Tile;
            var edgeTopCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.EdgeTop).Tile;
            var edgeRightCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.EdgeRight).Tile;
            var cliffTopEdgeRightCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffTopEdgeRight).Tile;
            var cliffRightEdgeTopCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffRightEdgeTop).Tile;
            var cliffDoubleWithRightCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDoubleWithRightCutout).Tile;
            var cliffDoubleWithTopCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDoubleWithLeftCutout).Tile;
            var cliffDoubleWithDoubleCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDoubleWithDoubleCutout).Tile;
            var cliffHalfTopEdgeRightCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffHalfTopEdgeRight).Tile;
            var cliffHalfRightEdgeTopCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffHalfRightEdgeTop).Tile;

            var cliffDoubleWithTopStairCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDoubleWithTopStairCutout).Tile;
            var cliffDoubleWithRightCutoutWithTopStairCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDoubleWithRightCutoutWithTopStairCutout).Tile;
            var cliffRightWithRightStairCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffRightWithRightStairCutout).Tile;
            var cliffTopEdgeRightWithTopStairCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffTopEdgeRightWithTopStairCutout).Tile;
            var cliffTopWithTopStairCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffTopWithTopStairCutout).Tile;
            var stairsRightBottomSideOnlyCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsRightBottomSideOnly).Tile;
            var stairsRightTopSideOnlyCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsRightTopSideOnly).Tile;
            var stairsTopBottomSideOnlyCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsTopBottomSideOnly).Tile;
            var stairsTopTopSideOnlyCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsTopTopSideOnly).Tile;
            var stairsTopWithRightCliffCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsTopWithRightCliff).Tile;
            var stairsTopWithRightCliffTopSideOnlyCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsTopWithRightCliffTopSideOnly).Tile;

            var cliffDoubleWithCentralCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDoubleWithCentral).Tile;
            var cliffDoubleEdgeRightWithDoubleCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDoubleEdgeRightWithDoubleCutout).Tile;
            var cliffDoubleEdgeTopWithDoubleCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDoubleEdgeTopWithDoubleCutout).Tile;
            var cliffDoubleEdgeDoubleWithDoubleCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDoubleEdgeDoubleWithDoubleCutout).Tile;
            var cliffDoubleEdgeRightWithRightCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDoubleEdgeRightWithRightCutout).Tile;
            var cliffDoubleEdgeTopWithTopCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDoubleEdgeTopWithTopCutout).Tile;
            var cliffHalfRightEdgeDoubleCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffHalfRightEdgeDouble).Tile;
            var cliffHalfRightEdgeRightCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffHalfRightEdgeRight).Tile;
            var cliffHalfTopEdgeDoubleCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffHalfTopEdgeDouble).Tile;
            var cliffHalfTopEdgeTopCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffHalfTopEdgeTop).Tile;
            var cliffRightWithCentralCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffRightWithCentral).Tile;
            var cliffTopWithCentralCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffTopWithCentral).Tile;
            var stairsRightWithCliffTopCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsRightWithCliffTop).Tile;
            var stairsRightWithCliffTopTopSideOnlyCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsRightWithCliffTopTopSideOnly).Tile;
            var cliffDoubleWithTopCutoutWithRightStairCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDoubleWithTopCutoutWithRightStairCutout).Tile;
            var cliffRightEdgeTopWithStairRightCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffRightEdgeTopWithRightStairCutout).Tile;
            var cliffDoubleWithRightStairCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDoubleWithRightStairCutout).Tile;

            var stairsBottomCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsBottom).Tile;
            var stairsBottomBottomSideOnlyCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsBottomBottomSideOnly).Tile;
            var stairsBottomTopSideOnlyCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsBottomTopSideOnly).Tile;
            var stairsBottomWİthBottomCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsBottomWithBottomCutout).Tile;
            var stairsBottomWithBottomCutoutBottomSideOnlyCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsBottomWithBottomCutoutBottomSideOnly).Tile;

            var stairsLeftCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsLeft).Tile;
            var stairsLeftBottomSideOnlyCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsLeftBottomSideOnly).Tile;
            var stairsLeftTopSideOnlyCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsLeftTopSideOnly).Tile;
            var stairsLeftWithBottomCutoutCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsLeftWithBottomCutout).Tile;
            var stairsLeftWithBottomCutoutBottomSideOnlyCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.StairsLeftWithBottomCutoutBottomSideOnly).Tile;

            var cliffDoubleWithTopCutoutWithCentralCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDoubleWithTopCutoutWithCentral).Tile;
            var cliffDoubleWithRightCutoutWithCentralCollider = colliderAndColliderTypes.First(tile => tile.TileType == ProceduralColliderSet.ColliderType.CliffDoubleWithRightCutoutWithCentral).Tile;
            #endregion

            var listLock = new System.Object();//used as lock for multithreading
            var allPositions = new List<Vector3Int>();
            var allTiles = new List<Tile>();

            var iterateFromX = _xBounds.x - _additionalInsetAmount;
            var iterateFromY = _yBounds.x - _additionalInsetAmount;
            var iterateToX = _xBounds.y + _additionalInsetAmount;
            var iterateToY = _yBounds.y + _additionalInsetAmount;

            Parallel.For(iterateFromX, iterateToX, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            x =>//for (int x = iterateFromX; x < iterateToX; x++)
            {
                var positions = new List<Vector3Int>();
                var tiles = new List<Tile>();

                var position = new Vector3Int(x, 0, 0);

                bool rightHasEdge, topHasEdge, isTopCliffHalfVisible, isRightCliffHalfVisible, isThereVisibleCliffOnTop, isThereVisibleCliffOnRight;
                bool hasTopStairs, leftHasTopStairs, rightHasTopStairs, hasRightStairs, bottomHasRightStairs, topHasRightStairs;
                bool hasBottomStairs, leftHasBottomStairs, rightHasBottomStairs, hasLeftStairs, bottomHasLeftStairs, topHasLeftStairs;

                for (int y = iterateFromY; y < iterateToY; y++)
                {
                    position.y = y;

                    isThereVisibleCliffOnTop = IsThereVisibleCliffOnTop(x, y);
                    isThereVisibleCliffOnRight = IsThereVisibleCliffOnRight(x, y);

                    isTopCliffHalfVisible = IsTopCliffHalfVisible(x, y);
                    isRightCliffHalfVisible = IsRightCliffHalfVisible(x, y);

                    rightHasEdge = IsThereVisibleEdgeOnRight(x, y) && !_leftStairPositions.Contains(new Vector2Int(x, y - 1));
                    topHasEdge = IsThereVisibleEdgeOnTop(x, y) && !_bottomStairPositions.Contains(new Vector2Int(x - 1, y));

                    hasTopStairs = _topStairPositions.Contains(new Vector2Int(x, y));
                    leftHasTopStairs = _topStairPositions.Contains(new Vector2Int(x - 1, y));
                    rightHasTopStairs = _topStairPositions.Contains(new Vector2Int(x + 1, y));

                    hasRightStairs = _rightStairPositions.Contains(new Vector2Int(x, y));
                    bottomHasRightStairs = _rightStairPositions.Contains(new Vector2Int(x, y - 1));
                    topHasRightStairs = _rightStairPositions.Contains(new Vector2Int(x, y + 1));

                    hasBottomStairs = _bottomStairPositions.Contains(new Vector2Int(x - 1, y));
                    leftHasBottomStairs = _bottomStairPositions.Contains(new Vector2Int(x - 2, y));
                    rightHasBottomStairs = _bottomStairPositions.Contains(new Vector2Int(x, y));

                    hasLeftStairs = _leftStairPositions.Contains(new Vector2Int(x, y - 1));
                    bottomHasLeftStairs = _leftStairPositions.Contains(new Vector2Int(x, y - 2));
                    topHasLeftStairs = _leftStairPositions.Contains(new Vector2Int(x, y));

                    if (GetLevelFromCoordinates(x + 1, y + 1) == -1 && !IsCentralFullyInvisible(x + 1, y + 1))//prevents wrong colliders at the boundries of the map
                    {
                        if (isThereVisibleCliffOnTop)
                        {
                            if (!isThereVisibleCliffOnRight && NoiseMatrixHasGreaterOrEqualTile(x + 1, y, GetLevelFromCoordinates(x, y)))
                            {
                                isRightCliffHalfVisible = true;
                            }

                            isThereVisibleCliffOnRight = true;
                        }
                        else if (isThereVisibleCliffOnRight)
                        {
                            if (!isThereVisibleCliffOnTop && NoiseMatrixHasGreaterOrEqualTile(x, y + 1, GetLevelFromCoordinates(x, y)))
                            {
                                isTopCliffHalfVisible = true;
                            }

                            isThereVisibleCliffOnTop = true;
                        }
                    }

                    if (isThereVisibleCliffOnTop)//top cliff
                    {
                        if (isThereVisibleCliffOnRight)//right cliff
                        {
                            if (isTopCliffHalfVisible)//half top cliff
                            {
                                if (isRightCliffHalfVisible)//double cliff with double cutout
                                {
                                    if (topHasEdge)//double cliff with double cutout + top edge
                                    {
                                        if (rightHasEdge)//double cliff with double cutout + double edge
                                        {
                                            positions.Add(position);
                                            tiles.Add(cliffDoubleEdgeDoubleWithDoubleCutoutCollider);
                                        }
                                        else//double cliff with double cutout + top edge
                                        {
                                            positions.Add(position);
                                            tiles.Add(cliffDoubleEdgeTopWithDoubleCutoutCollider);
                                        }
                                    }
                                    else//double cliff with double cutout 
                                    {
                                        if (rightHasEdge)//double cliff with double cutout + right edge
                                        {
                                            positions.Add(position);
                                            tiles.Add(cliffDoubleEdgeRightWithDoubleCutoutCollider);
                                        }
                                        else//double cliff with double cutout
                                        {
                                            positions.Add(position);
                                            tiles.Add(cliffDoubleWithDoubleCutoutCollider);
                                        }
                                    }
                                }
                                else//right cliff
                                {
                                    if (topHasEdge)//cliff double with top cutout + top edge
                                    {
                                        positions.Add(position);
                                        tiles.Add(cliffDoubleEdgeTopWithTopCutoutCollider);
                                    }
                                    else//cliff double with top cutout
                                    {
                                        if (bottomHasRightStairs)
                                        {
                                            positions.Add(position);
                                            tiles.Add(cliffDoubleWithTopCutoutWithRightStairCutoutCollider);
                                        }
                                        else
                                        {
                                            if (UseCentralCollider(x, y))
                                            {
                                                positions.Add(position);
                                                tiles.Add(cliffDoubleWithTopCutoutWithCentralCollider);
                                            }
                                            else
                                            {
                                                positions.Add(position);
                                                tiles.Add(cliffDoubleWithTopCutoutCollider);
                                            }
                                        }
                                    }
                                }
                            }
                            else//top cliff
                            {
                                if (isRightCliffHalfVisible)//half right cliff
                                {
                                    if (rightHasEdge)//cliff double with right cutout + right edge
                                    {
                                        positions.Add(position);
                                        tiles.Add(cliffDoubleEdgeRightWithRightCutoutCollider);
                                    }
                                    else//cliff double with right cutout
                                    {
                                        if (leftHasTopStairs)
                                        {
                                            positions.Add(position);
                                            tiles.Add(cliffDoubleWithRightCutoutWithTopStairCutoutCollider);
                                        }
                                        else
                                        {
                                            if (UseCentralCollider(x, y))
                                            {
                                                positions.Add(position);
                                                tiles.Add(cliffDoubleWithRightCutoutWithCentralCollider);
                                            }
                                            else
                                            {
                                                positions.Add(position);
                                                tiles.Add(cliffDoubleWithRightCutoutCollider);
                                            }
                                        }
                                    }
                                }
                                else//right cliff
                                {
                                    if (UseCentralCollider(x, y))
                                    {
                                        positions.Add(position);
                                        tiles.Add(cliffDoubleWithCentralCollider);
                                    }
                                    else
                                    {
                                        if (hasTopStairs)
                                        {
                                            if (leftHasTopStairs)
                                            {
                                                positions.Add(position);
                                                tiles.Add(stairsTopWithRightCliffTopSideOnlyCollider);
                                            }
                                            else
                                            {
                                                positions.Add(position);
                                                tiles.Add(stairsTopWithRightCliffCollider);
                                            }
                                        }
                                        else if (leftHasTopStairs)
                                        {
                                            positions.Add(position);
                                            tiles.Add(cliffDoubleWithTopStairCutoutCollider);
                                        }
                                        else if (hasRightStairs)
                                        {
                                            if (bottomHasRightStairs)
                                            {
                                                positions.Add(position);
                                                tiles.Add(stairsRightWithCliffTopTopSideOnlyCollider);
                                            }
                                            else
                                            {
                                                positions.Add(position);
                                                tiles.Add(stairsRightWithCliffTopCollider);
                                            }

                                        }
                                        else if (bottomHasRightStairs)
                                        {
                                            positions.Add(position);
                                            tiles.Add(cliffDoubleWithRightStairCutoutCollider);
                                        }
                                        else
                                        {
                                            positions.Add(position);
                                            tiles.Add(cliffDoubleCollider);
                                        }
                                    }
                                }
                            }
                        }
                        else//top cliff
                        {
                            if (isTopCliffHalfVisible)//half top cliff
                            {
                                if (rightHasEdge)//half top cliff + right edge 
                                {
                                    if (topHasEdge)//half top cliff + double edge
                                    {
                                        positions.Add(position);
                                        tiles.Add(cliffHalfTopEdgeDoubleCollider);
                                    }
                                    else//half top cliff + right edge 
                                    {
                                        positions.Add(position);
                                        tiles.Add(cliffHalfTopEdgeRightCollider);
                                    }
                                }
                                else//half top cliff
                                {
                                    if (topHasEdge)//half top cliff + top edge
                                    {
                                        positions.Add(position);
                                        tiles.Add(cliffHalfTopEdgeTopCollider);
                                    }
                                    else//half top cliff
                                    {
                                        positions.Add(position);
                                        tiles.Add(cliffHalfTopCollider);
                                    }
                                }
                            }
                            else//top cliff
                            {
                                if (rightHasEdge)//top cliff + right edge
                                {
                                    if (leftHasTopStairs)
                                    {
                                        positions.Add(position);
                                        tiles.Add(cliffTopEdgeRightWithTopStairCutoutCollider);
                                    }
                                    else
                                    {
                                        positions.Add(position);
                                        tiles.Add(cliffTopEdgeRightCollider);
                                    }
                                }
                                else//top cliff
                                {
                                    if (UseCentralCollider(x, y))
                                    {
                                        positions.Add(position);
                                        tiles.Add(cliffTopWithCentralCollider);
                                    }
                                    else
                                    {
                                        if (hasTopStairs)
                                        {
                                            if (leftHasTopStairs)
                                            {
                                                if (!rightHasTopStairs)//if right also has top stairs, don't put any collider
                                                {
                                                    positions.Add(position);
                                                    tiles.Add(stairsTopTopSideOnlyCollider);
                                                }
                                            }
                                            else
                                            {
                                                if (rightHasTopStairs)
                                                {
                                                    positions.Add(position);
                                                    tiles.Add(stairsTopBottomSideOnlyCollider);
                                                }
                                                else
                                                {
                                                    positions.Add(position);
                                                    tiles.Add(stairsTopCollider);
                                                }
                                            }
                                        }
                                        else if (leftHasTopStairs)
                                        {
                                            positions.Add(position);
                                            tiles.Add(cliffTopWithTopStairCutoutCollider);
                                        }
                                        else
                                        {
                                            positions.Add(position);
                                            tiles.Add(cliffTopCollider);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (isThereVisibleCliffOnRight)//right cliff
                    {
                        if (isRightCliffHalfVisible)//half right cliff
                        {
                            if (rightHasEdge)//half right cliff + right edge
                            {
                                if (topHasEdge)//half right cliff + double edge
                                {
                                    positions.Add(position);
                                    tiles.Add(cliffHalfRightEdgeDoubleCollider);
                                }
                                else//half right cliff + right edge
                                {
                                    positions.Add(position);
                                    tiles.Add(cliffHalfRightEdgeRightCollider);
                                }
                            }
                            else//half right cliff
                            {
                                if (topHasEdge)//half right cliff + top edge
                                {
                                    positions.Add(position);
                                    tiles.Add(cliffHalfRightEdgeTopCollider);
                                }
                                else//half right cliff
                                {
                                    positions.Add(position);
                                    tiles.Add(cliffHalfRightCollider);
                                }
                            }
                        }
                        else // right cliff
                        {
                            if (topHasEdge)//right cliff + top edge
                            {
                                if (bottomHasRightStairs)
                                {
                                    positions.Add(position);
                                    tiles.Add(cliffRightEdgeTopWithStairRightCutoutCollider);
                                }
                                else
                                {
                                    positions.Add(position);
                                    tiles.Add(cliffRightEdgeTopCollider);
                                }
                            }
                            else//right cliff
                            {
                                if (UseCentralCollider(x, y))
                                {
                                    positions.Add(position);
                                    tiles.Add(cliffRightWithCentralCollider);
                                }
                                else
                                {
                                    if (hasRightStairs)
                                    {
                                        if (bottomHasRightStairs)
                                        {
                                            if (!topHasRightStairs)//if top also has right stairs don't put any colliders
                                            {
                                                positions.Add(position);
                                                tiles.Add(stairsRightTopSideOnlyCollider);
                                            }
                                        }
                                        else
                                        {
                                            if (topHasRightStairs)
                                            {
                                                positions.Add(position);
                                                tiles.Add(stairsRightBottomSideOnlyCollider);
                                            }
                                            else
                                            {
                                                positions.Add(position);
                                                tiles.Add(stairsRightCollider);
                                            }
                                        }
                                    }
                                    else if (bottomHasRightStairs)
                                    {
                                        positions.Add(position);
                                        tiles.Add(cliffRightWithRightStairCutoutCollider);
                                    }
                                    else
                                    {
                                        positions.Add(position);
                                        tiles.Add(cliffRightCollider);
                                    }
                                }
                            }
                        }
                    }
                    else if (topHasEdge)//top edge
                    {
                        if (rightHasEdge)//double edge
                        {
                            positions.Add(position);
                            tiles.Add(edgeDoubleCollider);
                        }
                        else//top edge
                        {
                            positions.Add(position);
                            tiles.Add(edgeTopCollider);
                        }
                    }
                    else if (rightHasEdge)//right edge
                    {
                        positions.Add(position);
                        tiles.Add(edgeRightCollider);
                    }
                    else if (GetLevelFromCoordinates(x, y) == -1 && !IsCentralFullyInvisible(x, y))//central collider
                    {
                        positions.Add(position);
                        tiles.Add(centralCollider);
                    }

                    //inverse stairs
                    if (hasBottomStairs)//stairs towards bottom
                    {
                        position += Vector3Int.left;//adjust position
                        if (leftHasBottomStairs)//don't put bottom part of stairs
                        {
                            if (!rightHasBottomStairs)//stairs towards bottom top side only
                            {
                                positions.Add(position);
                                tiles.Add(stairsBottomTopSideOnlyCollider);
                            }
                            //else put nothing
                        }
                        else//stairs towards bottom with bottom part
                        {
                            if (IsThereVisibleEdgeOnTop(x - 1, y))//don't put half of the bottom part
                            {
                                if (rightHasBottomStairs)//stairs towards bottom bottom side only with bottom cutout 
                                {
                                    positions.Add(position);
                                    tiles.Add(stairsBottomWithBottomCutoutBottomSideOnlyCollider);
                                }
                                else//stairs towards bottom with bottom cutout
                                {
                                    positions.Add(position);
                                    tiles.Add(stairsBottomWİthBottomCutoutCollider);
                                }
                            }
                            else//stairs towards bottom with bottom part
                            {
                                if (rightHasBottomStairs)//stairs towards bottom bottom side only
                                {
                                    positions.Add(position);
                                    tiles.Add(stairsBottomBottomSideOnlyCollider);
                                }
                                else//stairs towards bottom
                                {
                                    positions.Add(position);
                                    tiles.Add(stairsBottomCollider);
                                }
                            }
                        }
                        position += Vector3Int.right;//re-adjust position
                    }
                    if (hasLeftStairs)//stairs towards left
                    {
                        position += Vector3Int.down;//adjust position
                        if (bottomHasLeftStairs)//don't put bottom part of stairs
                        {
                            if (!topHasLeftStairs)//stairs towards left top side only
                            {
                                positions.Add(position);
                                tiles.Add(stairsLeftTopSideOnlyCollider);
                            }
                            //else put nothing
                        }
                        else//stairs towards left with bottom part
                        {
                            if (IsThereVisibleEdgeOnRight(x, y - 1))//don't put half of the bottom part
                            {
                                if (topHasLeftStairs)//stairs towards left bottom side only with bottom cutout 
                                {
                                    positions.Add(position);
                                    tiles.Add(stairsLeftWithBottomCutoutBottomSideOnlyCollider);
                                }
                                else//stairs towards left with bottom cutout
                                {
                                    positions.Add(position);
                                    tiles.Add(stairsLeftWithBottomCutoutCollider);
                                }
                            }
                            else//stairs towards left with bottom part
                            {
                                if (topHasLeftStairs)//stairs towards left bottom side only
                                {
                                    positions.Add(position);
                                    tiles.Add(stairsLeftBottomSideOnlyCollider);
                                }
                                else//stairs towards left
                                {
                                    positions.Add(position);
                                    tiles.Add(stairsLeftCollider);
                                }
                            }
                        }
                        //position += Vector3Int.up;//no need to re-adjust position because it gets re-written at the start of the loop
                    }
                }
                lock (listLock)
                {
                    allPositions.AddRange(positions);
                    allTiles.AddRange(tiles);
                }
            });
            _colliderTilemap.SetTiles(allPositions.ToArray(), allTiles.ToArray());
        }

        private void CreateColliderTilemap()
        {
            var tilemapObject = new GameObject("AutoCollider", typeof(Tilemap), typeof(TilemapCollider2D),
                typeof(Rigidbody2D), typeof(CompositeCollider2D), typeof(TilemapRenderer));
            tilemapObject.GetComponent<TilemapRenderer>().sortingOrder = 100;
            tilemapObject.transform.SetParent(_grid.transform, false);

            tilemapObject.layer = LayerMask.NameToLayer(_tilemapColliderLayerName);

            _colliderTilemap = tilemapObject.GetComponent<Tilemap>();
            _colliderTilemap.tileAnchor = _tilemapAnchor;

            tilemapObject.GetComponent<TilemapCollider2D>().usedByComposite = true;
            tilemapObject.GetComponent<TilemapCollider2D>().extrusionFactor = 0;
            tilemapObject.GetComponent<TilemapCollider2D>().maximumTileChangeCount = int.MaxValue;

            tilemapObject.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;

            tilemapObject.GetComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Polygons;//ensures players not getting stuck in collider
            tilemapObject.GetComponent<CompositeCollider2D>().offsetDistance = 0;
        }

        private bool UseCentralCollider(int x, int y)
        {
            return GetLevelFromCoordinates(x, y) < 0 && !IsCentralFullyInvisible(x, y);
        }
    }
}