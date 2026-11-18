using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DanceLearning.Editor
{
    /// <summary>Creates serialized studio geometry; no generation runs on the headset.</summary>
    public static class DanceStudioBuilder
    {
        private const string scenePath = "Assets/DanceLearning/Scenes/Main.unity";
        private const string materialPath = "Assets/DanceLearning/Materials/Studio";
        private const string prefabPath = "Assets/DanceLearning/Prefabs/Presentation/DanceStudio.prefab";

        [MenuItem("Dance Learning/Create Studio In Main")]
        public static void Build()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != scenePath) scene = EditorSceneManager.OpenScene(scenePath);
            var origin = GameObject.Find("XR Origin (VR)").transform;
            var previous = GameObject.Find("Dance Studio");
            if (previous != null) Object.DestroyImmediate(previous);
            Directory.CreateDirectory(materialPath);
            AssetDatabase.Refresh();

            var floor = CreateMaterial("Oak Floor", new Color(0.82f, 0.64f, 0.40f), 0.25f);
            floor.SetTexture("_BaseMap", CreateFloorTexture());
            floor.SetTextureScale("_BaseMap", new Vector2(3, 4));
            var wall = CreateMaterial("Warm Plaster", new Color(0.88f, 0.85f, 0.77f), 0.05f);
            var panel = CreateMaterial("Oak Wall Panels", new Color(0.66f, 0.49f, 0.30f), 0.12f);
            var trim = CreateMaterial("Dark Oak Trim", new Color(0.30f, 0.23f, 0.16f), 0.1f);

            var root = new GameObject("Dance Studio");
            // Floor tracking uses the XR Origin's Y as the physical floor reference.
            root.transform.position = origin.position + new Vector3(0, 0, 2);
            Box(root.transform, "Floor", new Vector3(0, -0.1f, 0), new Vector3(12.4f, 0.2f, 16.4f), floor);
            var walls = new GameObject("Walls").transform;
            walls.SetParent(root.transform, false);
            Box(walls, "Front Wall", new Vector3(0, 2, 8.1f), new Vector3(12.4f, 4, 0.2f), wall);
            Box(walls, "Back Wall", new Vector3(0, 2, -8.1f), new Vector3(12.4f, 4, 0.2f), wall);
            Box(walls, "Left Wall", new Vector3(-6.1f, 2, 0), new Vector3(0.2f, 4, 16), wall);
            Box(walls, "Right Wall", new Vector3(6.1f, 2, 0), new Vector3(0.2f, 4, 16), wall);

            // Regular panel joints and skirting give stable, human-scale depth cues.
            for (var side = -1; side <= 1; side += 2)
            {
                Box(walls, "Side Panel Backing " + side, new Vector3(side * 6, 1.1f, 0), new Vector3(0.02f, 2, 16), trim, false);
                Box(walls, "End Panel Backing " + side, new Vector3(0, 1.1f, side * 8), new Vector3(12, 2, 0.02f), trim, false);
                for (var i = 0; i < 16; i++)
                    Box(walls, "Side Panel " + side + " " + i,
                        new Vector3(side * 5.985f, 1.1f, -7.5f + i), new Vector3(0.03f, 2.0f, 0.985f), panel, false);
                for (var i = 0; i < 12; i++)
                    Box(walls, "End Panel " + side + " " + i,
                        new Vector3(-5.5f + i, 1.1f, side * 7.985f), new Vector3(0.985f, 2.0f, 0.03f), panel, false);
                Box(walls, "Side Skirting " + side, new Vector3(side * 5.96f, 0.07f, 0), new Vector3(0.08f, 0.14f, 16), trim, false);
                Box(walls, "End Skirting " + side, new Vector3(0, 0.07f, side * 7.96f), new Vector3(12, 0.14f, 0.08f), trim, false);
                Box(walls, "Side Panel Cap " + side, new Vector3(side * 5.96f, 2.12f, 0), new Vector3(0.06f, 0.04f, 16), trim, false);
                Box(walls, "End Panel Cap " + side, new Vector3(0, 2.12f, side * 7.96f), new Vector3(12, 0.04f, 0.06f), trim, false);
            }

            // Neutral fill keeps all four walls readable without extra realtime lights.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.72f, 0.70f, 0.65f);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            CapturePreview(root.transform.position);
            Debug.Log("Dance studio created: floor and four walls, no mirror.");
        }

        private static Material CreateMaterial(string name, Color color, float smoothness)
        {
            var path = materialPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D CreateFloorTexture()
        {
            const int size = 1024;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, true);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var column = x / 32;
                var shiftedY = (y + (column % 3) * 85) % size;
                var row = shiftedY / 256;
                var variation = Mathf.PerlinNoise(column * 7.3f + 0.1f, row * 9.1f + 0.4f);
                var grain = Mathf.PerlinNoise(x * 0.27f, y * 0.014f);
                var fine = Mathf.Sin(x * 1.7f + Mathf.Sin(y * 0.025f) * 2) * 0.018f;
                var value = 0.72f + variation * 0.25f + grain * 0.14f + fine;
                if (x % 32 == 0 || shiftedY % 256 < 2) value *= 0.66f;
                pixels[y * size + x] = new Color(value, value * 0.89f, value * 0.72f);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            var path = materialPath + "/OakPlanks.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.maxTextureSize = size;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider = true)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.isStatic = true;
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            // Enclosing walls must not block the existing directional light.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (!collider) Object.DestroyImmediate(item.GetComponent<Collider>());
        }

        private static void CapturePreview(Vector3 center)
        {
            var item = new GameObject("Studio Preview");
            var camera = item.AddComponent<Camera>();
            camera.transform.position = center + new Vector3(-3.7f, 1.65f, -6.6f);
            camera.transform.LookAt(center + new Vector3(0.5f, 1.1f, 3));
            camera.fieldOfView = 75;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            var target = new RenderTexture(1280, 800, 24);
            camera.targetTexture = target;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
            texture.Apply();
            File.WriteAllBytes("Assets/DanceLearning/Tests/StudioPreview.png", texture.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(item);
        }
    }
}
