public class OpusDecoder : IDisposable
{
    private IntPtr decoder;
    
    [DllImport("opus")]
    private static extern IntPtr opus_decoder_create(int sampleRate, int channels, out IntPtr error);
    
    [DllImport("opus")]
    private static extern int opus_decode(IntPtr decoder, byte[] data, int len, float[] pcm, int frameSize, int decodeFec);
    
    public OpusDecoder()
    {
        IntPtr error;
        decoder = opus_decoder_create(48000, 1, out error);
    }
    
    public float[] Decode(byte[] encodedData)
    {
        float[] pcm = new float[960]; // 20ms frame
        int samples = opus_decode(decoder, encodedData, encodedData.Length, pcm, 960, 0);
        
        if (samples <= 0) return null;
        
        // Trim to actual samples decoded
        float[] result = new float[samples];
        Array.Copy(pcm, result, samples);
        return result;
    }
    
    public void Dispose()
    {
        if (decoder != IntPtr.Zero)
        {
            opus_decoder_destroy(decoder);
            decoder = IntPtr.Zero;
        }
    }
}