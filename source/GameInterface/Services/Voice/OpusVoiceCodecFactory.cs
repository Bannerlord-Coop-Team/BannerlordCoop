using Common.Voice;
using Concentus.Enums;
using Concentus.Structs;
using System;

namespace GameInterface.Services.Voice;

public interface IVoiceCodecFactory
{
    IVoiceEncoder CreateEncoder();
    IVoiceDecoder CreateDecoder();
}

public sealed class OpusVoiceCodecFactory : IVoiceCodecFactory
{
    public IVoiceEncoder CreateEncoder() => new Encoder();
    public IVoiceDecoder CreateDecoder() => new Decoder();

    private sealed class Encoder : IVoiceEncoder
    {
        private readonly OpusEncoder encoder = OpusEncoder.Create(48000, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        public Encoder()
        {
            encoder.Bitrate = 24000;
            encoder.Complexity = 5;
            encoder.UseVBR = true;
        }
        public byte[] Encode(short[] samples)
        {
            var output = new byte[VoicePacket.MaximumPayload];
            int length = encoder.Encode(samples, 0, 960, output, 0, output.Length);
            Array.Resize(ref output, length);
            return output;
        }
    }

    private sealed class Decoder : IVoiceDecoder
    {
        private readonly OpusDecoder decoder = OpusDecoder.Create(48000, 1);
        public short[] Decode(byte[] audio)
        {
            var samples = new short[960];
            int length = decoder.Decode(audio, 0, audio?.Length ?? 0, samples, 0, samples.Length, false);
            if (length != samples.Length) throw new InvalidOperationException("Invalid voice frame duration.");
            return samples;
        }
    }
}

public interface IVoiceEncoder
{
    byte[] Encode(short[] samples);
}

public interface IVoiceDecoder
{
    short[] Decode(byte[] audio);
}
