using UnityEditor;
using TextEditor = UnityEditor.UI.TextEditor;
namespace LinkSpriteText.Scripts.Editor
{
    [CanEditMultipleObjects, CustomEditor(typeof(LinkSpriteText), false)]
    public class LinkSpriteTextEditor : TextEditor
    {
        private SerializedProperty _mOriginText;
        private SerializedProperty _mFontData;

        protected override void OnEnable()
        {
            base.OnEnable();

            _mOriginText = serializedObject.FindProperty("originText");
            _mFontData = serializedObject.FindProperty("m_FontData");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_mOriginText);
            EditorGUILayout.PropertyField(_mFontData);


            AppearanceControlsGUI();
            serializedObject.ApplyModifiedProperties();
        }
    }
}