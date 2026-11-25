using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Herramienta de editor: automatiza la preparacion de modelos y animaciones de Mixamo.
// Poner este archivo en Assets/Editor/. Aparece un menu "RPG" arriba.
public static class RPGModelSetup
{
    const string ModelsFolder = "Assets/Models";
    const string ControllerPath = "Assets/Models/RPGController.controller";
    const string PrefabsFolder = "Assets/Prefabs";

    // ---------- 1) Rig Humanoid + loop del Idle ----------

    [MenuItem("RPG/1 - Preparar FBX (Rig Humanoid)")]
    static void PrepareModels()
    {
        if (!AssetDatabase.IsValidFolder(ModelsFolder))
        {
            EditorUtility.DisplayDialog("RPG", "No existe la carpeta " + ModelsFolder + ".\nCreala y poné ahí los FBX de Mixamo.", "OK");
            return;
        }

        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.SaveAndReimport();

            // Solo el Idle se repite en loop
            bool isIdle = Path.GetFileNameWithoutExtension(path).ToLower().Contains("idle");
            var clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                foreach (var c in clips) c.loopTime = isIdle;
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
            count++;
        }
        EditorUtility.DisplayDialog("RPG", "Listo: " + count + " FBX configurados con Rig Humanoid.", "OK");
    }

    // ---------- 2) Animator Controller ----------

    [MenuItem("RPG/2 - Crear Animator Controller")]
    static void BuildController()
    {
        var idleClip = FindClip("Idle");
        var attackClip = FindClip("Attack", "Slash", "Melee");
        var skillClip = FindClip("Skill", "Magic", "Spell", "Cast");
        var hitClip = FindClip("Hit", "Reaction");
        var dieClip = FindClip("Die", "Dying", "Death");
        var itemClip = FindClip("Item", "Drinking", "Drink") ?? skillClip;   // si no hay, reutiliza la de habilidad

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach (string t in new[] { "Attack", "Skill", "Item", "Hit", "Die" })
            controller.AddParameter(t, AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;

        var idle = sm.AddState("Idle");
        idle.motion = idleClip;
        sm.defaultState = idle;

        AddActionState(sm, idle, "Attack", attackClip, true);
        AddActionState(sm, idle, "Skill", skillClip, true);
        AddActionState(sm, idle, "Item", itemClip, true);
        AddActionState(sm, idle, "Hit", hitClip, true);
        AddActionState(sm, idle, "Die", dieClip, false);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string report =
            "Idle   <- " + Name(idleClip) + "\n" +
            "Attack <- " + Name(attackClip) + "\n" +
            "Skill  <- " + Name(skillClip) + "\n" +
            "Item   <- " + Name(itemClip) + "\n" +
            "Hit    <- " + Name(hitClip) + "\n" +
            "Die    <- " + Name(dieClip) + "\n\n" +
            "Si alguna dice 'NO ENCONTRADA' o eligio un archivo equivocado, arrastra el clip a mano en la ventana Animator.";
        Debug.Log("RPGController creado:\n" + report);
        EditorUtility.DisplayDialog("RPG", "Animator Controller creado en\n" + ControllerPath + "\n\n" + report, "OK");
        Selection.activeObject = controller;
    }

    static void AddActionState(AnimatorStateMachine sm, AnimatorState idle, string name, AnimationClip clip, bool returnToIdle)
    {
        var state = sm.AddState(name);
        state.motion = clip;

        // Desde cualquier estado, se activa con el trigger
        var any = sm.AddAnyStateTransition(state);
        any.hasExitTime = false;
        any.duration = 0.1f;
        any.canTransitionToSelf = false;
        any.AddCondition(AnimatorConditionMode.If, 0, name);

        // Al terminar vuelve al Idle (menos la de morir)
        if (returnToIdle)
        {
            var back = state.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.1f;
        }
    }

    static AnimationClip FindClip(params string[] keywords)
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { AssetDatabase.IsValidFolder(ModelsFolder) ? ModelsFolder : "Assets" });

        // 1) nombre de archivo exacto, 2) nombre que contiene la palabra
        for (int pass = 0; pass < 2; pass++)
            foreach (string key in keywords)
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string file = Path.GetFileNameWithoutExtension(path).ToLower();
                    string k = key.ToLower();
                    bool match = pass == 0 ? file == k : file.Contains(k);
                    if (!match) continue;

                    foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        var clip = obj as AnimationClip;
                        if (clip != null && !clip.name.StartsWith("__preview__")) return clip;
                    }
                }
        return null;
    }

    static string Name(AnimationClip clip)
    {
        if (clip == null) return "NO ENCONTRADA";
        return Path.GetFileName(AssetDatabase.GetAssetPath(clip)) + " (" + clip.name + ")";
    }

    // ---------- 3) Prefabs de personajes ----------

    [MenuItem("RPG/3 - Crear prefabs de los FBX seleccionados")]
    static void BuildPrefabs()
    {
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller == null)
        {
            EditorUtility.DisplayDialog("RPG", "Primero ejecuta \"RPG/2 - Crear Animator Controller\".", "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder(PrefabsFolder)) AssetDatabase.CreateFolder("Assets", "Prefabs");

        int count = 0;
        string created = "";
        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (!(AssetImporter.GetAtPath(path) is ModelImporter)) continue;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(obj);
            var anim = instance.GetComponent<Animator>();
            if (anim == null) anim = instance.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;

            string prefabPath = PrefabsFolder + "/Modelo_" + obj.name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);

            created += "\n" + prefabPath;
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (count == 0)
            EditorUtility.DisplayDialog("RPG", "No hay FBX seleccionados.\nSeleccioná en Project los FBX de los personajes (no los de animaciones) y volvé a ejecutarlo.", "OK");
        else
            EditorUtility.DisplayDialog("RPG", "Prefabs creados (" + count + "):" + created + "\n\nAhora arrastralos a 'Model Prefab' de cada personaje.", "OK");
    }
}
