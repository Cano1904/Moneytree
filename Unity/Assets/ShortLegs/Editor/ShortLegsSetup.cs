using System.IO;
using ShortLegs.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShortLegs.EditorTools
{
    /// <summary>
    /// Tools ▸ Short Legs ▸ Create Test Scene
    /// Baut eine Testszene für <see cref="ShortLegsEngine"/>: Boden, Treppe (0,3 m Stufen – ab 2 Lügen blockiert),
    /// Kante (0,6 m – nur mit Sprung, ab 3 Lügen blockiert) und einen Platzhalter-Spieler mit Beinknochen.
    /// Im Play Mode: WASD / Stick, Shift sprinten, Leertaste springen, L = Lüge, R = Reset.
    /// </summary>
    public static class ShortLegsSetup
    {
        private const string SceneFolder = "Assets/ShortLegs/Scenes";
        private const string ScenePath = SceneFolder + "/ShortLegs_TestScene.unity";

        [MenuItem("Tools/Short Legs/Create Test Scene")]
        public static void CreateTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var defaultCamera = Camera.main;
            if (defaultCamera != null) Object.DestroyImmediate(defaultCamera.gameObject);

            // Kalte Umgebung mit warmem Akzent (Art Direction, Kapitel 0)
            var ground = Mat("Ground", new Color(0.79f, 0.86f, 0.89f));
            var wood = Mat("Wood", new Color(0.35f, 0.23f, 0.14f));
            var stone = Mat("Stone", new Color(0.29f, 0.36f, 0.42f));

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Ground";
            plane.transform.localScale = new Vector3(4f, 1f, 4f);
            plane.GetComponent<Renderer>().sharedMaterial = ground;

            // Treppe: Stufenhöhe 0,3 m < stepOffset 0,35 m
            var stairs = new GameObject("Stairs (blocked at 2 lies)");
            for (int i = 0; i < 5; i++)
            {
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = $"Step {i + 1}";
                step.transform.SetParent(stairs.transform);
                float h = 0.3f * (i + 1);
                step.transform.position = new Vector3(0f, h * 0.5f, 6f + i * 0.6f);
                step.transform.localScale = new Vector3(3f, h, 0.6f);
                step.GetComponent<Renderer>().sharedMaterial = wood;
            }

            var ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ledge.name = "Ledge 0.6m (jump only, blocked at 3 lies)";
            ledge.transform.position = new Vector3(-6f, 0.3f, 3f);
            ledge.transform.localScale = new Vector3(3f, 0.6f, 3f);
            ledge.GetComponent<Renderer>().sharedMaterial = stone;

            var player = CreatePlayer();
            Selection.activeGameObject = player;

            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                Directory.CreateDirectory(SceneFolder);
                AssetDatabase.Refresh();
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Short Legs] Testszene erstellt: {ScenePath}. Play drücken – L = Lüge, R = Reset.");
        }

        [MenuItem("Tools/Short Legs/Add Test Player To Scene")]
        public static void AddPlayerToOpenScene()
        {
            Selection.activeGameObject = CreatePlayer();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static GameObject CreatePlayer()
        {
            var coat = Mat("Coat", new Color(0.35f, 0.29f, 0.23f));
            var trousers = Mat("Trousers", new Color(0.23f, 0.25f, 0.28f));
            var skin = Mat("Skin", new Color(0.95f, 0.70f, 0.56f));

            var player = new GameObject("ShortLegs Player");
            Undo.RegisterCreatedObjectUndo(player, "Create Short Legs Player");

            var cc = player.AddComponent<CharacterController>();
            cc.height = 2.7f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 1.35f, 0f);
            cc.stepOffset = 0.35f;

            var audio = player.AddComponent<AudioSource>();
            audio.playOnAwake = false;

            var modelRoot = new GameObject("Model").transform;
            modelRoot.SetParent(player.transform, false);

            // Übergroßer Kopf, bullige Jacke, lange Beine (1,35×) – damit jede Stufe gut lesbar ist
            Part(PrimitiveType.Capsule, "Body", modelRoot, new Vector3(0f, 1.55f, 0f), new Vector3(0.8f, 0.5f, 0.6f), coat);
            Part(PrimitiveType.Sphere, "Head", modelRoot, new Vector3(0f, 2.35f, 0f), Vector3.one * 0.75f, skin);

            const float legLength = 1.1f;
            var leftLeg = Leg("LeftLeg", modelRoot, -0.18f, legLength, trousers);
            var rightLeg = Leg("RightLeg", modelRoot, 0.18f, legLength, trousers);

            // Third-Person-Kamera hinter dem Spieler, damit die Beine sichtbar sind
            var pitchPivot = new GameObject("CameraPivot").transform;
            pitchPivot.SetParent(player.transform, false);
            pitchPivot.localPosition = new Vector3(0f, 1.8f, 0f);
            pitchPivot.localEulerAngles = new Vector3(15f, 0f, 0f);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pitchPivot, false);
            camGo.transform.localPosition = new Vector3(0f, 0.6f, -4.5f);
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();

            var engine = player.AddComponent<ShortLegsEngine>();
            var so = new SerializedObject(engine);
            so.FindProperty("leftLegBone").objectReferenceValue = leftLeg;
            so.FindProperty("rightLegBone").objectReferenceValue = rightLeg;
            so.FindProperty("modelRoot").objectReferenceValue = modelRoot;
            so.FindProperty("legLength").floatValue = legLength;
            so.ApplyModifiedPropertiesWithoutUndo();

            var look = player.AddComponent<PrototypeMouseLook>();
            var lso = new SerializedObject(look);
            lso.FindProperty("pitchPivot").objectReferenceValue = pitchPivot;
            lso.ApplyModifiedPropertiesWithoutUndo();

            return player;
        }

        /// <summary>Bein-"Knochen": Pivot an der Hüfte, Geometrie hängt nach unten – Y-Skalierung kürzt das Bein.</summary>
        private static Transform Leg(string name, Transform parent, float x, float length, Material mat)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(x, length, 0f);
            Part(PrimitiveType.Cube, name + "Mesh", pivot, new Vector3(0f, -length * 0.5f, 0f), new Vector3(0.24f, length, 0.26f), mat);
            return pivot;
        }

        private static void Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // nur Optik, der CharacterController kollidiert
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Material Mat(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            return new Material(shader) { name = name, color = color };
        }
    }
}
