using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForestGenerator : MonoBehaviour
{
    public bool auto_update;

    [Range(0,2)]
    public float global_scale_modifier;

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
    public float max_water_height;
    public float scale;

    public Renderer plane;
    public int seed;

    public int width;
    public int height;

    [Range (0,0.5f)]
    public float error_margin;

    void Start() {
        DrawMap();
    }
    
    public void DrawMap () {
        float[][,] noise_maps = new float[trees.Length][,];
        for (int i = 0; i < trees.Length; i++) {
            noise_maps[i] = GenerateNoiseMap(trees[i]);
        }
        float[,] cumulative_noise_map = GenerateCumulativeNoiseMap(noise_maps);
        Color[] ground = Ground(cumulative_noise_map);
        Texture2D texture = ConvertToTexture(ground);
        DrawGround(texture);
        Spawn(noise_maps);
    }

    float[,] GenerateNoiseMap (Tree tree_settings) {

        System.Random prng = new System.Random (seed + tree_settings.seed);
        Vector2[] octave_offsets = new Vector2[tree_settings.octaves];

        float[,] noise_map = new float [width, height];

        float half_width = width / 2;
        float half_height = height / 2;

        float max_possible_height = 0;
        float amplitude = 1;
        float frequency = 1;

        float max_noise = float.MinValue;
        float min_noise = float.MaxValue;

        for (int i = 0; i < tree_settings.octaves; i++)
        {
            float offsetx = prng.Next(-100000, 100000) + tree_settings.offset.x;
            float offsety = prng.Next(-100000, 100000) - tree_settings.offset.y;
            octave_offsets [i] = new Vector2(offsetx, offsety);

            max_possible_height += amplitude;
            amplitude *= tree_settings.persistance;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                amplitude = 1;
                frequency = 1;
                float noise_height = 0;

                for (int i = 0; i < tree_settings.octaves; i++)
                {
                    float X = (x - half_width + octave_offsets[i].x) / tree_settings.noise_scale * frequency;
                    float Y = (y - half_height + octave_offsets[i].y) / tree_settings.noise_scale * frequency;

                    float perlin_value = Mathf.PerlinNoise(X, Y) * 2 - 1;
                    noise_height += perlin_value * amplitude;

                    amplitude *= tree_settings.persistance;
                    frequency *= tree_settings.lacunarity;
                }

                if (noise_height > max_noise)
                {
                    max_noise = noise_height;
                }
                if (noise_height < min_noise)
                {
                    min_noise = noise_height;
                }
                noise_map[x, y] = noise_height;

                noise_map[x, y] = Mathf.InverseLerp(min_noise, max_noise, noise_map[x, y]);
            }
        }

        return noise_map;
    }

    float[,] GenerateCumulativeNoiseMap (float[][,] noise_maps) {
        float[,] cumulative_noise_map = new float [width, height];
        for (int y = 0; y < height;  y++) {
            for (int x = 0; x < width; x++) {
                float max_height = 0;
                for (int i = 0; i < trees.Length; i++) {
                    if (noise_maps[i][x, y] > max_height) {
                        max_height = noise_maps[i][x, y];
                    }
                }
                cumulative_noise_map[x, y] = max_height;
            }
        }
        return cumulative_noise_map;
    }

    Color[] Ground (float[,] cumulative_noise_map) {
        
        Color[] map = new Color [width * height];

        for (int y = 0; y < height;  y++) {
            for (int x = 0; x < width; x++) {
                if (display_mode == DisplayMode.noise) {
                    map[y * width + x] = Color.Lerp(Color.white, Color.black, cumulative_noise_map[x, y]);
                } else if (display_mode == DisplayMode.flat_color && draw_lakes) {
                    if (cumulative_noise_map[x, y] < max_water_height) {
                        map[y * width + x] = water_color;
                    } else {
                        map[y * width + x] = ground_color;
                    }
                } else {
                    map[y * width + x] = ground_color;
                }
            }
        }

        return map;
    }

    Texture2D ConvertToTexture (Color[] color_map) {
        Texture2D texture = new Texture2D (width, height);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels (color_map);
        texture.Apply();
        return texture;
    }

    void DrawGround (Texture2D texture) {
        plane.sharedMaterial.mainTexture = texture;
        plane.transform.localScale = new Vector3 (texture.width / 10 * scale, 1, texture.height / 10 * scale);
    }

    void Spawn(float[][,] noise_maps) {
        List<GameObject> children = new List<GameObject>();
        foreach (Transform tree in forest) children.Add(tree.gameObject);
        children.ForEach(child => DestroyImmediate(child));
        Random.InitState(seed);

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {

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
                    float spawnx = (-x + width/2 - 0.5f + errorx) * scale;
                    float spawny = (-y + height/2  - 0.5f + errory) * scale;
                    if (override_mode == TreeOverride.random) {
                        Tree tree_data = SelectRandomTree(trees_to_choose);
                        GameObject tree = Instantiate(tree_data.tree, new Vector3(spawnx, 0, spawny), Quaternion.identity, forest);
                        tree.transform.localScale = tree.transform.localScale * scale * tree_data.tree_scale;
                    } else if (override_mode == TreeOverride.max_probability){
                        Tree tree_data = SelectMaxProbabilityTree(trees_to_choose);
                        GameObject tree = Instantiate(tree_data.tree, new Vector3(spawnx, 0, spawny), Quaternion.identity, forest);
                        tree.transform.localScale = tree.transform.localScale * scale * tree_data.tree_scale;
                    } else {
                        foreach (Tree tree_data in trees_to_choose) {
                            errorx = Random.Range(-error_margin, error_margin);
                            errory = Random.Range(-error_margin, error_margin);
                            spawnx = (-x + width/2 - 0.5f + errorx) * scale;
                            spawny = (-y + height/2  - 0.5f + errory) * scale;
                            GameObject tree = Instantiate(tree_data.tree, new Vector3(spawnx, 0, spawny), Quaternion.identity, forest);
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