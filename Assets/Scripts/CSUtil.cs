using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TLab.TPEG
{
    public static class CSUtil
    {
        public static void GraphicsBuffer(ref GraphicsBuffer graphicsBuffer, GraphicsBuffer.Target target, int count, int stride)
        {
            graphicsBuffer = new GraphicsBuffer(target, count, stride);
        }
    }
}
