using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TLab
{
    [System.Serializable]
    public class AnimationParameterAction
    {
        [SerializeField] public string action_name;

        [SerializeField] public string parameter_name;

        [SerializeField] public AnimatorControllerParameterType type;

        [SerializeField] public int int_value;

        [SerializeField] public float float_value;

        [SerializeField] public bool bool_value;
    }

    public class AnimationStateController : MonoBehaviour
    {
        [SerializeField] private Animator m_animator;

        [SerializeField] private AnimationParameterAction[] m_anim_param_actions;

        private Dictionary<string, UnityAction> m_action_dictionaly = new Dictionary<string, UnityAction>();

        private string THIS_NAME => "[ " + this.GetType() +"] ";

        public void SetBool(string name, bool value)
        {
            m_animator.SetBool(name, value);
        }

        public void SetFloat(string name, float value)
        {
            m_animator.SetFloat(name, value);
        }

        public void SetInteger(string name, int value)
        {
            m_animator.SetInteger(name, value);
        }

        public void SetTrigger(string name)
        {
            m_animator.SetTrigger(name);
        }

        public void CallRegistedAction(string action_name)
        {
            if (m_action_dictionaly.ContainsKey(action_name))
            {
                m_action_dictionaly[action_name]?.Invoke();
            }
            else
            {
                Debug.LogError(THIS_NAME + $"{action_name} no registed ...");
            }
        }

        private void Start()
        {
            foreach (var anim_param_action in m_anim_param_actions)
            {
                switch (anim_param_action.type)
                {
                    case AnimatorControllerParameterType.Bool:
                        m_action_dictionaly[anim_param_action.action_name] = () =>
                        {
                            SetBool(anim_param_action.parameter_name, anim_param_action.bool_value);
                        };
                        break;
                    case AnimatorControllerParameterType.Float:
                        m_action_dictionaly[anim_param_action.action_name] = () => {
                            SetFloat(anim_param_action.parameter_name, anim_param_action.float_value);
                        };
                        break;
                    case AnimatorControllerParameterType.Int:
                        m_action_dictionaly[anim_param_action.action_name] = () => {
                            SetInteger(anim_param_action.parameter_name, anim_param_action.int_value);
                        };
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        m_action_dictionaly[anim_param_action.action_name] = () => {
                            SetTrigger(anim_param_action.parameter_name);
                        };
                        break;
                }
            }
        }
    }
}
