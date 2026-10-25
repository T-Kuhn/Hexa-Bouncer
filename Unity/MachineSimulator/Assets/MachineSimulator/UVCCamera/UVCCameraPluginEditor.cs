using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

namespace MachineSimulator.UVCCamera
{
    [CustomEditor(typeof(UVCCameraPlugin))]
    public class UVCCameraPluginEditor : Editor
    {
        // NOTE: Keeps the candidate readout below live while playing.
        public override bool RequiresConstantRepaint()
        {
            return UnityEngine.Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var script = (UVCCameraPlugin)target;
            if (GUILayout.Button("SetProperties", GUILayout.Width(200)))
            {
                script.SetCameraProperties();
            }

            if (GUILayout.Button("ResetCameraProperties", GUILayout.Width(200)))
            {
                script.ResetCameraProperties();
            }

            if (UnityEngine.Application.isPlaying)
            {
                DrawBallCandidates(script);
            }
        }

        // NOTE: Objects of the newest frame that passed the radius filter, biggest first, and which one was
        //       taken as the ball (the one with the smallest CircleDeviation). Lets you read off what the ball
        //       and a wrongly detected surface score before touching any threshold.
        private static void DrawBallCandidates(UVCCameraPlugin script)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ball candidates of the newest frame, biggest first", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("r = radius px, outline = outline px, len = outline-length roundness (1 = circle),", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("rad = relative radial deviation (0 = circle), score = rad + |len - 1|, smallest is the ball", EditorStyles.miniLabel);

            var candidates = script.NewestCandidates;
            if (candidates == null || candidates.Count == 0)
            {
                EditorGUILayout.LabelField("none");
                return;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var marker = i == script.NewestBallCandidateIndex ? "ball" : "    ";
                EditorGUILayout.LabelField(
                    $"{marker}  r {candidate.Radius:F1}   outline {candidate.OutlinePixelCount}   len {candidate.OutlineLengthRoundness:F2}   rad {candidate.RelativeRadialDeviation:F3}   score {candidate.CircleDeviation:F3}");
            }
        }
    }
}
#endif