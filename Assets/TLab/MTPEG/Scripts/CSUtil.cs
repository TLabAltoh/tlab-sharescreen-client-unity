using UnityEngine;

namespace TLab.MTPEG
{
    public static class CSUtil
    {
        public static void GraphicsBuffer(ref GraphicsBuffer graphics_buffer, GraphicsBuffer.Target target, int count, int stride)
        {
            if (graphics_buffer == null)
            {
                graphics_buffer = new GraphicsBuffer(target, count, stride);
            }
        }

        public static void DisposeBuffer(ref GraphicsBuffer graphics_buffer)
        {
            if (graphics_buffer != null)
            {
                graphics_buffer.Release();
                graphics_buffer.Dispose();
            }
        }
    }
}
