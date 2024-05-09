using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using static TLab.MTPEG.Constants;

namespace TLab.MTPEG
{
    public unsafe class MTPEGClient : MonoBehaviour
    {
        // 前提: 同一のルーターに属していれば，パケットはロスしない限り必ず順番に届く．

        private enum RunType
        {
            SHARE,
            CS_TEST
        }

        private enum ClientState
        {
            OPENED,
            CLOSING,
            CLOSED
        }

        [Header("Screen Setting")]
        [SerializeField] private int m_screen_width = 1920;
        [SerializeField] private int m_screen_height = 1080;
        [SerializeField] private RawImage m_raw_image;

        [Header("End Point")]
        [SerializeField] private string m_server_addr = "127.0.0.1";
        [SerializeField] private int m_server_port = 55555;
        [SerializeField] private int m_client_port = 50000;

        [Header("Compute Shader")]
        [SerializeField] private ComputeShader m_decoder;

        [Header("Runtype")]
        [SerializeField] private RunType m_run_type;
        [SerializeField] private TMPro.TextMeshProUGUI m_log_tmpro;

        private int m_block_width;
        private int m_block_height;

        private bool m_keep_alive = false;

        private ClientState m_client_state = ClientState.CLOSED;

        private SynchronizationContext m_context;

        public string server_addr { get => m_server_addr; set => m_server_addr = value; }

        public int server_port { get => m_server_port; set => m_server_port = value; }

        public int client_port { get => m_client_port; set => m_client_port = value; }


        private struct LostPacketInfo
        {
            public ushort start;
            public ushort end;
            public byte index;
        }

        private Queue<LostPacketInfo> m_lost_packet_info_queue = new Queue<LostPacketInfo>();
        private Mutex m_queue_mutex = new Mutex();
        private Mutex m_frame_update_mutex = new Mutex();

        private GraphicsBuffer m_encoded_frame_cs_buffer;
        private GraphicsBuffer m_dct_block_cs_buffer;

        private bool m_texture_update_flag = false;

        private int m_encoded_frame_buffer_size;
        private byte[] m_encoded_frame_buffer;

        private void CreateScreenTexture()
        {
            m_raw_image.texture = null;

            MTPEGUtil.PixelTweak(ref m_screen_width, ref m_screen_height);
            m_block_width = m_screen_width / DCT.BLOCK_AXIS_SIZE;
            m_block_height = m_screen_height / DCT.BLOCK_AXIS_SIZE;

            RenderTexture screen_texture = new RenderTexture(
                m_screen_width,
                m_screen_height,
                8,
                RenderTextureFormat.ARGB32
            );
            screen_texture.enableRandomWrite = true;
            screen_texture.Create();

            m_raw_image.texture = screen_texture;
        }

        private bool InitializeTPEGDecoder(int encoded_frame_buffer_size, int decoded_frame_buffer_size)
        {
            if (m_decoder == null)
            {
                Debug.LogError("m_decoder is null");
                return false;
            }

            m_encoded_frame_buffer = MTPEGUtil.CreateEncodedFrameBuffer(encoded_frame_buffer_size);

            var kernel_entropy_invert = m_decoder.FindKernel(Decoder.KERNEL_ENTROPY_INVERT);
            var kernel_dct_invert = m_decoder.FindKernel(Decoder.KERNEL_DCT_INVERT);

            var decoded_texture = m_raw_image.texture;

            m_decoder.SetTexture(kernel_dct_invert, Decoder.DECODED_TEXTURE, decoded_texture);

            m_decoder.SetInt(Decoder.SCREEN_WIDTH, m_screen_width);
            m_decoder.SetInt(Decoder.SCREEN_HEIGHT, m_screen_height);

            m_decoder.SetInt(Decoder.BLOCK_WIDTH, m_block_width);
            m_decoder.SetInt(Decoder.BLOCK_HEIGHT, m_block_height);

            CSUtil.GraphicsBuffer(
                ref m_encoded_frame_cs_buffer,
                GraphicsBuffer.Target.Structured,
                encoded_frame_buffer_size / sizeof(int), sizeof(int));

            CSUtil.GraphicsBuffer(
                ref m_dct_block_cs_buffer,
                GraphicsBuffer.Target.Structured,
                decoded_frame_buffer_size, sizeof(int));

            m_decoder.SetBuffer(kernel_entropy_invert, Decoder.ENCODED_FRAME_BUFFER, m_encoded_frame_cs_buffer);
            m_decoder.SetBuffer(kernel_entropy_invert, Decoder.DCT_BLOCK_BUFFER, m_dct_block_cs_buffer);
            m_decoder.SetBuffer(kernel_dct_invert, Decoder.DCT_BLOCK_BUFFER, m_dct_block_cs_buffer);

            return true;
        }

