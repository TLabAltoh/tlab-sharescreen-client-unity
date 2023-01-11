static class TPEGConstant
{
    // DCT block's axis size.
    public const byte BLOCK_AXIS_SIZE = 8;

    // BLOCK_AXIS_SIZE's log2()
    public const byte BLOCK_AXIS_SIZE_LOG2 = 3;

    // DCT block's block size.
    public const byte BLOCK_SIZE = 64;

    // BLOCK_SIZE's log2()
    public const byte BLOCK_SIZE_LOG2 = 6;

    // Block offset size for copy array.
    public const short BLOCK_OFFSET_SIZE = BLOCK_SIZE * YCrCb_SIZE * ENDIAN_SIZE;

    // Block unit size for pointer Increment.
    public const short BLOCK_UNIT_SIZE = BLOCK_HEDDER_SIZE + BLOCK_OFFSET_SIZE;

    // 16bit data's byte size.
    public const byte ENDIAN_SIZE = 2;

    // ENDIAN_SIZE's log2()
    public const byte ENDIAN_SIZE_LOG2 = 1;

    // encBuffer's frame count.
    public const byte FRAME_BUFFER_NUM = 2;

    // For loop frame index.
    public const byte FRAME_BUFFER_LOOP_NUM = 1;

    // YCrCb's cannel size.
    public const byte YCrCb_SIZE = 3;

    /////////////////////////////////////////////////////////////////
    // Network
    // 

    // Packet's dct block size:
    public const short DCT_BLOCK_SIZE = 1443;

    // MTU size(1500).
    public const short MTU = PACKET_HEDDER_SIZE + DCT_BLOCK_SIZE;

    /////////////////////////////////////////////////////////////////
    // Packet hedder
    //

    // Packet hedder's composition:
    // CURRENT_IDX : 2BYTE
    // LAST_FRAME_OFFSET_IDX : 2BYTE
    // FRAME_OFFSET_IDX : 1BYTE
    // IS_THIS_FRAME_END : 1BYTE
    // IS_THSI_FIX_PACKET : 1BYTE

    // Packet hedder's size.
    public const byte PACKET_HEDDER_SIZE = 7;

    // Packet index little endian's index.
    public const byte PACKET_IDX_LE = 0;

    // Packet index big endian's index.
    public const byte PACKET_IDX_BE = 1;

    // Last packet's end index little endian's index.
    public const byte LAST_PACKET_IDX_LE= 2;

    // Last packet's end index big endian's index.
    public const byte LAST_PACKET_IDX_BE = 3;

    // Frame offset's index.
    public const byte FRAME_OFFSET_IDX = 4;

    // Packet is frame's end index flag's index.
    public const byte IS_THIS_PACKET_END_IDX = 5;

    // Packet is frame's fix packet flag's index.
    public const byte IS_THIS_FIX_PACKET_IDX = 6;

    // Packet is not frame's end index.
    public const byte THIS_PACKET_IS_NOT_FRAMES_LAST = 0;

    // Packet is frame's end index.
    public const byte THIS_PACKET_IS_FRAMES_LAST = 1;

    // Packet is not frame's fix packet.
    public const byte THIS_PACKET_IS_NOT_FOR_FIX = 0;

    // Packet is frame's fix packet.
    public const byte THIS_PACKET_IS_FOR_FIX = 1;

    /////////////////////////////////////////////////////////////////
    // Block hedder
    //

    // block's hedder composition:
    // BLOCK_IDX : 2BYTE
    // Y_BUFFER_SIZE : 1BYTE
    // Cr_BUFFER_SIZE : 1BYTE
    // Cb_BUFFER_SIZE : 1BYTE

    // Block hedder's size.
    public const byte BLOCK_HEDDER_SIZE = 5;

    // Block index little endian's index.
    public const int BLOCK_IDX_LE = 1;

    // Block index big endian's index.
    public const int BLOCK_IDX_BE = 0;

    // Block hedder's Y cannel size's index.
    public const int Y_SIZE_IDX = 2;

    // Block hedder's Cr cannel size's index.
    public const int Cr_SIZE_IDX = 3;

    // Block hedder's Cb cannel size's index.
    public const int Cb_SIZE_IDX = 4;
}
