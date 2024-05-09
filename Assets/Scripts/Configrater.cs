using UnityEngine;
using TLab.MTPEG;
using TLab.InputField;

namespace TLab
{
    public class Configrater : MonoBehaviour
    {
        [SerializeField] private MTPEGClient m_client;

        [SerializeField] private SimpleInputField m_client_port_input;
        [SerializeField] private SimpleInputField m_server_address_input;

        public void Configuration()
        {
            m_client.server_addr = m_server_address_input.text;
            m_client.client_port = int.Parse(m_client_port_input.text);
        }
    }
}