        private void StartSharing()
        {
            m_lost_packet_info_queue.Clear();

            CreateScreenTexture();

            int encoded_frame_buffer_size = MTPEGUtil.EncodedFrameSize(m_screen_width, m_screen_height);
            int decoded_frame_buffer_size = MTPEGUtil.DecodedFrameSize(m_screen_width, m_screen_height);

            InitializeTPEGDecoder(encoded_frame_buffer_size, decoded_frame_buffer_size);

            Log("tpeg initialized ...");

            int packet_num_capacity = encoded_frame_buffer_size / DGRAM_BUFFER_SIZE + 1;

            Thread main_thread = new Thread(() =>
            {
                // Main receive thread

                UDPSocket udp_socket = null;
                if (!UDPSocketUtil.CreateSocket(ref udp_socket, m_client_port, m_server_port, m_server_addr, 0))
                {
                    if (m_log_tmpro != null)
                    {
                        m_log_tmpro.text = "an error occurred when creating the socket ...";
                    }

                    return;
                }

                Log("socket created ...");

                var socket = udp_socket.socket;

                var buffer = new byte[MTU];

                byte last_frame_index = 0;

                ushort last_packet_index = 0;   // Last frame's final packet index

                while (m_keep_alive)
                {
                    int length = socket.Receive(buffer); // Exception handling is quite heavy, so I dare not implement it (WSACancelBlockingCall)

                    fixed (byte* packet_hedder_ptr = buffer)
                    {
                        //
                        // analysing packet headers
                        // If the packet is not a retransmitted packet, the packet lost from the current is requested for retransmission.
                        //

                        ushort packet_index = (ushort)((ushort)(packet_hedder_ptr[PacketHedder.PACKET_INDEX_BE] << 8) | packet_hedder_ptr[PacketHedder.PACKET_INDEX_LE]);
                        if (packet_index > packet_num_capacity)
                        {
                            // ignore this packet to prevent out of range index
                            break;
                        }

                        byte frame_index = packet_hedder_ptr[PacketHedder.FRAME_OFFSET];
                        if (frame_index > FRAME_NUM)
                        {
                            // ignore this packet to prevent out of range index
                            break;
                        }

                        if (packet_hedder_ptr[PacketHedder.IS_THIS_FIX_PACKET] == FALSE)
                        {
                            m_queue_mutex.WaitOne();

                            if (frame_index != last_frame_index)
                            {
                                // The next frame arrives before all packets have been received.

                                ushort last_packet_final_index = (ushort)((ushort)(packet_hedder_ptr[PacketHedder.LAST_PACKET_INDEX_BE] << 8) | packet_hedder_ptr[PacketHedder.LAST_PACKET_INDEX_LE]);
                                if (last_packet_final_index > packet_num_capacity)
                                {
                                    m_queue_mutex.ReleaseMutex();

                                    break;
                                }

                                m_lost_packet_info_queue.Enqueue(new LostPacketInfo()
                                {
                                    start = (ushort)(last_packet_index + 1),
                                    end = last_packet_final_index,
                                    index = last_frame_index
                                });

                                m_frame_update_mutex.WaitOne();   // Update Frame

                                m_texture_update_flag = true;

                                m_frame_update_mutex.ReleaseMutex();

                                m_lost_packet_info_queue.Enqueue(new LostPacketInfo()
                                {
                                    start = 0,
                                    end = packet_index,
                                    index = frame_index
                                });
                            }
                            else
                            {
                                m_lost_packet_info_queue.Enqueue(new LostPacketInfo()
                                {
                                    start = (ushort)(last_packet_index + 1),
                                    end = packet_index,
                                    index = frame_index
                                });
                            }

                            last_packet_index = packet_index;

                            last_frame_index = frame_index;

                            if (packet_hedder_ptr[PacketHedder.IS_THIS_PACKET_END] == TRUE)
                            {
                                // Enqueue current frame request's finish flag.

                                // If there is no packet to resend,
                                // the end flag will be detected immediately
                                // on the resending thread side,
                                // and the texture will be updated immediately.

                                m_frame_update_mutex.WaitOne();   // Update Frame

                                m_texture_update_flag = true;

                                m_frame_update_mutex.ReleaseMutex();

                                m_queue_mutex.ReleaseMutex();

                                // If packet's "is this packet end flag" is true.
                                // update last frame index and last pacekt index.

                                last_frame_index = (byte)((last_frame_index + 1) & FRAME_LOOP_NUM);

                                last_packet_index = 0;

                                continue;
                            }

                            // Release mutex so other process can access lost packet queue.
                            m_queue_mutex.ReleaseMutex();
                        }

                        //
                        // copy received data
                        //

                        fixed (byte* encode_buffer_ptr = m_encoded_frame_buffer)
                        {
                            byte* dct_block_hedder_ptr = packet_hedder_ptr + PacketHedder.HEDDER_SIZE;
                            byte* dct_block_buffer_ptr = dct_block_hedder_ptr + BlockHedder.HEDDER_SIZE;

                            ushort copy_length;
                            byte* encode_buffer_copy_ptr;

                            while (true)
                            {
                                ushort block_index = (ushort)((ushort)(dct_block_hedder_ptr[BlockHedder.BLOCK_INDEX_BE] << 8) | dct_block_hedder_ptr[BlockHedder.BLOCK_INDEX_LE]);
                                if (block_index > m_block_width * m_block_height)
                                {
                                    // ignore this packet to prevent out of range index
                                    break;
                                }

                                encode_buffer_copy_ptr = encode_buffer_ptr + block_index * BLOCK_OFFSET_SIZE;

                                for (int i = 0; i < BlockHedder.CHANNEL_SIZE_IDX.Length; i++)
                                {
                                    copy_length = (ushort)(dct_block_hedder_ptr[BlockHedder.CHANNEL_SIZE_IDX[i]] << ENDIAN_SIZE_LOG2);

                                    if (copy_length > DCT.BLOCK_SIZE * ENDIAN_SIZE)
                                    {
                                        break;
                                    }

                                    MTPEGUtil.LongCopy(dct_block_buffer_ptr, encode_buffer_copy_ptr, copy_length);

                                    encode_buffer_copy_ptr += DCT.BLOCK_SIZE * ENDIAN_SIZE;

                                    dct_block_buffer_ptr += copy_length;
                                }

                                dct_block_hedder_ptr = dct_block_buffer_ptr;
                                dct_block_buffer_ptr += BlockHedder.HEDDER_SIZE;
                            }
                        }
                    }
                }
            });

            Thread fix_request_thread = new Thread(() =>
            {
                UDPSocket udp_socket = null;

                if (!UDPSocketUtil.CreateSocket(ref udp_socket, m_client_port + 1, m_server_port + 1, m_server_addr, 1))
                {
                    return;
                }

                var socket = udp_socket.socket;
                var remote = udp_socket.remote;

                while (true)
                {
                    while (m_lost_packet_info_queue.Count < 1) ;

                    m_queue_mutex.WaitOne();

                    var lost_packet_info = m_lost_packet_info_queue.Dequeue();

                    var start = lost_packet_info.start;
                    var end = lost_packet_info.end;
                    var index = lost_packet_info.index;

                    m_queue_mutex.ReleaseMutex();

                    if (!m_keep_alive)
                    {
                        break;
                    }

                    for (ushort i = start; i < end; i++)
                    {
                        // ソケットが受信待ちのタイミングで送信処理を行おうとすると
                        // SocketException:
                        // "既存の接続がリモートホストによって強制的に閉じられました"が発生する．
                        // 例外はレシーブ処理で受信することになるが重いのでどう扱うか要検討．
                        socket.SendTo(new byte[] { (byte)(i >> 8), (byte)i, index }, remote);
                    }
                }
            });

            m_client_state = ClientState.OPENED;

            m_keep_alive = true;

            main_thread.Start();
            fix_request_thread.Start();
        }

