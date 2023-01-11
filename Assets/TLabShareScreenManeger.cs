using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections.LowLevel.Unsafe;
using UniRx;

public class TLabShareScreenManeger : MonoBehaviour
{
    // 前提: 同一のルーターに属していれば，パケットはロスしない限り必ず順番に届く．

    [Header("Screen Setting")]
    [SerializeField] int screenWidth = 1920;
    [SerializeField] int screenHeight = 1080;
    [SerializeField] GameObject screenCanvas;
    [SerializeField] GameObject screen;
    [SerializeField] Text stateText;
    [SerializeField] Text connectButtonText;

    [Header("Server Info")]
    [SerializeField] string serverAddr;
    [SerializeField] int serverPort;

    [Header("Client Info")]
    [SerializeField] int clientPort;

    [Header("Decoding device")]
    [SerializeField] ComputeShader TPEGDecoder;

    private TLabUnitySocket tlabSocket;

    private int blockWidth;
    private int blockHeight;

    [Header("Test mode")]
    [SerializeField] bool testing = true;

    private bool keepAlive = false;
    private bool shareing = false;
    private bool nowClosing = false;
    private bool screenTextureCreated = false;

    // Queue to hold resend requests.
    private Queue<ushort> lostPacketQueue = null;
    private Mutex queueMutex = new Mutex();
    private Mutex updateMutex = new Mutex();

    // Create GraphicsBuffer to send data to compute shader.
    private GraphicsBuffer encodedBlockBuffer = null;
    private GraphicsBuffer dctBlockBuffer = null;

    private int textureUpdateFlag = TPEGConstant.FRAME_BUFFER_NUM;
    private int encBufferSize;
    private byte[][] encBuffer;

    private void PixelTweak(ref int width, ref int height)
    {
        // Adjust pixels to multiples of BLOCK_AXIS_SIZE.

        int tmp;
        float tmp1;
        float tmp2;

        tmp = width / TPEGConstant.BLOCK_AXIS_SIZE;
        tmp1 = (float)width / (float)TPEGConstant.BLOCK_AXIS_SIZE;
        tmp2 = tmp1 - tmp;

        if (tmp2 > 0)
        {
            width = width + (int)(tmp2 * TPEGConstant.BLOCK_AXIS_SIZE);
        }

        tmp = height / TPEGConstant.BLOCK_AXIS_SIZE;
        tmp1 = (float)height / (float)TPEGConstant.BLOCK_AXIS_SIZE;
        tmp2 = tmp1 - tmp;

        if (tmp2 > 0)
        {
            height = height + (int)(tmp2 * TPEGConstant.BLOCK_AXIS_SIZE);
        }

        return;
    }

    private void BufferSizeTweak(ref int bufferSize, int multipleOf)
    {
        float div = (float)bufferSize / (float)multipleOf;
        int divNum = (int)div;
        if (divNum % multipleOf != 0) divNum += (int)(div - divNum) * multipleOf;
    }

    private void SetTexture(RenderTexture screenTexture, bool isThisScreen, GameObject screenImage)
    {
        RectTransform temp = screen.GetComponent<RectTransform>();

        float scaleBasedOnX = temp.rect.width / screenTexture.width;
        float scaleBasedOnY = temp.rect.height / screenTexture.height;
        float heightBasedOnX = screenHeight * scaleBasedOnX;
        float scale = heightBasedOnX > temp.rect.height ? scaleBasedOnY : scaleBasedOnX;

        float srsX = scale;
        float srsY = isThisScreen ? -scale : scale;
        float srsZ = scale;

        RectTransform screenRect = screenImage.GetComponent<RectTransform>();

        screenRect.anchorMin = new Vector2(0.5f, 0.5f);
        screenRect.anchorMax = new Vector2(0.5f, 0.5f);
        screenRect.sizeDelta = new Vector2(screenTexture.width, screenTexture.height);
        screenRect.localScale = new Vector3(srsX, srsY, srsZ);
        screenRect.anchoredPosition = Vector3.zero;

        screenImage.GetComponent<RawImage>().texture = screenTexture;

        // Set Rread write texture to compute shader.
        if(testing == false)
        {
            TPEGDecoder.SetTexture(
                TPEGDecoder.FindKernel("DCTInvert"),
                "DecodedTexture",
                screenTexture
            );
        }
    }

