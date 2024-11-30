using UnityEngine;
using TLab.MTPEG;
using TLab.VKeyborad;

namespace TLab
{
    public class Configurator : MonoBehaviour
    {
        [SerializeField] private MTPEGClient m_client;

        [SerializeField] private InputField m_port;
        [SerializeField] private InputField m_address;

        public void Configuration()
        {
            m_client.server_addr = m_address.text;
            m_client.client_port = int.Parse(m_port.text);
        }
    }
}
