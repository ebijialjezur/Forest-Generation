using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForestGenerator : MonoBehaviour
{
    public bool auto_update;

    public Transform forest;
    public Tree[] trees;
    public enum DisplayMode {noise, flat_color};
    public DisplayMode display_mode;
    public enum TreeOverride {random, max_probability, spawn_all};
    public TreeOverride override_mode;
    public bool draw_lakes;
    public Color ground_color;
    public Color water_color;
    [Range(0,1)]
    public float max_water_chunk_size;
    public float scale;

    public GameObject plane;
    public int seed;

    public int chunk_size;

    [Range (0,0.5f)]
    public float error_margin;

    public Transform player;
    public int max_view_dst;

    Vector2 player_position;
    ForestGenerator forest_generator;

    List<TerrainChunk> terrain_chunks = new List<TerrainChunk>();
    Dictionary<Vector2, TerrainChunk> terrain_chunk_dictionary = new Dictionary<Vector2, TerrainChunk>();

    void Start() {
        Update();
    }

    void Update () {
        for (int i = 0; i < terrain_chunks.Count; i++) {
            terrain_chunks[i].Update(false);
        }

        int current_chunk_coordX = Mathf.RoundToInt(player.position.x / chunk_size);
        int current_chunk_coordY = Mathf.RoundToInt(player.position.y / chunk_size);

        for (int x = -max_view_dst; x < max_view_dst + 1; x++) {
            for (int y = -max_view_dst; y < max_view_dst + 1; y++) {
                Vector2 chunk_coord = new Vector2(current_chunk_coordX + x, current_chunk_coordY + y);
                if (terrain_chunk_dictionary.ContainsKey(chunk_coord)) {
                    terrain_chunk_dictionary [chunk_coord].Update(true);
                } else {
                    TerrainChunk terrain_chunk = new TerrainChunk (chunk_coord);
                    terrain_chunks.Add(terrain_chunk);
                    terrain_chunk_dictionary.Add(chunk_coord, terrain_chunk);
                }
            }
        }
    }
    
    public void DrawMapInEditor () {
        float[][,] noise_maps = new float[trees.Length][,];
        for (int i = 0; i < trees.Length; i++) {
            noise_maps[i] = GenerateNoiseMap(trees[i], new Vector2 (0,0));
        }
        float[,] cumulative_noise_map = GenerateCumulativeNoiseMap(noise_maps);
        Color[] ground = Ground(cumulative_noise_map);
        Texture2D texture = ConvertToTexture(ground);
        DrawGround(texture, plane);
        Spawn(noise_maps, Vector2.zero, forest);
    }

    public float[,] GenerateNoiseMap (Tree tree_settings, Vector2 offset) {

        System.Random prng = new System.Random (seed + tree_settings.seed);
        Vector2[] octave_offsets = new Vector2[tree_settings.octaves];

        float[,] noise_map = new float [chunk_size, chunk_size];

        float half_chunk_size = chunk_size / 2;

        float max_possible_height = 0;
        float min_possible_height = 0;
        float amplitude = 1;
        float frequency = 1;

        float max_noise = float.MinValue;
        float min_noise = float.MaxValue;

        for (int i = 0; i < tree_settings.octaves; i++)
        {
            float offsetx = prng.Next(-100000, 100000) + tree_settings.offset.x + offset.x;
            float offsety = prng.Next(-100000, 100000) - tree_settings.offset.y - offset.y;
            octave_offsets [i] = new Vector2(offsetx, offsety);

            max_possible_height += amplitude;
            min_possible_height -= amplitude;
            amplitude *= tree_settings.persistance;
        }

        for (int y = 0; y < chunk_size; y++)
        {
            for (int x = 0; x < chunk_size; x++)
            {
                amplitude = 1;
                frequency = 1;
                float noise_chunk_size = 0;

                for (int i = 0; i < tree_settings.octaves; i++)
                {
                    float X = (-x - half_chunk_size + octave_offsets[i].x) / tree_settings.noise_scale * frequency;
                    float Y = (y - half_chunk_size + octave_offsets[i].y) / tree_settings.noise_scale * frequency;

                    float perlin_value = Mathf.PerlinNoise(X, Y) * 2 - 1;
                    noise_chunk_size += perlin_value * amplitude;

                    amplitude *= tree_settings.persistance;
                    frequency *= tree_settings.lacunarity;
                }

                if (noise_chunk_size > max_noise)
                {
                    max_noise = noise_chunk_size;
                }
                if (noise_chunk_size < min_noise)
                {
                    min_noise = noise_chunk_size;
                }
                noise_map[x, y] = noise_chunk_size;

                noise_map[x, y] = Mathf.InverseLerp(max_possible_height, min_possible_height, noise_map[x, y]);

                if (noise_map[x, y] > 1 || noise_map[x, y] < 0) {
                    Debug.Log("Error: " + noise_map[x, y]);
                }
            }
        }

        return noise_map;
    }

    public float[,] GenerateCumulativeNoiseMap (float[][,] noise_maps) {
        float[,] cumulative_noise_map = new float [chunk_size, chunk_size];
        for (int y = 0; y < chunk_size;  y++) {
            for (int x = 0; x < chunk_size; x++) {
                float max_chunk_size = 0;
                for (int i = 0; i < trees.Length; i++) {
                    if (noise_maps[i][x, y] > max_chunk_size) {
                        max_chunk_size = noise_maps[i][x, y];
                    }
                }
                cumulative_noise_map[x, y] = max_chunk_size;
            }
        }
        return cumulative_noise_map;
    }

    public Color[] Ground (float[,] cumulative_noise_map) {
        
        Color[] map = new Color [chunk_size * chunk_size];

        for (int y = 0; y < chunk_size;  y++) {
            for (int x = 0; x < chunk_size; x++) {
                if (display_mode == DisplayMode.noise) {
                    map[y * chunk_size + x] = Color.Lerp(Color.white, Color.black, cumulative_noise_map[x, y]);
                } else if (display_mode == DisplayMode.flat_color && draw_lakes) {
                    if (cumulative_noise_map[x, y] < max_water_chunk_size) {
                        map[y * chunk_size + x] = water_color;
                    } else {
                        map[y * chunk_size + x] = ground_color;
                    }
                } else {
                    map[y * chunk_size + x] = ground_color;
                }
            }
        }

        return map;
    }

    public Texture2D ConvertToTexture (Color[] color_map) {
        Texture2D texture = new Texture2D (chunk_size, chunk_size);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels (color_map);
        texture.Apply();
        return texture;
    }

    public void DrawGround (Texture2D texture, GameObject plane) {
        plane.GetComponent<Renderer>().sharedMaterial.mainTexture = texture;
        plane.GetComponent<Renderer>().transform.localScale = new Vector3 (texture.width / 10 * scale, 1, texture.height / 10 * scale);
    }

    public void Spawn(float[][,] noise_maps, Vector2 offset, Transform plane) {
        List<GameObject> children = new List<GameObject>();
        foreach (Transform tree in forest) children.Add(tree.gameObject);
        children.ForEach(child => DestroyImmediate(child));
        Random.InitState(seed);

        for (int y = 0; y < chunk_size; y++) {
            for (int x = 0; x < chunk_size; x++) {

                List<Tree> trees_to_choose = new List<Tree>();

                for (int i = 0; i < noise_maps.Length; i++) {
                    if (noise_maps[i][x, y] >= trees[i].min_height_to_tree && trees[i].display) {
                        Tree tree_data = trees[i];
                        trees_to_choose.Add(tree_data);
                    }
                }
                if(trees_to_choose.Count > 0) {
                    float errorx = Random.Range(-error_margin, error_margin);
                    float errory = Random.Range(-error_margin, error_margin);
                    float spawnx = (-x + chunk_size/2 - 0.5f + errorx + offset.x) * scale;
                    float spawny = (-y + chunk_size/2 - 0.5f + errory + offset.y) * scale;
                    if (override_mode == TreeOverride.random) {
                        Tree tree_data = SelectRandomTree(trees_to_choose);
                        GameObject tree = Instantiate(tree_data.tree, new Vector3(spawnx, 0, spawny), Quaternion.identity, plane);
                        tree.transform.localScale = tree.transform.localScale * scale * tree_data.tree_scale;
                    } else if (override_mode == TreeOverride.max_probability){
                        Tree tree_data = SelectMaxProbabilityTree(trees_to_choose);
                        GameObject tree = Instantiate(tree_data.tree, new Vector3(spawnx, 0, spawny), Quaternion.identity, plane);
                        tree.transform.localScale = tree.transform.localScale * scale * tree_data.tree_scale;
                    } else {
                        foreach (Tree tree_data in trees_to_choose) {
                            errorx = Random.Range(-error_margin, error_margin);
                            errory = Random.Range(-error_margin, error_margin);
                            spawnx = (x + chunk_size/2 - 0.5f + errorx) * scale;
                            spawny = (-y + chunk_size/2 + 0.5f + errory) * scale;
                            GameObject tree = Instantiate(tree_data.tree, new Vector3(spawnx, 0, spawny), Quaternion.identity, plane);
                            tree.transform.localScale = tree.transform.localScale * scale * tree_data.tree_scale;
                        }
                    }
                }
            }
        }
    }

    Tree SelectRandomTree (List<Tree> trees_to_choose) {
        float total = 0;

        foreach (Tree tree in trees_to_choose) {
            total += tree.probability;
        }

        float randomPoint = Random.value * total;

        for (int i= 0; i < trees_to_choose.Count; i++) {
            if (randomPoint < trees_to_choose[i].probability) {
                return trees_to_choose[i];
            }
            else {
                randomPoint -= trees_to_choose[i].probability;
            }
        }
        return trees_to_choose[trees_to_choose.Count - 1];
    }

    Tree SelectMaxProbabilityTree(List<Tree> trees_to_choose) {
        Tree tree = trees_to_choose[0];
        float max_probability = 0;
        for (int i= 0; i < trees_to_choose.Count; i++) {
            if (trees_to_choose[i].probability > max_probability) {
                tree = trees_to_choose[i];
                max_probability = trees_to_choose[i].probability;
            }
        }
        return tree;
    }
    
    void OnValidate () {
        for (int i = 0; i < trees.Length; i++) {
            if (trees[i].octaves < 1) {
                trees[i].octaves = 1;
            } else if (trees[i].octaves > 12) {
                trees[i].octaves = 12;
            }
        }
        BalanceProbabilities();
    }

    void BalanceProbabilities() {
        float total = 0;
        for (int i = 0; i < trees.Length; i++) {
            total += trees[i].probability;
        }

        float multiplier = 1/total;

        for (int i = 0; i < trees.Length; i++) {
            trees[i].probability *= multiplier;
        }
    }
}