        private IEnumerator CloseSocketAsync()
        {
            m_client_state = ClientState.CLOSING;

            Log("client closing ...");

            m_keep_alive = false;

            UDPSocketUtil.CloseAllSocket();

            yield return new WaitForSeconds(2f);    // wait for all socket closed

            m_queue_mutex.WaitOne();

            m_lost_packet_info_queue.Enqueue(new LostPacketInfo());    // add an element to the queue and break out of the while() in the block.

            m_queue_mutex.ReleaseMutex();

            yield return new WaitForSeconds(2f);    // wait for udp fix req thread closed

            m_client_state = ClientState.CLOSED;

            Log("client closed ...");

            yield break;
        }

        private void DecodeTest()
        {
            CreateScreenTexture();

            int encoded_frame_buffer_size = MTPEGUtil.EncodedFrameSize(m_screen_width, m_screen_height);
            int decoded_frame_buffer_size = MTPEGUtil.DecodedFrameSize(m_screen_width, m_screen_height);

            InitializeTPEGDecoder(encoded_frame_buffer_size, decoded_frame_buffer_size);

            var kernel_entropy_invert = m_decoder.FindKernel(Decoder.KERNEL_ENTROPY_INVERT);
            var kernel_dct_invert = m_decoder.FindKernel(Decoder.KERNEL_DCT_INVERT);

            var stop_watch = new System.Diagnostics.Stopwatch();

#if true
            var encoded_frame_buffer = m_encoded_frame_buffer;

            MTPEGUtil.File2EncodedFrameBuffer(  // create encoded_frame_buffer from .tpeg file
                ref encoded_frame_buffer,
                "Assets/Resources/encoded_buffer.tpeg",
                (uint)m_block_width, (uint)m_block_height);

            stop_watch.Start();

            m_encoded_frame_cs_buffer.SetData(encoded_frame_buffer, 0, 0, encoded_frame_buffer.Length);
#else
            short[] test_buffer = new short[encoded_frame_buffer_size / sizeof(short)];
            for (int i = 0; i < test_buffer.Length; i += 2)
            {
                //////////////////////////////////
                // Get run's value
                // 
                // 1 1 1 1 1 1 1 1
                //       &
                // 0 1 1 1 1 1 1 0 (= 126)
                //
                // 0 0 1 1 1 1 1 1 (=  63)
                //

                // -32257 * 2 = -64514
                // -32513 * 2 = -65026
                test_buffer[i + 0] = -32257;
                test_buffer[i + 1] = -32513;
            }

            stop_watch.Start();
            m_encoded_frame_cs_buffer.SetData(test_buffer, 0, 0, test_buffer.Length);
#endif

            m_decoder.Dispatch(kernel_entropy_invert, m_block_width, m_block_height, YCrCb_SIZE);
            m_decoder.Dispatch(kernel_dct_invert, m_block_width, m_block_height, 1);

            stop_watch.Stop();

            int[] result = new int[decoded_frame_buffer_size];

            m_dct_block_cs_buffer.GetData(result);

            CSUtil.DisposeBuffer(ref m_encoded_frame_cs_buffer);
            CSUtil.DisposeBuffer(ref m_dct_block_cs_buffer);

            System.GC.SuppressFinalize(m_encoded_frame_cs_buffer);
            System.GC.SuppressFinalize(m_dct_block_cs_buffer);

            for (int i = 0; i < 180; i++)
            {
                Debug.Log($"result [{i}] : {result[i]}");
            }

            Debug.Log($"decoded time {stop_watch.ElapsedMilliseconds} ms, fps {1f / stop_watch.ElapsedMilliseconds * 1000}");
        }

