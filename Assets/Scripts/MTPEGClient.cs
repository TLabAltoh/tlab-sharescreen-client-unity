using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        [SerializeField] int m_screenWidth = 1920;
        [SerializeField] int m_screenHeight = 1080;
        [SerializeField] RawImage m_rawImage;

        [Header("End Point")]
        [SerializeField] private string m_serverAddr = "192.168.3.25";
        [SerializeField] private int m_serverPort = 55555;
        [SerializeField] private int m_clientPort = 50000;

        [Header("Compute Shader")]
        [SerializeField] private ComputeShader m_decoder;

        [Header("Runtype")]
        [SerializeField] private RunType m_runType;

        private int m_blockWidth;
        private int m_blockHeight;

        private bool m_keepAlive = false;

        private ClientState m_clientState;

        private struct LostPacketInfo
        {
            public ushort start;
            public ushort end;
            public byte index;
        }

        private Queue<LostPacketInfo> m_lostPacketInfoQueue = new Queue<LostPacketInfo>();
        private Mutex m_queueMutex = new Mutex();
        private Mutex m_frameUpdateMutex = new Mutex();

        private GraphicsBuffer m_encodedFrameComputeBuffer;
        private GraphicsBuffer m_dctBlockComputeBuffer;

        private bool m_textureUpdateFlag = false;
        private int m_targetFrameIndex;

        private int m_encodedFrameBufferSize;
        private byte[][] m_encodedFrameBuffer;

        private void CreateScreenTexture()
        {
            m_rawImage.texture = null;

            MTPEGUtil.PixelTweak(ref m_screenWidth, ref m_screenHeight);
            m_blockWidth = m_screenWidth / DCT.BLOCK_AXIS_SIZE;
            m_blockHeight = m_screenHeight / DCT.BLOCK_AXIS_SIZE;

            RenderTexture screenTexture = new RenderTexture(
                m_screenWidth,
                m_screenHeight,
                8,
                RenderTextureFormat.ARGB32
            );
            screenTexture.enableRandomWrite = true;
            screenTexture.Create();

            m_rawImage.texture = screenTexture;
        }

        private bool InitializeTPEGDecoder(int encoded_frame_size, int decoded_frame_size)
        {
            if (m_decoder == null)
            {
                Debug.LogError("m_decoder is null");
                return false;
            }

            var kernel_entropy_invert = m_decoder.FindKernel(Decoder.KERNEL_ENTROPY_INVERT);
            var kernel_dct_invert = m_decoder.FindKernel(Decoder.KERNEL_DCT_INVERT);

            var decoded_texture = m_rawImage.texture;

            m_decoder.SetTexture(kernel_dct_invert, Decoder.DECODED_TEXTURE, decoded_texture);

            m_decoder.SetInt(Decoder.SCREEN_WIDTH, m_screenWidth);
            m_decoder.SetInt(Decoder.SCREEN_HEIGHT, m_screenHeight);

            m_decoder.SetInt(Decoder.BLOCK_WIDTH, m_blockWidth);
            m_decoder.SetInt(Decoder.BLOCK_HEIGHT, m_blockHeight);

            if (m_encodedFrameComputeBuffer == null)
            {
                m_encodedFrameComputeBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    encoded_frame_size / sizeof(int),
                    sizeof(int)
                );
            }

            if (m_dctBlockComputeBuffer == null)
            {
                m_dctBlockComputeBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    decoded_frame_size,
                    sizeof(int)
                );
            }

            m_decoder.SetBuffer(kernel_entropy_invert, Decoder.ENCODED_BLOCK_BUFFER, m_encodedFrameComputeBuffer);
            m_decoder.SetBuffer(kernel_entropy_invert, Decoder.DCT_BLOCK_BUFFER, m_dctBlockComputeBuffer);
            m_decoder.SetBuffer(kernel_dct_invert, Decoder.DCT_BLOCK_BUFFER, m_dctBlockComputeBuffer);

            return true;
        }

        private void LongCopy(byte* src, byte* dst, int count)
        {
            // https://github.com/neuecc/MessagePack-CSharp/issues/117
            // Define it as an internal function in the thread to avoid method brute force.

            while (count >= 8)
            {
                *(ulong*)dst = *(ulong*)src;
                dst += 8;
                src += 8;
                count -= 8;
            }

            if (count >= 4)
            {
                *(uint*)dst = *(uint*)src;
                dst += 4;
                src += 4;
                count -= 4;
            }

            if (count >= 2)
            {
                *(ushort*)dst = *(ushort*)src;
                dst += 2;
                src += 2;
                count -= 2;
            }

            if (count >= 1)
            {
                *dst = *src;
            }
        }

        private void StartSharing()
        {
            m_lostPacketInfoQueue.Clear();

            CreateScreenTexture();

            int encoded_frame_size = MTPEGUtil.EncodedFrameSize(m_screenWidth, m_screenHeight);
            int decoded_frame_size = MTPEGUtil.DecodedFrameSize(m_screenWidth, m_screenHeight);

            m_encodedFrameBuffer = MTPEGUtil.CreateEncodedFrameBuffer(encoded_frame_size);

            InitializeTPEGDecoder(encoded_frame_size, decoded_frame_size);

            int packet_num_capacity = encoded_frame_size / DCT_BLOCK_SIZE + 1;

            Thread mainThread = new Thread(() =>
            {
                // Main receive thread

                UDPSocket udpSocket = null;
                if (!UDPSocketUtil.CreateSocket(ref udpSocket, m_clientPort, m_serverPort, m_serverAddr, 0))
                {
                    return;
                }

                var socket = udpSocket.socket;

                var buffer = new byte[MTU];

                byte last_frame_index = 0;

                ushort last_packet_index = 0;   // Last frame's final packet index

                while (m_keepAlive)
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
                            m_queueMutex.WaitOne();

                            if (frame_index != last_frame_index)
                            {
                                // The next frame arrives before all packets have been received.

                                ushort last_packet_final_index = (ushort)((ushort)(packet_hedder_ptr[PacketHedder.LAST_PACKET_INDEX_BE] << 8) | packet_hedder_ptr[PacketHedder.LAST_PACKET_INDEX_LE]);
                                if (last_packet_final_index > packet_num_capacity)
                                {
                                    m_queueMutex.ReleaseMutex();

                                    break;
                                }

                                m_lostPacketInfoQueue.Enqueue(new LostPacketInfo()
                                {
                                    start = (ushort)(last_packet_index + 1),
                                    end = last_packet_final_index,
                                    index = last_frame_index
                                });

                                m_frameUpdateMutex.WaitOne();   // Update Frame

                                m_targetFrameIndex = last_frame_index;
                                m_textureUpdateFlag = true;

                                m_frameUpdateMutex.ReleaseMutex();

                                m_lostPacketInfoQueue.Enqueue(new LostPacketInfo()
                                {
                                    start = 0,
                                    end = packet_index,
                                    index = frame_index
                                });
                            }
                            else
                            {
                                m_lostPacketInfoQueue.Enqueue(new LostPacketInfo()
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

                                m_frameUpdateMutex.WaitOne();   // Update Frame

                                m_textureUpdateFlag = true;
                                m_targetFrameIndex = frame_index;

                                m_frameUpdateMutex.ReleaseMutex();

                                m_queueMutex.ReleaseMutex();

                                // If packet's "is this packet end flag" is true.
                                // update last frame index and last pacekt index.

                                last_frame_index = (byte)((last_frame_index + 1) & FRAME_LOOP_NUM);

                                last_packet_index = 0;

                                continue;
                            }

                            // Release mutex so other process can access lost packet queue.
                            m_queueMutex.ReleaseMutex();
                        }

                        //
                        // copy received data
                        //

                        fixed (byte* encode_buffer_ptr = m_encodedFrameBuffer[frame_index])
                        {
                            byte* dct_block_hedder_ptr = packet_hedder_ptr + PacketHedder.HEDDER_SIZE;
                            byte* dct_block_buffer_ptr = dct_block_hedder_ptr + BlockHedder.HEDDER_SIZE;

                            ushort copy_length;
                            byte* encode_buffer_copy_ptr;

                            while (true)
                            {
                                ushort block_index = (ushort)((ushort)(dct_block_hedder_ptr[BlockHedder.BLOCK_INDEX_BE] << 8) | dct_block_hedder_ptr[BlockHedder.BLOCK_INDEX_LE]);
                                if (block_index > m_blockWidth * m_blockHeight)
                                {
                                    // ignore this packet to prevent out of range index
                                    break;
                                }

                                encode_buffer_copy_ptr = encode_buffer_ptr + block_index * BLOCK_OFFSET_SIZE;

                                foreach (var channel_size_index in new int[] { BlockHedder.Y_SIZE, BlockHedder.Cr_SIZE, BlockHedder.Cb_SIZE })
                                {
                                    copy_length = (ushort)(dct_block_hedder_ptr[channel_size_index] << ENDIAN_SIZE_LOG2);

                                    if (copy_length > DCT.BLOCK_SIZE * ENDIAN_SIZE)
                                    {
                                        break;
                                    }

                                    LongCopy(dct_block_buffer_ptr, encode_buffer_copy_ptr, copy_length);

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

            Thread fixRequestThread = new Thread(() =>
            {
                UDPSocket udpSocket = null;

                if (!UDPSocketUtil.CreateSocket(ref udpSocket, m_clientPort + 1, m_serverPort + 1, m_serverAddr, 1))
                {
                    return;
                }

                var socket = udpSocket.socket;
                var remote = udpSocket.remote;

                while (true)
                {
                    while (m_lostPacketInfoQueue.Count < 0) ;

                    m_queueMutex.WaitOne();

                    var lostPacketInfo = m_lostPacketInfoQueue.Dequeue();

                    var start = lostPacketInfo.start;
                    var end = lostPacketInfo.end;
                    var index = lostPacketInfo.index;

                    m_queueMutex.ReleaseMutex();

                    if (!m_keepAlive)
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

            m_clientState = ClientState.OPENED;

            m_keepAlive = true;

            mainThread.Start();
            fixRequestThread.Start();
        }

        private IEnumerator CloseSocketAsync()
        {
            m_clientState = ClientState.CLOSING;

            m_keepAlive = false;

            UDPSocketUtil.CloseAllSocket();

            yield return new WaitForSeconds(2f);    // wait for all socket closed

            m_queueMutex.WaitOne();

            m_lostPacketInfoQueue.Enqueue(new LostPacketInfo());    // add an element to the queue and break out of the while() in the block.

            m_queueMutex.ReleaseMutex();

            yield return new WaitForSeconds(2f);    // wait for udp fix req thread closed

            m_clientState = ClientState.CLOSED;

            yield break;
        }

        private void TPEGDecoderTest()
        {
            int encoded_frame_size = MTPEGUtil.EncodedFrameSize(m_screenWidth, m_screenHeight);
            int decoded_frame_size = MTPEGUtil.DecodedFrameSize(m_screenWidth, m_screenHeight);

            InitializeTPEGDecoder(encoded_frame_size, decoded_frame_size);

            // Create encodedBlock buffer copy to destination.
            short[] test_buffer = new short[encoded_frame_size / sizeof(short)];
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

            var kernel_entropy_invert = m_decoder.FindKernel(Decoder.KERNEL_ENTROPY_INVERT);
            var kernel_dct_invert = m_decoder.FindKernel(Decoder.KERNEL_DCT_INVERT);

            m_encodedFrameComputeBuffer.SetData(test_buffer, 0, 0, test_buffer.Length);

            m_decoder.Dispatch(kernel_entropy_invert, m_blockWidth, m_blockHeight, YCrCb_SIZE);
            m_decoder.Dispatch(kernel_dct_invert, m_blockWidth, m_blockHeight, YCrCb_SIZE);

            int[] result = new int[decoded_frame_size];

            m_dctBlockComputeBuffer.GetData(result);

            m_encodedFrameComputeBuffer.Release();
            m_dctBlockComputeBuffer.Release();

            for (int i = 0; i < 180; i++)
            {
                Debug.Log($"result [{i}] : {result[i]}");
            }
        }

        private void GraphicsBufferTest()
        {
            int encoded_frame_size = MTPEGUtil.EncodedFrameSize(m_screenWidth, m_screenHeight);

            byte[] test_buffer0 = new byte[encoded_frame_size];
            byte[] test_buffer1 = new byte[encoded_frame_size];

            for (int i = 0; i < test_buffer0.Length; i++)
            {
                test_buffer0[i] = 128;
            }

            GraphicsBuffer testGraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                test_buffer0.Length * sizeof(byte) / sizeof(int), sizeof(int)
            );

            testGraphicsBuffer.SetData(test_buffer0, 0, 0, test_buffer0.Length);
            testGraphicsBuffer.GetData(test_buffer1, 0, 0, test_buffer1.Length);

            for (int i = 0; i < 64; i++)
            {
                Debug.Log($"test_buffer1[{i}]: {test_buffer1[i]}");
            }

            for (int i = 0; i < test_buffer0.Length; i++)
            {
                test_buffer0[i] = 64;
            }

            testGraphicsBuffer.SetData(test_buffer0, 0, 0, test_buffer0.Length);
            testGraphicsBuffer.GetData(test_buffer1, 0, 0, test_buffer1.Length);

            for (int i = 0; i < 64; i++)
            {
                Debug.Log($"test_buffer1[{i}]: {test_buffer1[i]}");
            }

            testGraphicsBuffer.Release();
        }

        public void ComputeShaderDispacheTest()
        {
            // TPEGDecoderTest();

            GraphicsBufferTest();
        }

        public void OnButtonPress()
        {
            if (m_runType == RunType.CS_TEST)
            {
                Debug.LogError($"currently operating in {m_runType}");

                return;
            }

            switch (m_clientState)
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
            CreateScreenTexture();
        }

        private void Update()
        {
            m_frameUpdateMutex.WaitOne();

            if (m_textureUpdateFlag)
            {
                var encoded_frame_buffer = m_encodedFrameBuffer[m_targetFrameIndex];
                m_encodedFrameComputeBuffer.SetData(encoded_frame_buffer, 0, 0, encoded_frame_buffer.Length);

                m_frameUpdateMutex.ReleaseMutex();

                m_decoder.Dispatch(0, m_blockWidth, m_blockHeight, YCrCb_SIZE); // Entropy invert.
                m_decoder.Dispatch(1, m_blockWidth, m_blockHeight, 1);  // DCT invert.

                return;
            }

            m_frameUpdateMutex.ReleaseMutex();
        }

        void OnApplicationQuit()
        {
            m_queueMutex.Dispose();
            m_frameUpdateMutex.Dispose();
            m_encodedFrameComputeBuffer.Release();
            m_dctBlockComputeBuffer.Release();
        }
    }
}
