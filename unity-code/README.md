# Dance Learning

mocopi proで事前収録したダンスの動きをUnity上で再生し、HMDで確認するための研究開発プロジェクトです。移動経路や身体部位の強調など、動きの理解を助ける情報提示を検討します。ユーザーの身体情報を取得するリアルタイムフィードバックは対象外です。

開発はPC VRで行い、完成品はPICO本体で動作させます。

## 自作アセットのフォルダ構成

新しい実装は `Assets/DanceLearning` にまとめます。以下のフォルダを用意しています。シーン、スクリプト、Prefab、データなどの中身は、実装時に追加します。

```text
Assets/DanceLearning/
├─ Scenes/                          # 実際に使用するシーン（Main.unityなど）
│  └─ Sandbox/                      # 機能を個別に確認するシーン
├─ Scripts/
│  ├─ Runtime/
│  │  ├─ Playback/                  # 再生・停止・速度・共通の再生時刻
│  │  ├─ Avatars/                   # 人物識別・対象アバターの管理
│  │  ├─ Presentation/              # 理解を助ける情報提示
│  │  │  ├─ Formation/              # 移動経路・次の立ち位置・交差関係
│  │  │  ├─ Choreography/           # 部位強調・次の姿勢・動作順
│  │  │  └─ Shared/                 # ラベル・透明度などの共通表示
│  │  ├─ Viewpoints/                # 3種類の視点・前方ディスプレイ
│  │  ├─ Input/                     # コントローラー入力
│  │  └─ UI/                        # 再生操作・表示切り替え
│  └─ Editor/                       # データ取り込み・編集補助
├─ Prefabs/
│  ├─ Avatars/                      # アバター
│  ├─ Presentation/                 # 経路・ラベルなどの表示部品
│  ├─ XR/                           # XR Originなどの自作・派生Prefab
│  └─ UI/                           # 操作パネルなど
├─ Data/
│  ├─ Dances/                       # 曲・人物・動作の関連付け
│  ├─ Annotations/                  # カウント・接触箇所・移動区間
│  └─ PresentationPresets/          # 提示条件の設定
├─ Animations/                      # 再生用モーション・Animator関連
├─ Models/                          # アバター・ステージなどのモデル
├─ Materials/                       # マテリアル
├─ Shaders/                         # 自作シェーダー
├─ Audio/                           # 楽曲・効果音
└─ Tests/
   ├─ EditMode/                     # データ処理・設定などのテスト
   └─ PlayMode/                     # 再生・表示などの動作テスト
```

## 既存フォルダの役割

| 場所 | 用途 |
| --- | --- |
| `Assets/MocopiReceiver` | mocopi受信SDKとサンプル。収録済みファイルの再生方法は別途検討する |
| `Assets/Samples` | XR Interaction Toolkit・XR Handsの参考実装 |
| `Assets/Scenes` | 既存の環境確認用シーン。開発用に複製する場合は `DanceLearning/Scenes` に保存する |
| `Assets/Editor/PicoBuild` | 既存のPICOビルド前処理 |
| `Assets/Tests/Editor/PicoBuild` | 既存のPICOビルド前処理のテスト |
| `Assets/Settings` | PC・Mobile向けの描画設定など |
| `Assets/XR`・`Assets/XRI` | XR・XR Interaction Toolkitの設定 |
| `Assets/Resources` | 既存のPICO関連設定 |
| `Assets/TextMesh Pro` | 文字表示用のリソース |
| `Assets/TutorialInfo` | Unityテンプレートの案内 |
| `Packages` | 導入パッケージの管理 |
| `ProjectSettings` | Unityプロジェクト全体の設定 |

## 実装・運用の方針

- モーション再生と情報提示を分離する。各表示は `Playback` が管理する共通の再生時刻に合わせ、一時停止や巻き戻しに追従させる。
- 動作・注釈・提示条件は `Data` に保存し、表示ロジックから分離する。
- SDKやサンプルを変更して使う場合は、変更対象を `DanceLearning` に複製し、自作部分を区別する。
- mocopiの収録原本は、必要になった段階でプロジェクト直下の `SourceData/` などに保管する。Unityで使用する再生用モーションは `Animations` に置く。`SourceData/` は現時点では作成していない。
- Editor専用コードは `Scripts/Editor` に配置する。実行時コードから `UnityEditor` を参照しない。アセンブリ定義を追加する際もEditor専用として分離する。
- アセンブリ定義は実装に合わせてRuntime・Editor・EditModeテスト・PlayModeテストから追加する。現時点では新しいアセンブリ定義は作成していない。
- 変数名はcamelCase、その他の名前はPascalCaseに統一する。
- テストにはUnity Test Frameworkを使用する。テストを追加する際はテスト用アセンブリとスクリプトを作成し、実行結果を報告する。
- アセットの移動・削除時は対応する `.meta` も一緒に扱い、既存アセットのGUIDを維持する。空フォルダ用の `.meta` も管理対象とする。

実装は、アバター1体の再生、視点切り替え、移動経路表示の順に進める想定です。