        private void GraphicsBufferTest()
        {
            int encoded_frame_buffer_size = MTPEGUtil.EncodedFrameSize(m_screen_width, m_screen_height);

            byte[] test_buffer0 = new byte[encoded_frame_buffer_size];
            byte[] test_buffer1 = new byte[encoded_frame_buffer_size];

            for (int i = 0; i < test_buffer0.Length; i++)
            {
                test_buffer0[i] = 128;
            }

            GraphicsBuffer test_cs_buffer = null;

            CSUtil.GraphicsBuffer(
                ref test_cs_buffer,
                GraphicsBuffer.Target.Structured,
                test_buffer0.Length * sizeof(byte) / sizeof(int), sizeof(int));

            test_cs_buffer.SetData(test_buffer0, 0, 0, test_buffer0.Length);
            test_cs_buffer.GetData(test_buffer1, 0, 0, test_buffer1.Length);

            for (int i = 0; i < 64; i++)
            {
                Debug.Log($"test_buffer1[{i}]: {test_buffer1[i]}");
            }

            for (int i = 0; i < test_buffer0.Length; i++)
            {
                test_buffer0[i] = 64;
            }

            test_cs_buffer.SetData(test_buffer0, 0, 0, test_buffer0.Length);
            test_cs_buffer.GetData(test_buffer1, 0, 0, test_buffer1.Length);

            for (int i = 0; i < 64; i++)
            {
                Debug.Log($"test_buffer1[{i}]: {test_buffer1[i]}");
            }

            test_cs_buffer.Release();
        }

