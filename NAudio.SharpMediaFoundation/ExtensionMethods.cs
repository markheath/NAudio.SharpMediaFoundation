using System;
using System.Runtime.InteropServices;
using NAudio.Wave;
using SharpDX.MediaFoundation;

namespace NAudio.SharpMediaFoundation
{
    static class ExtensionMethods
    {
        public static SharpDX.Multimedia.WaveFormat ToSharpDxWaveFormat(this WaveFormat waveFormat)
        {
            if (!waveFormat.IsPcmOrIeeeFloat()) throw new ArgumentException("Only PCM and IEEE float supported");
            var wfe = waveFormat as WaveFormatExtensible;
            if (wfe == null) // || (wfe != null && (wfe.SubFormat == AudioSubtypes.MFAudioFormat_PCM))
            {
                return new SharpDX.Multimedia.WaveFormat(waveFormat.SampleRate, waveFormat.BitsPerSample, waveFormat.Channels);
            }
            return new SharpDX.Multimedia.WaveFormatExtensible(waveFormat.SampleRate, waveFormat.BitsPerSample, waveFormat.Channels);
        }

        public static bool IsPcmOrIeeeFloat(this WaveFormat waveFormat)
        {
            var wfe = waveFormat as WaveFormatExtensible;
            return waveFormat.Encoding == WaveFormatEncoding.Pcm ||
                   waveFormat.Encoding == WaveFormatEncoding.IeeeFloat ||
                   (wfe != null && (wfe.SubFormat == AudioFormatGuids.Pcm
                                    || wfe.SubFormat == AudioFormatGuids.Float));
        }

        public static MediaType ToMediaType(this WaveFormat waveFormat)
        {
            var audioMediaType = new MediaType();
            var inputWaveFormat = waveFormat.ToSharpDxWaveFormat();
            MediaFactory.InitMediaTypeFromWaveFormatEx(audioMediaType, new[] { inputWaveFormat }, Marshal.SizeOf(waveFormat));
            return audioMediaType;
        }
    }
}