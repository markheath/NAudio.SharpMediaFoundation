using System;
using System.IO;
using System.Runtime.InteropServices;
using NAudio.Wave;
using SharpDX.MediaFoundation;
using WaveFormatEncoding = SharpDX.Multimedia.WaveFormatEncoding;

namespace NAudio.SharpMediaFoundation
{
    public class SharpMediaFoundationReader : WaveStream
    {
        private readonly Stream stream;
        private readonly SourceReader reader;
        private WaveFormat waveFormat;
        private readonly long length;
        private long position;

        private byte[] decoderOutputBuffer;
        private int decoderOutputOffset;
        private int decoderOutputCount;

        public SharpMediaFoundationReader(string path)
        {
            MediaManager.Startup();
            reader = new SourceReader(path);
            Initialise();
            length = GetLength();
        }

        public SharpMediaFoundationReader(Stream stream)
        {
            this.stream = stream;
            MediaManager.Startup();
            reader = new SourceReader(stream);
            Initialise();
            length = GetLength();
        }

        private void Initialise()
        {
            reader.SetStreamSelection(SourceReaderIndex.AllStreams, false);
            reader.SetStreamSelection(SourceReaderIndex.FirstAudioStream, true);

            var partialMediaType = new MediaType();
            partialMediaType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
            partialMediaType.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Pcm);

            // set the media type
            // can return MF_E_INVALIDMEDIATYPE if not supported
            reader.SetCurrentMediaType(SourceReaderIndex.FirstAudioStream, partialMediaType);

            waveFormat = GetCurrentWaveFormat();
            reader.SetStreamSelection(SourceReaderIndex.FirstAudioStream, true);
        }

        private void EnsureBuffer(int bytesRequired)
        {
            if (decoderOutputBuffer == null || decoderOutputBuffer.Length < bytesRequired)
            {
                decoderOutputBuffer = new byte[bytesRequired];
            }
        }

        private long GetLength()
        {
            // http://msdn.microsoft.com/en-gb/library/windows/desktop/dd389281%28v=vs.85%29.aspx#getting_file_duration
            var variantValue = reader.GetPresentationAttribute(SourceReaderIndex.MediaSource, PresentationDescriptionAttributeKeys.Duration);
            var lengthInBytes = (variantValue * waveFormat.AverageBytesPerSecond) / 10000000L;
            return lengthInBytes;
        }

        private WaveFormat GetCurrentWaveFormat()
        {
            var uncompressedMediaType = reader.GetCurrentMediaType(SourceReaderIndex.FirstAudioStream);
            var sharpWf = uncompressedMediaType.ExtracttWaveFormat(out int _);

            return sharpWf.Encoding == WaveFormatEncoding.Pcm
                ? new WaveFormat(sharpWf.SampleRate, sharpWf.BitsPerSample, sharpWf.Channels)
                : WaveFormat.CreateIeeeFloatWaveFormat(sharpWf.SampleRate, sharpWf.Channels);
        }

        /// <summary>
        /// Reads from this wave stream
        /// </summary>
        /// <param name="buffer">Buffer to read into</param>
        /// <param name="offset">Offset in buffer</param>
        /// <param name="count">Bytes required</param>
        /// <returns>Number of bytes read; 0 indicates end of stream</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (repositionTo != -1)
            {
                Reposition(repositionTo);
            }

            int bytesWritten = 0;
            // read in any leftovers from last time
            if (decoderOutputCount > 0)
            {
                bytesWritten += ReadFromDecoderBuffer(buffer, offset, count - bytesWritten);
            }

            while (bytesWritten < count)
            {
                using var sample = reader.ReadSample(SourceReaderIndex.FirstAudioStream, 0, out int actualStreamIndex, out SourceReaderFlags dwFlags, out long timestamp);
                if ((dwFlags & SourceReaderFlags.Endofstream) != 0)
                {
                    // reached the end of the stream
                    break;
                }
                else if ((dwFlags & SourceReaderFlags.Currentmediatypechanged) != 0)
                {
                    waveFormat = GetCurrentWaveFormat();
                    throw new InvalidOperationException("Changed format midway through stream");

                    // carry on, but user must handle the change of format
                }
                else if (dwFlags != 0)
                {
                    throw new InvalidOperationException(String.Format("MediaFoundationReadError {0}", dwFlags));
                }

                using var mediaBuffer = sample.ConvertToContiguousBuffer();
                
                var pAudioData = mediaBuffer.Lock(out int pcbMaxLength, out int cbBuffer);
                EnsureBuffer(cbBuffer);
                Marshal.Copy(pAudioData, decoderOutputBuffer, 0, cbBuffer);
                decoderOutputOffset = 0;
                decoderOutputCount = cbBuffer;

                bytesWritten += ReadFromDecoderBuffer(buffer, offset + bytesWritten, count - bytesWritten);

                mediaBuffer.Unlock();
            }
            position += bytesWritten;
            return bytesWritten;
        }

        private int ReadFromDecoderBuffer(byte[] buffer, int offset, int needed)
        {
            int bytesFromDecoderOutput = Math.Min(needed, decoderOutputCount);
            Array.Copy(decoderOutputBuffer, decoderOutputOffset, buffer, offset, bytesFromDecoderOutput);
            decoderOutputOffset += bytesFromDecoderOutput;
            decoderOutputCount -= bytesFromDecoderOutput;
            if (decoderOutputCount == 0)
            {
                decoderOutputOffset = 0;
            }
            return bytesFromDecoderOutput;
        }

        public override WaveFormat WaveFormat
        {
            get { return waveFormat; }
        }

        public override long Length
        {
            get { return length; }
        }

        public override long Position
        {
            get { return position; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("value", "Position cannot be less than 0");

                repositionTo = value;
                position = value; // for gui apps, make it look like we have alread processed the reposition

            }
        }

        private long repositionTo = -1;

        private void Reposition(long desiredPosition)
        {
            // should pass in a variant of type VT_I8 which is a long containing time in 100nanosecond units
            long nsPosition = (10000000L * repositionTo) / waveFormat.AverageBytesPerSecond;
            reader.SetCurrentPosition(nsPosition);
            decoderOutputCount = 0;
            decoderOutputOffset = 0;
            position = desiredPosition;
            repositionTo = -1;// clear the flag
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (stream != null) stream.Dispose();
                if (reader != null) reader.Dispose();
            }
            
            base.Dispose(disposing);
        }
    }
}