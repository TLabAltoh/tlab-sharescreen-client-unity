using UnityEngine;

namespace TLab.MTPEG
{
    public static class Constants
    {
        public const byte FALSE = 0;

        public const byte TRUE = 1;

        public const byte ENDIAN_SIZE = 2;  // 16bit data's byte size

        public const byte ENDIAN_SIZE_LOG2 = 1; // Log2(ENDIAN_SIZE)

        public const byte YCrCb_SIZE = 3;   // YCrCb's cannel size

        public const short BLOCK_OFFSET_SIZE = DCT.BLOCK_SIZE * YCrCb_SIZE * ENDIAN_SIZE;

        public const short BLOCK_UNIT_SIZE = BlockHedder.HEDDER_SIZE + BLOCK_OFFSET_SIZE;

        public const byte FRAME_NUM = 2;

        public const byte FRAME_LOOP_NUM = 1;

        public const short DGRAM_BUFFER_SIZE = 1443;

        public const short MTU = PacketHedder.HEDDER_SIZE + DGRAM_BUFFER_SIZE; // 1500

        public static class Decoder
        {
            public const string KERNEL_DCT_INVERT = "DCTInvert";

            public const string KERNEL_ENTROPY_INVERT = "EntropyInvert";

            public const string DECODED_TEXTURE = "DecodedTexture";

            public static int SCREEN_WIDTH = Shader.PropertyToID("_SCREEN_WIDTH");

            public static int SCREEN_HEIGHT = Shader.PropertyToID("_SCREEN_HEIGHT");

            public static int BLOCK_WIDTH = Shader.PropertyToID("_BLOCK_WIDTH");

            public static int BLOCK_HEIGHT = Shader.PropertyToID("_BLOCK_HEIGHT");

            public static int ENCODED_FRAME_BUFFER = Shader.PropertyToID("EncodedFrameBuffer");

            public static int DCT_BLOCK_BUFFER = Shader.PropertyToID("DCTBlockBuffer");
        }

        public static class DCT
        {
            public const byte BLOCK_AXIS_SIZE = 8;

            public const byte BLOCK_AXIS_SIZE_LOG2 = 3; // Log2(BLOCK_AXIS_SIZE)

            public const byte BLOCK_SIZE = 64;  // Pow(BLOCK_AXIS_SIZE, 2)

            public const byte BLOCK_SIZE_LOG2 = 6;  // Log2(BLOCK_SIZE)
        }

        public static class PacketHedder
        {
            // hedder composition
            // PACKET_INDEX_LE : 1BYTE
            // PACKET_INDEX_BE : 1BYTE
            // LAST_PACKET_LE : 1BYTE
            // LAST_PACKET_BE : 1BYTE
            // FRAME_OFFSET : 1BYTE
            // IS_THIS_FRAME_END : 1BYTE
            // IS_THSI_FIX_PACKET : 1BYTE

            public const byte HEDDER_SIZE = 7;   // packet hedder's size

            public const byte PACKET_INDEX_LE = 0;    // packet index's little endian

            public const byte PACKET_INDEX_BE = 1;    // packet index's big endian

            public const byte LAST_PACKET_INDEX_LE = 2;   // last packet's final index's little endian

            public const byte LAST_PACKET_INDEX_BE = 3;   // last packet's final index's big endian

            public const byte FRAME_OFFSET = 4; // frame offset index

            public const byte IS_THIS_PACKET_END = 5;   // packet is frame's final index

            public const byte IS_THIS_FIX_PACKET = 6;   // packet is frame's fix packet
        }

        public static class BlockHedder
        {
            // hedder composition
            // BLOCK_LE : 1BYTE
            // BLOCK_BE : 1BYTE
            // Y_BUFFER_SIZE : 1BYTE
            // Cr_BUFFER_SIZE : 1BYTE
            // Cb_BUFFER_SIZE : 1BYTE

            public const byte HEDDER_SIZE = 5;    // block hedder's size

            public const int BLOCK_INDEX_LE = 1;  // block index's little endian

            public const int BLOCK_INDEX_BE = 0;  // block index's big endian

            public const int Y_SIZE = 2;    // block hedder's Y cannel size

            public const int Cr_SIZE = 3;   // block hedder's Cr cannel size

            public const int Cb_SIZE = 4;   // block hedder's Cb cannel size

            public static int[] CHANNEL_SIZE_IDX = new int[] { Y_SIZE, Cr_SIZE, Cb_SIZE };
        }
    }
}