    private void CreateScreenTexture()
    {
        GameObject screenImage = new GameObject("ScreenImage");
        screenImage.AddComponent<RectTransform>();
        screenImage.AddComponent<CanvasRenderer>();
        screenImage.AddComponent<RawImage>();

        screenImage.transform.SetParent(screenCanvas.transform);
        screenImage.transform.localPosition = Vector3.zero;

        PixelTweak(ref screenWidth, ref screenHeight);
        blockWidth = screenWidth / TPEGConstant.BLOCK_AXIS_SIZE;
        blockHeight = screenHeight / TPEGConstant.BLOCK_AXIS_SIZE;

        RenderTexture screenTexture = new RenderTexture(
            screenWidth,
            screenHeight,
            8,
            RenderTextureFormat.ARGB32
        );
        screenTexture.enableRandomWrite = true;
        screenTexture.Create();

        SetTexture(screenTexture, true, screenImage);

        screenTextureCreated = true;
    }

    private void InitializeTPEGDecoder(int encBufferSize, int dctBufferSize)
    {
        if(TPEGDecoder == null)
        {
            Debug.LogError("TPEGDecoder is NULL");
            return;
        }

        // Set parameter to compute shader.
        TPEGDecoder.SetInt("WIDTH", screenWidth);
        TPEGDecoder.SetInt("HEIGHT", screenHeight);

        TPEGDecoder.SetInt("BLOCK_WIDTH", blockWidth);
        TPEGDecoder.SetInt("BLOCK_HEIGHT", blockHeight);

        Debug.Log("BlockWidth: " + blockWidth.ToString());
        Debug.Log("BlockHeight: " + blockHeight.ToString());

        // Set Graphics buffer.

        if(encodedBlockBuffer == null)
        {
            encodedBlockBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                encBufferSize / sizeof(int),
                sizeof(int)
            );
        }

