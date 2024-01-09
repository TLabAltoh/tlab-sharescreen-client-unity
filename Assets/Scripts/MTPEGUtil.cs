using static TLab.MTPEG.Constants;

namespace TLab.MTPEG
{
    public static class MTPEGUtil
    {
        /// <summary>
        /// adjust texture size to multiples of DCT.BLOCK_AXIS_SIZE
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public static void PixelTweak(ref int width, ref int height)
        {
            int tmp;
            float tmp1;
            float tmp2;

            tmp = width / DCT.BLOCK_AXIS_SIZE;
            tmp1 = (float)width / DCT.BLOCK_AXIS_SIZE;
            tmp2 = tmp1 - tmp;

            if (tmp2 > 0)
            {
                width = width + (int)(tmp2 * DCT.BLOCK_AXIS_SIZE);
            }

            tmp = height / DCT.BLOCK_AXIS_SIZE;
            tmp1 = (float)height / DCT.BLOCK_AXIS_SIZE;
            tmp2 = tmp1 - tmp;

            if (tmp2 > 0)
            {
                height = height + (int)(tmp2 * DCT.BLOCK_AXIS_SIZE);
            }

            return;
        }

        public static byte[][] CreateEncodedFrameBuffer(int size)
        {
            byte[][] encodedFrameBuffer = new byte[FRAME_NUM][];

            for (int i = 0; i < FRAME_NUM; i++)
            {
                encodedFrameBuffer[i] = new byte[size + BLOCK_OFFSET_SIZE];
            }

            return encodedFrameBuffer;
        }

        public static int EncodedFrameSize(int width, int height)
        {
            return width * height * YCrCb_SIZE * ENDIAN_SIZE;
        }

        public static int DecodedFrameSize(int width, int height)
        {
            return width * height * YCrCb_SIZE;
        }
    }
}
