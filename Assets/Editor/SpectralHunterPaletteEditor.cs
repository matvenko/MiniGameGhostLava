using UnityEditor;
using UnityEngine;

// The palette sits on the hunter's root, but the piece an artist reaches for
// first is the one under the mouse in the scene - a child renderer with its own
// material in the inspector. Editing that material does nothing while the
// palette is overriding it, and nothing on screen says why. This says it.
[CustomEditor(typeof(SpectralHunterPalette))]
[CanEditMultipleObjects]
public class SpectralHunterPaletteEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "These five colours paint every part of this hunter, in the scene view and in play mode.\n\n" +
            "They override what the part materials say, so editing \"Ember Tail - violet\" and the rest " +
            "has no effect while this component is here.",
            MessageType.Info);

        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Read the colours back off the materials"))
            foreach (var target in targets)
                ((SpectralHunterPalette)target).CaptureFromMaterials();
    }
}