        if(dctBlockBuffer == null)
        {
            dctBlockBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                dctBufferSize,
                sizeof(int)
            );
        }

        TPEGDecoder.SetBuffer(0, "EncodedBlockBuffer", encodedBlockBuffer);
        TPEGDecoder.SetBuffer(0, "DCTBlockBuffer", dctBlockBuffer);
        TPEGDecoder.SetBuffer(1, "DCTBlockBuffer", dctBlockBuffer);
    }

    private byte[][] CreateEncBufferArray(int size)
    {
        byte[][] encBuffer = new byte[TPEGConstant.FRAME_BUFFER_NUM][];
        for (int i = 0; i < TPEGConstant.FRAME_BUFFER_NUM; i++)
            encBuffer[i] = new byte[size + TPEGConstant.BLOCK_OFFSET_SIZE];

        return encBuffer;
    } 

    private int EncBufferArraySize(int width, int height)
    {
        return width * height * TPEGConstant.YCrCb_SIZE * TPEGConstant.ENDIAN_SIZE;
    }

    private int DCTBufferArraySize(int width, int height)
    {
        return width * height * TPEGConstant.YCrCb_SIZE;
    }

    private unsafe void LongCopy(byte* src, byte* dst, int count)
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
        if (testing == true)
        {
            Debug.Log("Now is testing mode.");
            return;
        }

        if (screenTextureCreated == false) CreateScreenTexture();

        encBufferSize = EncBufferArraySize(screenWidth, screenHeight);

        // Create compute buffer.
        BufferSizeTweak(ref encBufferSize, sizeof(int));

        // Create encBuffer array.
        encBuffer = CreateEncBufferArray(encBufferSize);

        int dctBufferSize = DCTBufferArraySize(screenWidth, screenHeight);

        // Initialize tpeg decoder.
        InitializeTPEGDecoder(encBufferSize, dctBufferSize);

        int packetNum = encBufferSize / TPEGConstant.DCT_BLOCK_SIZE + 1;

        Subject<string> changeState = new Subject<string>();

        changeState
            .ObserveOnMainThread()
            .Subscribe(state =>
            {
                // update display's log message.

                Debug.Log(state);
                stateText.text = state;
            })
            .AddTo(this);

        Subject<string> tlabDebugLog = new Subject<string>();

        tlabDebugLog
            .ObserveOnMainThread()
            .Subscribe(message =>
            {
                // print log in main thread.

                Debug.Log(message);
            })
            .AddTo(this);

        Thread udpMainThread = new Thread(() =>
        {
            // Main receive thread.

            // Update log in display.
            changeState.OnNext("Shareing ...");

            // Get udpMainThread's socket.
            Socket socket = tlabSocket.GetSocketFromID(clientPort, serverPort, serverAddr, 0);

            // Create socket's receive buffer.
            byte[] buffer = new byte[TPEGConstant.MTU];

            // Frame's last index.
            byte lastFrameIdx = 0;

            // Last frame's end packet idnex.
            ushort lastPacketIdx = 0;

            while (keepAlive == true)
            {
                // Exception handling is quite heavy, so I dare not implement it (WSACancelBlockingCall).
                int receiveLength = socket.Receive(buffer);

                unsafe
                {
                    //////////////////////////////////////////////////////////////////////////
                    // Parse packet headers.
                    // 

                    fixed (byte* packetBufferHedderPt = buffer)
                    {
                        // Get packet index.
                        ushort packetIdx =
                        (ushort)(
                            (ushort)(packetBufferHedderPt[TPEGConstant.PACKET_IDX_BE] << 8) +
                            packetBufferHedderPt[TPEGConstant.PACKET_IDX_LE]
                        );

                        if (packetIdx > packetNum) break;

                        // Get frame's index.
                        byte frameOffset = packetBufferHedderPt[TPEGConstant.FRAME_OFFSET_IDX];

                        if (frameOffset > TPEGConstant.FRAME_BUFFER_NUM) break;

                        if(packetBufferHedderPt[TPEGConstant.IS_THIS_FIX_PACKET_IDX] ==
                        TPEGConstant.THIS_PACKET_IS_NOT_FOR_FIX)
                        {
                            // Perform lock processing so that conflicts do not occur in queue operations.
                            queueMutex.WaitOne();

                            if (frameOffset != lastFrameIdx)
                            {
                                // The next frame arrives before all packets have been received.

                                // Get last packet index.
                                ushort lastPacketEndIdx =
                                (ushort)(
                                    (ushort)(packetBufferHedderPt[TPEGConstant.LAST_PACKET_IDX_BE] << 8) +
                                    packetBufferHedderPt[TPEGConstant.LAST_PACKET_IDX_LE]
                                );

                                if (lastPacketEndIdx > packetNum)
                                {
                                    queueMutex.ReleaseMutex();
                                    break;
                                }

#if false
                            for (int i = lastPacketIdx + 1; i < lastPacketEndIdx; i++)
                            {
                                // Retransmission request for packets
                                // that did not arrive in the previous frame.

                                // Enqueue index's big endian.
                                lostPacketQueue.Enqueue((byte)(i >> 8));

                                // Enqueue index's little endian.
                                lostPacketQueue.Enqueue((byte)i);

                                // Enqueue last frame's index.
                                lostPacketQueue.Enqueue((byte)lastFrameIdx);
                            }
#endif

                                lostPacketQueue.Enqueue((ushort)(lastPacketIdx + 1));
                                lostPacketQueue.Enqueue(lastPacketEndIdx);
                                lostPacketQueue.Enqueue(lastFrameIdx);

                                // Enqueue last frame request's finish flag.
                                lostPacketQueue.Enqueue(ushort.MaxValue);
                                lostPacketQueue.Enqueue(ushort.MaxValue);
                                lostPacketQueue.Enqueue(lastFrameIdx);

#if false
                            for (int i = 0; i < packetIdx; i++)
                            {
                                // Request to resend the packet of the current frame.

                                // Enqueue index's big endian.
                                lostPacketQueue.Enqueue((byte)(i >> 8));

                                // Enqueue index's little endian.
                                lostPacketQueue.Enqueue((byte)i);

                                // Enqueue current frame's index.
                                lostPacketQueue.Enqueue((byte)frameOffset);
                            }
#endif
                                lostPacketQueue.Enqueue(0);
                                lostPacketQueue.Enqueue(packetIdx);
                                lostPacketQueue.Enqueue(frameOffset);
                            }
                            else
                            {
#if false
                            // Request to resend the packet of the current frame.
                            for (int i = lastPacketIdx + 1; i < packetIdx; i++)
                            {
                                // Request to resend the packet of the current frame.

                                // Enqueue index's big endian.
                                lostPacketQueue.Enqueue((byte)(i >> 8));

                                // Enqueue index's little endian.
                                lostPacketQueue.Enqueue((byte)i);

                                // Enqueue current frame's index.
                                lostPacketQueue.Enqueue((byte)frameOffset);
                            }
#endif
                                lostPacketQueue.Enqueue((ushort)(lastPacketIdx + 1));
                                lostPacketQueue.Enqueue(packetIdx);
                                lostPacketQueue.Enqueue(frameOffset);
                            }

                            // update last packet index.
                            lastPacketIdx = packetIdx;

                            // update last frame index.
                            lastFrameIdx = frameOffset;

                            if (packetBufferHedderPt[TPEGConstant.IS_THIS_PACKET_END_IDX] ==
                            TPEGConstant.THIS_PACKET_IS_FRAMES_LAST)
                            {
                                // Enqueue current frame request's finish flag.

                                // If there is no packet to resend,
                                // the end flag will be detected immediately
                                // on the resending thread side,
                                // and the texture will be updated immediately.

                                lostPacketQueue.Enqueue(ushort.MaxValue);
                                lostPacketQueue.Enqueue(ushort.MaxValue);
                                lostPacketQueue.Enqueue(frameOffset);
                                queueMutex.ReleaseMutex();

                                // If packet's "is this packet end flag" is true.
                                // update last frame index and last pacekt index.

                                // Update last frame index.
                                lastFrameIdx = (byte)((lastFrameIdx + 1) & TPEGConstant.FRAME_BUFFER_LOOP_NUM);

                                // Update last packet index.
                                lastPacketIdx = 0;

                                continue;
                            }

                            // Release mutex so other process can access lost packet queue.
                            queueMutex.ReleaseMutex();
                        }

                        //////////////////////////////////////////////////////////////////////////
                        // Copy received data
                        //

                        fixed (byte* encBufferOffsetStart = encBuffer[frameOffset])
                        {
                            // Get dct block's start index.
                            byte* dctBlockHedderPt = packetBufferHedderPt + TPEGConstant.PACKET_HEDDER_SIZE;
                            byte* dctBlockYCrCbPt = dctBlockHedderPt + TPEGConstant.BLOCK_HEDDER_SIZE;

                            ushort tmpCopySize;
                            byte* encBufferOffset;

                            while (true)
                            {
                                // Get current block index.
                                ushort blockIdx =
                                (ushort)(((ushort)dctBlockHedderPt[TPEGConstant.BLOCK_IDX_BE] << 8) +
                                (ushort)dctBlockHedderPt[TPEGConstant.BLOCK_IDX_LE]);

                                if (blockIdx > blockWidth * blockHeight) break;

                                // Calc encBuffer's copy start index from block index.
                                encBufferOffset =
                                encBufferOffsetStart + blockIdx * TPEGConstant.BLOCK_OFFSET_SIZE;

                                tmpCopySize =
                                (ushort)(
                                    (ushort)dctBlockHedderPt[TPEGConstant.Y_SIZE_IDX] <<
                                    TPEGConstant.ENDIAN_SIZE_LOG2
                                );
                                if (tmpCopySize > TPEGConstant.BLOCK_SIZE * TPEGConstant.ENDIAN_SIZE) break;
                                LongCopy(dctBlockYCrCbPt, encBufferOffset, tmpCopySize);
                                encBufferOffset += TPEGConstant.BLOCK_SIZE * TPEGConstant.ENDIAN_SIZE;
                                dctBlockYCrCbPt += tmpCopySize;

                                tmpCopySize =
                                (ushort)(
                                    (ushort)dctBlockHedderPt[TPEGConstant.Cr_SIZE_IDX] <<
                                    TPEGConstant.ENDIAN_SIZE_LOG2
                                );
                                if (tmpCopySize > TPEGConstant.BLOCK_SIZE * TPEGConstant.ENDIAN_SIZE) break;
                                LongCopy(dctBlockYCrCbPt, encBufferOffset, tmpCopySize);
                                encBufferOffset += TPEGConstant.BLOCK_SIZE * TPEGConstant.ENDIAN_SIZE;
                                dctBlockYCrCbPt += tmpCopySize;

                                tmpCopySize =
                                (ushort)(
                                    (ushort)dctBlockHedderPt[TPEGConstant.Cb_SIZE_IDX] <<
                                    TPEGConstant.ENDIAN_SIZE_LOG2
                                );
                                if (tmpCopySize > TPEGConstant.BLOCK_SIZE * TPEGConstant.ENDIAN_SIZE) break;
                                LongCopy(dctBlockYCrCbPt, encBufferOffset, tmpCopySize);
                                encBufferOffset += TPEGConstant.BLOCK_SIZE * TPEGConstant.ENDIAN_SIZE;
                                dctBlockYCrCbPt += tmpCopySize;

                                dctBlockHedderPt = dctBlockYCrCbPt;
                                dctBlockYCrCbPt += TPEGConstant.BLOCK_HEDDER_SIZE;
                            }
                        }
                    }
                }
            }
        });

        Thread udpFixReqThread = new Thread(() =>
        {
            // Transmission thread for resend requests.
            // Separate sockets because an exception will occur
            // if transmission processing is performed during reception.

            Socket socket = tlabSocket.GetSocketFromID(clientPort + 1, serverPort + 1, serverAddr, 1);
            EndPoint remote = tlabSocket.GetRemoteFromID(1);

            ushort waitStart = 0;
            ushort waitEnd = 0;
            byte waitFrameIdx = 0;

            while (true)
            {
                // Wait until the request is added to the
                // packet retransmission waiting queue..
                while (lostPacketQueue.Count < 3) { }

                queueMutex.WaitOne();
                waitStart = lostPacketQueue.Dequeue();
                waitEnd = lostPacketQueue.Dequeue();
                waitFrameIdx = (byte)lostPacketQueue.Dequeue();
                queueMutex.ReleaseMutex();

                if (keepAlive == false) break;

                if (waitStart + waitEnd == ushort.MaxValue * 2)
                {
                    updateMutex.WaitOne();
                    textureUpdateFlag = waitFrameIdx;
                    updateMutex.ReleaseMutex();
                    continue;
                }

                for(ushort i = waitStart; i < waitEnd; i++)
                {
#region SocketException
                    // ソケットが受信待ちのタイミングで送信処理を行おうとすると
                    // SocketException:
                    // "既存の接続がリモートホストによって強制的
                    // に閉じられました"が発生することに注意.
                    // 例外はレシーブ処理で受信することになる．
#endregion
                    socket.SendTo(
                        new byte[]
                        {
                            (byte)(i >> 8),
                            (byte)i,
                            waitFrameIdx
                        },
                        remote
                    );
                }
            }
        });

        keepAlive = true;

        udpMainThread.Start();
        udpFixReqThread.Start();
    }

    public IEnumerator TLabCloseSocketAsync()
    {
        // use this funciton doing runtime.

        if (shareing == false)
        {
            Debug.Log("Socket is already closed");

            yield break;
        }

        keepAlive = false;

        tlabSocket.CloseAllSocket();

        float timer;
        float count;

        Debug.Log("wait for all socket closed.");

        timer = 0;
        count = 2f;

        while (timer < count)
        {
            timer += Time.deltaTime;

            yield return 0;
        }

        queueMutex.WaitOne();

        // Add an element to the queue and break out of the while() in the block.
        lostPacketQueue.Enqueue(0);
        lostPacketQueue.Enqueue(0);
        lostPacketQueue.Enqueue(0);

        queueMutex.ReleaseMutex();

        Debug.Log("wait for udp fix req thread closed.");

        timer = 0;
        count = 2f;

        while (timer < count)
        {
            timer += Time.deltaTime;

            yield return 0;
        }

        shareing = false;
        nowClosing = false;

        Debug.Log("finish close socket.");

        yield break;
    }

    public void ComputeShaderDispacheTest()
    {
        void TPEGDecoderTest()
        {
            // Get struct size.
            int sizeofInt = UnsafeUtility.SizeOf<int>();
            int sizeofByte = UnsafeUtility.SizeOf<byte>();
            int sizeofFloat = UnsafeUtility.SizeOf<float>();

            Debug.Log("UnsageUtility.SizeOf<int>(): " + sizeofInt.ToString());
            Debug.Log("UnsageUtility.SizeOf<byte>(): " + sizeofByte.ToString());
            Debug.Log("UnsageUtility.SizeOf<float>(): " + sizeofFloat.ToString());

            int encBufferSize = EncBufferArraySize(screenWidth, screenHeight);
            int dctBufferSize = DCTBufferArraySize(screenWidth, screenHeight);

            // initalize TPEGDecoder.
            InitializeTPEGDecoder(encBufferSize, dctBufferSize);

            // Create encodedBlock buffer copy to destination.
            short[] testBuffer = new short[encBufferSize / sizeof(short)];
            for (int i = 0; i < testBuffer.Length; i += 2)
            {
                //////////////////////////////////
                // Get run's value
                // 
                // 1 1 1 1 1 1 1 1
                //       &
                // 0 1 1 1 1 1 1 0 (= 126)
                //
                // 0 0 1 1 1 1 1 1 (= 63)
                //

                // -32257 * 2 = -64514
                // -32513 * 2 = -65026
                testBuffer[i + 0] = -32257;
                testBuffer[i + 1] = -32513;
            }

            // Set exist buffer to grahics buffer.
            encodedBlockBuffer.SetData(testBuffer, 0, 0, testBuffer.Length);

            // Get kernel index.
            int kernel1 = TPEGDecoder.FindKernel("EntropyInvert");
            int kernel2 = TPEGDecoder.FindKernel("BufferTest");

            TPEGDecoder.SetBuffer(kernel2, "DCTBlockBuffer", dctBlockBuffer);

            TPEGDecoder.Dispatch(kernel1, blockWidth, blockHeight, TPEGConstant.YCrCb_SIZE);
            TPEGDecoder.Dispatch(kernel2, blockWidth, blockHeight, TPEGConstant.YCrCb_SIZE);

            Debug.Log("Compute shader test Dispatched.");

            // Buffer for result confirmation
            int[] testBuffer1 = new int[dctBufferSize];

            // Get buffer from GPU.
            dctBlockBuffer.GetData(testBuffer1);

            // Release graphics buffer.
            if (encodedBlockBuffer != null) encodedBlockBuffer.Release();
            if (dctBlockBuffer != null) dctBlockBuffer.Release();

            for (int i = 0; i < 180; i++)
                Debug.Log("testBuffer1[" + i.ToString() + "] :" + testBuffer1[i].ToString());
        }

        void GraphicsBufferTest()
        {
            int sizeofInt = UnsafeUtility.SizeOf<int>();
            int sizeofByte = UnsafeUtility.SizeOf<byte>();
            int sizeofFloat = UnsafeUtility.SizeOf<float>();

            int encBufferSize = EncBufferArraySize(screenWidth, screenHeight);

            byte[] testBuffer = new byte[encBufferSize];
            byte[] testBuffer1 = new byte[encBufferSize];

            for (int i = 0; i < testBuffer.Length; i++) testBuffer[i] = 128;

            GraphicsBuffer testGraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                testBuffer.Length * sizeofByte / sizeofInt,
                sizeof(int)
            );

            testGraphicsBuffer.SetData(testBuffer, 0, 0, testBuffer.Length);
            testGraphicsBuffer.GetData(testBuffer1, 0, 0, testBuffer1.Length);
            for (int i = 0; i < 64; i++)
                Debug.Log("testBuffer1[" + i.ToString() + "]: " + testBuffer1[i]);

            for (int i = 0; i < testBuffer.Length; i++) testBuffer[i] = 64;

            testGraphicsBuffer.SetData(testBuffer, 0, 0, testBuffer.Length);
            testGraphicsBuffer.GetData(testBuffer1, 0, 0, testBuffer1.Length);
            for (int i = 0; i < 64; i++)
                Debug.Log("testBuffer1[" + i.ToString() + "]: " + testBuffer1[i]);

            testGraphicsBuffer.Release();
        }

        if (testing == false)
        {
            Debug.Log("Now is not testing mode.");
            return;
        }

        // TPEGDecoderTest();

        GraphicsBufferTest();
    }

    public void ShareOnOffButton()
    {
        if(testing == true)
        {
            Debug.Log("Now testing mode.");
            return;
        }

        if (nowClosing == true)
        {
            Debug.Log("Now closing.");

            return;
        }

        if (shareing == true)
        {
            // Since the socket is already open, it will be closed.

            nowClosing = true;

            Debug.Log("Start socket closing");

            StartCoroutine("TLabCloseSocketAsync");

            connectButtonText.text = "Share On";

            return;
        }

        lostPacketQueue = new Queue<ushort>();

        connectButtonText.text = "Share Off";

        StartSharing();

        shareing = true;
    }

    void Start()
    {
        tlabSocket = new TLabUnitySocket();
        if (screenTextureCreated == false) CreateScreenTexture();
    }

    private void Update()
    {
        updateMutex.WaitOne();
        if(textureUpdateFlag < TPEGConstant.FRAME_BUFFER_NUM)
        {
            encodedBlockBuffer.SetData(encBuffer[textureUpdateFlag], 0, 0, encBufferSize);

            textureUpdateFlag = TPEGConstant.FRAME_BUFFER_NUM;

            updateMutex.ReleaseMutex();

            // Entropy invert.
            TPEGDecoder.Dispatch(0, blockWidth, blockHeight, TPEGConstant.YCrCb_SIZE);

            // DCT invert.
            TPEGDecoder.Dispatch(1, blockWidth, blockHeight, 1);

            return;
        }
        updateMutex.ReleaseMutex();
    }

    void OnApplicationQuit()
    {
        if (queueMutex != null) queueMutex.Dispose();
        if (updateMutex != null) updateMutex.Dispose();
        if(encodedBlockBuffer != null) encodedBlockBuffer.Release();
        if(dctBlockBuffer != null) dctBlockBuffer.Release();
    }
}
