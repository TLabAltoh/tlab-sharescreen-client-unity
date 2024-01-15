using UnityEngine;
using static TLab.MTPEG.Constants;

namespace TLab.MTPEG
{
    public static unsafe class MTPEGUtil
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
            byte[][] encoded_frame_buffer = new byte[FRAME_NUM][];

            for (int i = 0; i < FRAME_NUM; i++)
            {
                encoded_frame_buffer[i] = new byte[size];
            }

            return encoded_frame_buffer;
        }

        public static int EncodedFrameSize(int width, int height)
        {
            return width * height * YCrCb_SIZE * ENDIAN_SIZE;
        }

        public static int DecodedFrameSize(int width, int height)
        {
            return width * height * YCrCb_SIZE;
        }

        public static void LongCopy(byte* src, byte* dst, int count)
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

        public static bool File2EncodedFrameBuffer(ref byte[] encoded_frame_buffer, string path, uint block_width, uint block_height)
        {
            System.IO.StreamReader sr = new System.IO.StreamReader(path, false);
            Debug.Log($"file hedder: {sr.ReadLine()}");  // ignore file hedder

            uint block_idx = 0;

            sr.BaseStream.Position = 5;

            while (!sr.EndOfStream)
            {
                byte block_index_be = (byte)sr.BaseStream.ReadByte();
                byte block_index_le = (byte)sr.BaseStream.ReadByte();
                byte size_b = (byte)sr.BaseStream.ReadByte();
                byte size_g = (byte)sr.BaseStream.ReadByte();
                byte size_r = (byte)sr.BaseStream.ReadByte();

                if (block_idx < 64)
                {
                    Debug.Log($"block_index_be: {block_index_be}");
                    Debug.Log($"block_index_le: {block_index_le}");
                    Debug.Log($"size_b: {size_b}");
                    Debug.Log($"size_g: {size_g}");
                    Debug.Log($"size_r: {size_r}");
                }

                ushort block_index = (ushort)((ushort)(block_index_be << 8) | block_index_le);
                if (block_index > block_width * block_height)
                {
                    // ignore this packet to prevent out of range index
                    break;
                }

                fixed (byte* encoded_frame_buffer_ptr = encoded_frame_buffer)
                {
                    byte* encoded_frame_buffer_copy_ptr = encoded_frame_buffer_ptr + block_index * BLOCK_OFFSET_SIZE;

                    foreach (var size in new byte[] { size_b, size_g, size_r })
                    {
                        ushort copy_length = (ushort)(size << ENDIAN_SIZE_LOG2);
                        if (copy_length > DCT.BLOCK_SIZE * ENDIAN_SIZE)
                        {
                            break;
                        }

                        for (int i = 0; i < copy_length; i++)
                        {
                            *(encoded_frame_buffer_copy_ptr + i) = (byte)sr.BaseStream.ReadByte();

                            if (block_idx < 64)
                            {
                                Debug.Log($"[{i}]: {*(encoded_frame_buffer_copy_ptr + i)}");
                                block_idx++;
                            }
                        }
                        encoded_frame_buffer_copy_ptr += DCT.BLOCK_SIZE * ENDIAN_SIZE;
                    }
                }
            }

            return true;
        }
    }
}
