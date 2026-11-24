using UnityEditor;
using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    [CustomPropertyDrawer(typeof(PuzzleAction))]
    internal sealed class PuzzleActionDrawer : PropertyDrawer
    {
        private const string Type = nameof(PuzzleAction.type);
        private const string Target = nameof(PuzzleAction.target);
        private const string Duration = nameof(PuzzleAction.duration);

        private static readonly string[] Wait = { Type, Duration };
        private static readonly string[] Motion = { Type, Target, nameof(PuzzleAction.amount), Duration };
        private static readonly string[] SetState = { Type, Target, nameof(PuzzleAction.state) };
        private static readonly string[] Pulse = { Type, Target, nameof(PuzzleAction.intensity) };
        private static readonly string[] Play = { Type, Target, nameof(PuzzleAction.cue), nameof(PuzzleAction.intensity) };
        private static readonly string[] Material = { Type, Target, nameof(PuzzleAction.material) };
        private static readonly string[] Color = { Type, Target, nameof(PuzzleAction.color) };
        private static readonly string[] Start = { Type, Target };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.isExpanded)
            {
                foreach (string field in Fields(property))
                    height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field)) + EditorGUIUtility.standardVerticalSpacing;
            }
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using var scope = new EditorGUI.PropertyScope(position, label, property);
            var kind = (PuzzleActionType)property.FindPropertyRelative(Type).enumValueIndex;
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, ObjectNames.NicifyVariableName(kind.ToString()), true);
            if (!property.isExpanded)
                return;
            line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
            using var nesting = new EditorGUI.IndentLevelScope();
            foreach (string field in Fields(property))
            {
                var item = property.FindPropertyRelative(field);
                line.height = EditorGUI.GetPropertyHeight(item);
                EditorGUI.PropertyField(line, item);
                line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        private static string[] Fields(SerializedProperty property) =>
            (PuzzleActionType)property.FindPropertyRelative(Type).enumValueIndex switch
            {
                PuzzleActionType.Wait => Wait,
                PuzzleActionType.Move or PuzzleActionType.Rotate => Motion,
                PuzzleActionType.SetActive or PuzzleActionType.SetConnection => SetState,
                PuzzleActionType.ElasticPulse => Pulse,
                PuzzleActionType.PlayAudio => Play,
                PuzzleActionType.SetMaterial => Material,
                PuzzleActionType.SetColor => Color,
                _ => Start
            };
    }
}
