using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TileProceduralGenerator : MonoBehaviour
{

    public TileMap[] maps;
    public int mapIndex;

    public enum DrawMode { Classic, Smooth }
    public DrawMode drawMode;

    public Transform sprite;

    TileMap currentMap;

    public bool autoUpdate;
    public TerrainType[] regions;

    void Start()
    {
        GenerateMap();
    }

    public void GenerateMap()
    {
        //generates map based on map index, allows you to create multiple maps in the TileMap array and switch between them easily based on the index 
        currentMap = maps[mapIndex];

        //creation of noisempa based on the specific map at specified index 
        float[,] noiseMap = Noise.GenerateNoiseMap(currentMap.mapSize.x, currentMap.mapSize.y, currentMap.seed, currentMap.noiseScale, currentMap.octaves, currentMap.persistence, currentMap.lacunarity, currentMap.offset);
        System.Random prng = new System.Random(currentMap.seed);


        //create map holder object
        string holderName = "Generated Map";
        if (transform.Find(holderName))
        {
            DestroyImmediate(transform.Find(holderName).gameObject);
        }

        Transform mapHolder = new GameObject(holderName).transform;
        mapHolder.parent = transform;

        //spawning tiles
        for (int x = 0; x < currentMap.mapSize.x; x++)
        {
            for (int y = 0; y < currentMap.mapSize.y; y++)
            {
                float tileHeight = noiseMap[x, y]; //tile height based on the noisemap created through noise.cs
                TerrainType tileTerrain; 

                //going through each tile and matching it with a specific tile type based on it's tile height 
                for (int i = 0; i < regions.Length; i++)
                {
                    if (tileHeight <= regions[i].terrainHeight)
                    {
                        tileTerrain = regions[i];
                        Vector2 tilePosition = CoordToPosition(x, y);

                        /* tileHeight is translated to its relative time on the x axis of a curve, returns y value at that x value.
                         in this case, the curve is very gradual until a peak near the end, which leaves the majority of values being low to match natural topography*/
                        float evaluatedHeight = currentMap.tileHeightCurve.Evaluate(tileHeight) * 10;
                        
                        //repeat same block up to the smallest or equal integer to the evaluated height that could be a floating point value type
                        int z = 0;
                        for (z = 0; z < Mathf.FloorToInt(evaluatedHeight); z++) 
                        {
                            Transform newTile = Instantiate(sprite, tilePosition + (new Vector2(0f, currentMap.heightMultiplier) * z), Quaternion.identity) as Transform;

                            newTile.parent = mapHolder;

                            SpriteRenderer spriteRenderer = newTile.GetComponent<SpriteRenderer>();
                            spriteRenderer.sprite = tileTerrain.sprite;
                            spriteRenderer.sortingOrder = ((x + 1) * (y + 1)) + z;
                        }
                        if (drawMode == DrawMode.Smooth && evaluatedHeight % 1 != 0f)
                        {
                            Transform newTile = Instantiate(sprite, tilePosition + (new Vector2(0f, currentMap.heightMultiplier) * (z - 1)) + new Vector2(0f, (evaluatedHeight % 1) * currentMap.heightMultiplier), Quaternion.identity) as Transform;

                            newTile.parent = mapHolder;

                            SpriteRenderer spriteRenderer = newTile.GetComponent<SpriteRenderer>();
                            spriteRenderer.sprite = tileTerrain.sprite;
                            spriteRenderer.sortingOrder = ((x + 1) * (y + 1)) + z + 1;
                            z++;
                        }

                        if (prng.Next(0, 100) < currentMap.foliageRate * 100)
                        {
                            float foliageRandomNumber = prng.Next(100) * .01f;
                            for (int f = 0; f < regions[i].Foliage.Length; f++)
                            {
                                if (foliageRandomNumber <= regions[i].Foliage[f].cumulativeWeight)
                                {
                                    Vector2 topSmoothTileAddedHeight;
                                    if (drawMode == DrawMode.Smooth)
                                    {
                                        topSmoothTileAddedHeight = new Vector2(0f, (evaluatedHeight % 1) * currentMap.heightMultiplier - .5f);
                                    }
                                    else
                                    {
                                        topSmoothTileAddedHeight = new Vector2(0, .15f);
                                    }
                                    Transform newFoliage = Instantiate(sprite, tilePosition + (new Vector2(0f, currentMap.heightMultiplier) * (z - 1)) + topSmoothTileAddedHeight, Quaternion.identity) as Transform;

                                    newFoliage.parent = mapHolder;

                                    SpriteRenderer spriteRenderer = newFoliage.GetComponent<SpriteRenderer>();
                                    spriteRenderer.sprite = regions[i].Foliage[f].sprite;
                                    spriteRenderer.sortingOrder = ((x + 1) * (y + 1)) + z + 1;
                                    break;
                                }
                            }
                        }

                        break;
                    }
                }





            }
        }

    }

    //determines spacing between blocks through a co-ordinate system. Have to change based on the sprite pixel length and width of each block
    Vector2 CoordToPosition(int x, int y)
    {
        float zeroX = 0f + .20f * x - .20f * y;
        float zeroY = ( ( currentMap.mapSize.y + currentMap.mapSize.x ) ) / 8f - 0.125f * y - .125f * x;
        return new Vector2(zeroX, zeroY);
    }

    [System.Serializable]
    public struct Coord
    {
        public int x;
        public int y;

        public Coord(int _x, int _y)
        {
            x = _x;
            y = _y;
        }
    }

    [System.Serializable]
    public class TileMap
    {
        public Coord mapSize;
        public float noiseScale;
        public int octaves;
        [Range(0, 1)]
        public float persistence;
        public float lacunarity;
        public Vector2 offset;
        public AnimationCurve tileHeightCurve;
        public int seed;
        [Range(0, .65f)]
        public float heightMultiplier = .5f;
        [Range(0, 1)]
        public float foliageRate = .5f;

        public Coord mapCenter
        {
            get
            {
                return new Coord(mapSize.x / 2, mapSize.y / 2);
            }
        }
    }

    //void OnValidate()
    //{
    //    if (currentMap.mapSize.x < 1)
    //    {
    //        currentMap.mapSize.x = 1;
    //    }
    //    if (currentMap.mapSize.y < 1)
    //    {
    //        currentMap.mapSize.y = 1;
    //    }
    //    if (currentMap.lacunarity < 1)
    //    {
    //        currentMap.lacunarity = 1;
    //    }
    //    if (currentMap.octaves < 0)
    //    {
    //        currentMap.octaves = 0;
    //    }
    //
    //
    //}

    [System.Serializable]
    public struct TerrainType
    {
        public string name;
        public float terrainHeight;
        public Sprite sprite;
        public FoliageType[] Foliage;
    }

    [System.Serializable]
    public struct FoliageType
    {
        public string name;
        public float cumulativeWeight;
        public Sprite sprite;
    }


}