        public void Log(string message)
        {
            m_context.Post(obj =>
            {
                if (m_log_tmpro != null)
                {
                    m_log_tmpro.text = message;
                }
            }, null);
        }

        public void ComputeShaderDispacheTest()
        {
            DecodeTest();

            //GraphicsBufferTest();
        }

        public void OnButtonPress()
        {
            if (m_run_type == RunType.CS_TEST)
            {
                Debug.LogError($"currently operating in {m_run_type}");

                Log($"currently operating in { m_run_type}");

                return;
            }

            switch (m_client_state)
            {
                case ClientState.CLOSING:
                    Debug.LogError("termination process in progress");
                    break;
                case ClientState.OPENED:
                    StartCoroutine(CloseSocketAsync());
                    break;
                case ClientState.CLOSED:
                    StartSharing();
                    break;
            }
        }

        void Start()
        {
            m_context = SynchronizationContext.Current;
        }

        private void Update()
        {
            m_frame_update_mutex.WaitOne();

            if (m_texture_update_flag)
            {
                var encoded_frame_buffer = m_encoded_frame_buffer;
                m_encoded_frame_cs_buffer.SetData(encoded_frame_buffer, 0, 0, encoded_frame_buffer.Length);

                m_texture_update_flag = false;

                m_frame_update_mutex.ReleaseMutex();

                m_decoder.Dispatch(0, m_block_width, m_block_height, YCrCb_SIZE); // Entropy invert.
                m_decoder.Dispatch(1, m_block_width, m_block_height, 1);  // DCT invert.

                return;
            }

            m_frame_update_mutex.ReleaseMutex();
        }

        void OnApplicationQuit()
        {
            m_queue_mutex.Dispose();
            m_frame_update_mutex.Dispose();
            CSUtil.DisposeBuffer(ref m_dct_block_cs_buffer);
            CSUtil.DisposeBuffer(ref m_encoded_frame_cs_buffer);
        }
    }
}