[System.Serializable]
public class Tree {
    public int seed;
    public bool display;

    public GameObject tree;
    [Range(0,1)]
    public float probability;
    public float tree_scale;

    [Range(0,12)]
    public int octaves;
    public float noise_scale;

    [Range(0,1)]
    public float persistance;
    public float lacunarity;

    [Range(0,1)]
    public float min_height_to_tree;

    public Vector2Int offset;
}

public class TerrainChunk {
    Vector2 coord;
    Vector2 offset;
    GameObject plane;
    GameObject terrain_chunk;

    public TerrainChunk (Vector2 coord) {
        ForestGenerator forest_generator = GameObject.FindObjectOfType<ForestGenerator>();
        this.coord = coord;
        this.offset = new Vector2 (coord.x * forest_generator.chunk_size, coord.y * forest_generator.chunk_size);
        this.terrain_chunk = new GameObject();
        terrain_chunk.name = "Terrain Chunk";
        this.plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.transform.SetParent(terrain_chunk.transform);
        plane.transform.position = new Vector3(offset.x, 0, offset.y);

        float[][,] noise_maps = new float[forest_generator.trees.Length][,];
        for (int i = 0; i < forest_generator.trees.Length; i++) {
            noise_maps[i] = forest_generator.GenerateNoiseMap(forest_generator.trees[i], offset);
        }
        float[,] cumulative_noise_map = forest_generator.GenerateCumulativeNoiseMap(noise_maps);
        Color[] ground = forest_generator.Ground(cumulative_noise_map);
        Texture2D texture = forest_generator.ConvertToTexture(ground);
        forest_generator.DrawGround(texture, plane);
        forest_generator.Spawn(noise_maps, offset, terrain_chunk.transform);
        plane.GetComponent<Renderer>().material.shader = Shader.Find("Unlit/Texture");
    }

    public void Update(bool visibility) {
        terrain_chunk.SetActive(visibility);
    }
}