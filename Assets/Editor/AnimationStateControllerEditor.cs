using UnityEditor;

namespace TLab.Editor
{
    [CustomEditor(typeof(AnimationStateController))]
    public class AnimationStateControllerEditor : UnityEditor.Editor
    {
        private AnimationStateController m_instance;

        private void OnEnable()
        {
            m_instance = target as AnimationStateController;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
        }
    }
}