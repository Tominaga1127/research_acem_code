using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DanceLearning.Editor
{
    /// <summary>
    /// Main.unityに、XR Originを囲む市松模様の床・四方の壁・天井を作成するエディター専用のファイル。
    /// 形状やマテリアルをアセットとして保存するため、HMD実行時には生成処理が走らない。
    /// </summary>
    public static class DanceStudioBuilder
    {
        private const string scenePath = "Assets/DanceLearning/Scenes/Main.unity";
        private const string materialPath = "Assets/DanceLearning/Materials/Studio";
        private const string prefabPath = "Assets/DanceLearning/Prefabs/Presentation/DanceStudio.prefab";
        private const float RoomHeight = 4f;

        /// <summary>
        /// Unityのメニューからスタジオを作成し、プレハブ・シーン・確認用画像を保存する。
        /// 再実行すると既存のDance Studioを置き換え、マテリアルも指定の設定で更新する。
        /// </summary>
        [MenuItem("Dance Learning/Create Studio In Main")]
        public static void Build()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            // Mainがアクティブならそのまま編集し、別のシーンの場合のみMainを開く。
            if (scene.path != scenePath) scene = EditorSceneManager.OpenScene(scenePath);
            var origin = GameObject.Find("XR Origin (VR)").transform;
            var previous = GameObject.Find("Dance Studio");
            // 前回作成したスタジオがある場合のみ削除し、重複した配置を防ぐ。
            if (previous != null) Object.DestroyImmediate(previous);
            Directory.CreateDirectory(materialPath);
            AssetDatabase.Refresh();

            var oakFloor = CreateMaterial("Oak Floor", new Color(0.82f, 0.64f, 0.40f), 0.25f);
            oakFloor.SetTexture("_BaseMap", CreateFloorTexture());
            // 木目画像を繰り返し、床全体に対する床板の大きさを調整する。
            oakFloor.SetTextureScale("_BaseMap", new Vector2(3, 4));
            var wall = CreateMaterial("Warm Plaster", new Color(0.88f, 0.85f, 0.77f), 0.05f);
            var panel = CreateMaterial("Oak Wall Panels", new Color(0.66f, 0.49f, 0.30f), 0.12f);
            var trim = CreateMaterial("Dark Oak Trim", new Color(0.30f, 0.23f, 0.16f), 0.1f);

            var root = new GameObject("Dance Studio");
            // XR OriginのYを床面の高さとする。部屋の中心はOriginより前方に2mずらす。
            // 床の厚さを下側に設けることで、床の上面をOriginと同じ高さにそろえる。
            root.transform.position = origin.position + new Vector3(0, 0, 2);
            var floor = Box(root.transform, "Floor", new Vector3(0, -0.1f, 0), new Vector3(12.4f, 0.2f, 16.4f), oakFloor);
            ConfigureCheckerFloor(floor);
            CreateCeiling(root.transform, wall);
            var walls = new GameObject("Walls").transform;
            walls.SetParent(root.transform, false);
            // 壁の下端を床面、上端を天井の下面にそろえる。
            var wallCenterY = RoomHeight * 0.5f;
            Box(walls, "Front Wall", new Vector3(0, wallCenterY, 8.1f), new Vector3(12.4f, RoomHeight, 0.2f), wall);
            Box(walls, "Back Wall", new Vector3(0, wallCenterY, -8.1f), new Vector3(12.4f, RoomHeight, 0.2f), wall);
            Box(walls, "Left Wall", new Vector3(-6.1f, wallCenterY, 0), new Vector3(0.2f, RoomHeight, 16), wall);
            Box(walls, "Right Wall", new Vector3(6.1f, wallCenterY, 0), new Vector3(0.2f, RoomHeight, 16), wall);

            // 壁の内側は幅12m・奥行16m。腰壁の継ぎ目や巾木を距離の手掛かりにする。
            // sideを-1、+1の順に処理し、左右の壁と前後の壁に同じ装飾を配置する。
            for (var side = -1; side <= 1; side += 2)
            {
                // 継ぎ目の奥に暗い下地を設け、明るい壁面が細い隙間から見えるのを抑える。
                Box(walls, "Side Panel Backing " + side, new Vector3(side * 6, 1.1f, 0), new Vector3(0.02f, 2, 16), trim, false);
                Box(walls, "End Panel Backing " + side, new Vector3(0, 1.1f, side * 8), new Vector3(12, 2, 0.02f), trim, false);
                // 左右の壁には、奥行方向に1m間隔で16枚ずつパネルを並べる。
                for (var i = 0; i < 16; i++)
                    Box(walls, "Side Panel " + side + " " + i,
                        new Vector3(side * 5.985f, 1.1f, -7.5f + i), new Vector3(0.03f, 2.0f, 0.985f), panel, false);
                // 前後の壁には、幅方向に1m間隔で12枚ずつパネルを並べる。
                for (var i = 0; i < 12; i++)
                    Box(walls, "End Panel " + side + " " + i,
                        new Vector3(-5.5f + i, 1.1f, side * 7.985f), new Vector3(0.985f, 2.0f, 0.03f), panel, false);
                Box(walls, "Side Skirting " + side, new Vector3(side * 5.96f, 0.07f, 0), new Vector3(0.08f, 0.14f, 16), trim, false);
                Box(walls, "End Skirting " + side, new Vector3(0, 0.07f, side * 7.96f), new Vector3(12, 0.14f, 0.08f), trim, false);
                Box(walls, "Side Panel Cap " + side, new Vector3(side * 5.96f, 2.12f, 0), new Vector3(0.06f, 0.04f, 16), trim, false);
                Box(walls, "End Panel Cap " + side, new Vector3(0, 2.12f, side * 7.96f), new Vector3(12, 0.04f, 0.06f), trim, false);
            }

            // リアルタイムライトを追加せず、一定の環境光で四方の壁を見やすくする。
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.72f, 0.70f, 0.65f);
            // 作成したオブジェクトをプレハブ化し、Mainにはそのインスタンスを保存する。
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
            // スタジオを作り直した場合も、再生側が新しい床と鏡面の基準を参照するようにする。
            var playback = Object.FindAnyObjectByType<DancePlayback>();
            if (playback != null)
            {
                var serialized = new SerializedObject(playback);
                serialized.FindProperty("studio").objectReferenceValue = root.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            CapturePreview(root.transform.position);
            Debug.Log("Dance studio created: floor, four walls and ceiling.");
        }

        /// <summary>
        /// 既存のスタジオプレハブに天井だけを追加し、床や壁の編集内容を保持する。
        /// Mainのプレハブインスタンスにも反映される。天井が既にある場合は追加しない。
        /// </summary>
        [MenuItem("Dance Learning/Add Ceiling To Main")]
        public static void AddCeilingToMain()
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                // 再実行しても天井が重複しないよう、未作成の場合だけ直方体を追加する。
                if (root.transform.Find("Ceiling") == null)
                {
                    var wall = AssetDatabase.LoadAssetAtPath<Material>(materialPath + "/Warm Plaster.mat");
                    CreateCeiling(root.transform, wall);
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                // 保存中に例外が起きても、プレハブ編集用の一時シーンは解放する。
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            // Main以外を編集中なら、保存・撮影は行わずプレハブの更新だけで終了する。
            if (scene.path == scenePath)
            {
                EditorSceneManager.SaveScene(scene);
                CapturePreview(GameObject.Find("Dance Studio").transform.position);
            }
        }

        /// <summary>DanceStudioプレハブの天井を作り、下面を壁の上端にそろえる。</summary>
        private static void CreateCeiling(Transform root, Material wall)
        {
            const float thickness = 0.2f;
            Box(root, "Ceiling", new Vector3(0, RoomHeight + thickness * 0.5f, 0),
                new Vector3(12.4f, thickness, 16.4f), wall);
        }

        /// <summary>50 cmのマスを2つ含む画像を床の各方向に1 m周期で並べ、中央をマスの境界にする。</summary>
        internal static void ConfigureCheckerFloor(GameObject floor)
        {
            var size = floor.transform.lossyScale;
            var repeats = new Vector2(size.x, size.z);
            var material = CreateMaterial("Checker Floor", Color.white, 0.1f);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(materialPath + "/FloorChecker.png"));
            material.SetTextureScale("_BaseMap", repeats);
            material.SetTextureOffset("_BaseMap", -0.5f * repeats);
            floor.GetComponent<MeshRenderer>().sharedMaterial = material;

            // 床だけを静的バッチから外し、プレイヤーでマテリアルを交換できるようにする。
            var flags = GameObjectUtility.GetStaticEditorFlags(floor);
            GameObjectUtility.SetStaticEditorFlags(floor, flags & ~StaticEditorFlags.BatchingStatic);
        }

        /// <summary>
        /// 指定名のURP/Litマテリアルを取得または作成し、色と滑らかさを設定する。
        /// </summary>
        private static Material CreateMaterial(string name, Color color, float smoothness)
        {
            var path = materialPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            // 同じパスのマテリアルがなければ新規作成し、あれば既存アセットを更新する。
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            // マテリアルの変更を保存対象としてUnityに通知する。
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// 床板の継ぎ目・色むら・木目を計算して1024四方のPNGに保存し、テクスチャとして読み込む。
        /// </summary>
        private static Texture2D CreateFloorTexture()
        {
            const int size = 1024;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, true);
            var pixels = new Color[size * size];
            // 外側で各行、内側で各列を走査し、画像の全ピクセルの色を計算する。
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var column = x / 32;
                // 板幅は32px、長さは256px。列ごとに継ぎ目をずらし、画像の端では折り返す。
                var shiftedY = (y + (column % 3) * 85) % size;
                var row = shiftedY / 256;
                // 板ごとの色むらと、長手方向に伸びる木目・細かな筋を重ねる。
                var variation = Mathf.PerlinNoise(column * 7.3f + 0.1f, row * 9.1f + 0.4f);
                var grain = Mathf.PerlinNoise(x * 0.27f, y * 0.014f);
                var fine = Mathf.Sin(x * 1.7f + Mathf.Sin(y * 0.025f) * 2) * 0.018f;
                var value = 0.72f + variation * 0.25f + grain * 0.14f + fine;
                // 板の幅または長さの境界に当たるピクセルだけ暗くし、継ぎ目を描く。
                if (x % 32 == 0 || shiftedY % 256 < 2) value *= 0.66f;
                // 2次元座標を1次元配列の添字に変換し、暖色寄りの色を格納する。
                pixels[y * size + x] = new Color(value, value * 0.89f, value * 0.72f);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            var path = materialPath + "/OakPlanks.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            // 繰り返し表示とミップマップ、異方性フィルタで遠方・斜め方向の床の表示を整える。
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.maxTextureSize = size;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// 親を基準とした位置と大きさで直方体を作成し、共通マテリアルを割り当てる。
        /// colliderがtrueなら床・壁・天井の当たり判定を残し、falseなら装飾用として取り除く。
        /// </summary>
        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider = true)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.isStatic = true;
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            // 囲いが既存のDirectional Lightを遮って室内を暗くしないよう、影の投影を無効にする。
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            // パネルや巾木などの装飾には当たり判定を持たせず、床・壁・天井で判定する。
            if (!collider) Object.DestroyImmediate(item.GetComponent<Collider>());
            return item;
        }

        /// <summary>
        /// 室内に一時的なカメラを置いて確認用PNGを保存し、撮影用オブジェクトを解放する。
        /// </summary>
        private static void CapturePreview(Vector3 center)
        {
            var item = new GameObject("Studio Preview");
            var camera = item.AddComponent<Camera>();
            camera.transform.position = center + new Vector3(-3.7f, 1.65f, -6.6f);
            camera.transform.LookAt(center + new Vector3(0.5f, 1.1f, 3));
            camera.fieldOfView = 75;
            // HMDの左右眼向けではなく、通常の1枚の画像として撮影する。
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            var target = new RenderTexture(1280, 800, 24);
            camera.targetTexture = target;
            camera.Render();
            // 読み取り元を撮影先に切り替える前に、元の描画先を退避する。
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
            texture.Apply();
            File.WriteAllBytes("Assets/DanceLearning/Tests/StudioPreview.png", texture.EncodeToPNG());
            // 描画先を元に戻し、一時的な画像・描画領域・カメラを破棄する。
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(item);
        }
    }
}
