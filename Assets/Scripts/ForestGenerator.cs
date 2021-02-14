using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForestGenerator : MonoBehaviour
{
    public Transform forest;
    public Tree[] trees;

    public enum DisplayMode {noise_map, filtered, flat_color};
    public DisplayMode display_mode;
    public Color ground_color;
    public float scale;

    public enum NormalizeMode {local, global};
    public NormalizeMode noise_mode;

    public bool auto_update;

    public Renderer plane;
    public int seed;

    [Range(0,12)]
    public int octaves;
    public float noise_scale;
    public int width;
    public int height;
    public Vector2 offset;

    [Range (0,1)]
    public float error_margin;

    public float global_scale_modifier;

    [Range(0,1)]
    public float persistance;
    public float lacunarity;

    [Range(0,1)]
    public float min_height_to_tree;

    void Start() {
        DrawMap();
    }
    
    public void DrawMap () {
        float[,] noise_map = GenerateNoiseMap();
        Color[] map = ConvertToColor(noise_map);
        Texture2D texture = ConvertToTexture(map);
        DrawMap(texture);
        Spawn(noise_map);
    }

    public float[,] GenerateNoiseMap () {

        System.Random prng = new System.Random (seed);
        Vector2[] octave_offsets = new Vector2[octaves];

        float[,] noise_map = new float [width, height];

        float half_width = width / 2;
        float half_height = height / 2;

        float max_possible_height = 0;
        float amplitude = 1;
        float frequency = 1;

        float max_noise = float.MinValue;
        float min_noise = float.MaxValue;

        for (int i = 0; i < octaves; i++)
        {
            float offsetx = prng.Next(-100000, 100000) + offset.x;
            float offsety = prng.Next(-100000, 100000) - offset.y;
            octave_offsets [i] = new Vector2(offsetx, offsety);

            max_possible_height += amplitude;
            amplitude *= persistance;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                amplitude = 1;
                frequency = 1;
                float noise_height = 0;

                for (int i = 0; i < octaves; i++)
                {
                    float X = (x - half_width + octave_offsets[i].x) / noise_scale * frequency;
                    float Y = (y - half_height + octave_offsets[i].y) / noise_scale * frequency;

                    float perlin_value = Mathf.PerlinNoise(X, Y) * 2 - 1;
                    noise_height += perlin_value * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
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

                if (noise_mode == NormalizeMode.global){
                    noise_map[x, y] = Mathf.Clamp((noise_map [x, y] + 1) / (2f * max_possible_height / global_scale_modifier), 0, int.MaxValue);
                }
            }
        }

        if (noise_mode == NormalizeMode.local){
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    noise_map[x, y] = Mathf.InverseLerp(min_noise, max_noise, noise_map[x, y]);
                }
            }
        }

        return noise_map;
    }

    public Color[] ConvertToColor (float[,] noise_map) {
        // Generate test color
        Color[] map = new Color [width * height];

        for (int y = 0; y < height;  y++) {
            for (int x = 0; x < width; x++) {

                float value = noise_map[x, y];

                if (display_mode == DisplayMode.noise_map) {
                    map[y * width + x] = Color.white;
                    map[y * width + x] = Color.Lerp(Color.black, Color.white, value);
                } else if (display_mode == DisplayMode.filtered) {
                    if (value >= min_height_to_tree) {
                        map[y * width + x] = Color.black;
                    } else {
                        map[y * width + x] = Color.white;
                    }
                } else {
                    map[y * width + x] = ground_color;
                }
            }
        }

        return map;
    }

    public Texture2D ConvertToTexture (Color[] color_map) {
        Texture2D texture = new Texture2D (width, height);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels (color_map);
        texture.Apply();
        return texture;
    }

    public void DrawMap (Texture2D texture) {
        plane.sharedMaterial.mainTexture = texture;
        plane.transform.localScale = new Vector3 (texture.width / 10 * scale, 1, texture.height / 10 * scale);
    }

    public void Spawn(float[,] map) {
        List<GameObject> children = new List<GameObject>();
        foreach (Transform tree in forest) children.Add(tree.gameObject);
        children.ForEach(child => DestroyImmediate(child));
        Random.InitState(seed);
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                if (map[x, y] >= min_height_to_tree) {
                    float errorx = Random.Range(-error_margin, error_margin);
                    float errory = Random.Range(-error_margin, error_margin);
                    double spawnx = (-x + width/2 - 0.5 + errorx) * scale;
                    double spawny = (-y + height/2  - 0.5 + errory) * scale;
                    Tree tree_data = SelectRandomTree();
                    GameObject tree = Instantiate(tree_data.tree, new Vector3((float)spawnx, 0, (float)spawny), Quaternion.identity, forest);
                    tree.transform.localScale = tree.transform.localScale * scale * tree_data.tree_scale;
                }
            }
        }
    }

    public Tree SelectRandomTree () {
        float total = 0;

        foreach (Tree tree in trees) {
            total += tree.probability;
        }

        float randomPoint = Random.value * total;

        for (int i= 0; i < trees.Length; i++) {
            if (randomPoint < trees[i].probability) {
                return trees[i];
            }
            else {
                randomPoint -= trees[i].probability;
            }
        }
        return trees[trees.Length - 1];
    }
    
    void OnValidate () {
        if (octaves < 1) {
            octaves = 1;
        } else if (octaves > 12) {
            octaves = 12;
        }
        BalanceProbabilities();
    }

    public void BalanceProbabilities() {
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
    public GameObject tree;
    [Range(0,1)]
    public float probability;
    public float tree_scale;

    Tree(GameObject tree, float probability, float tree_scale) {
        this.tree = tree;
        this.probability = probability;
        this.tree_scale = tree_scale;
    }
}