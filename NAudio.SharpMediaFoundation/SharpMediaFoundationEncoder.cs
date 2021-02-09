using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NAudio.Utils;
using NAudio.Wave;
using SharpDX;
using SharpDX.MediaFoundation;
using MediaType = SharpDX.MediaFoundation.MediaType;

namespace NAudio.SharpMediaFoundation
{
    /// <summary>
    /// Media Foundation Encoder class allows you to use Media Foundation to encode an IWaveProvider
    /// to any supported encoding format
    /// </summary>
    public class SharpMediaFoundationEncoder : IDisposable
    {
        private const int MF_E_NOT_FOUND = unchecked((int)0xC00D36D5);

        /// <summary>
        /// Queries the available bitrates for a given encoding output type, sample rate and number of channels
        /// </summary>
        /// <param name="audioSubtype">Audio subtype - a value from the AudioSubtypes class</param>
        /// <param name="sampleRate">The sample rate of the PCM to encode</param>
        /// <param name="channels">The number of channels of the PCM to encode</param>
        /// <returns>An array of available bitrates in average bits per second</returns>
        public static int[] GetEncodeBitrates(Guid audioSubtype, int sampleRate, int channels)
        {
            return GetOutputMediaTypes(audioSubtype)
                .Where(mt => mt.Get(MediaTypeAttributeKeys.AudioSamplesPerSecond) == sampleRate && 
                    mt.Get(MediaTypeAttributeKeys.AudioNumChannels) == channels)
                .Select(mt => mt.Get(MediaTypeAttributeKeys.AudioAvgBytesPerSecond) * 8)
                .Distinct()
                .OrderBy(br => br)
                .ToArray();
        }

        /// <summary>
        /// Gets all the available media types for a particular 
        /// </summary>
        /// <param name="audioSubtype">Audio subtype - a value from the AudioSubtypes class</param>
        /// <returns>An array of available media types that can be encoded with this subtype</returns>
        public static MediaType[] GetOutputMediaTypes(Guid audioSubtype)
        {
            Collection availableTypes;
            try
            {
                availableTypes = MediaFactory.TranscodeGetAudioOutputAvailableTypes(audioSubtype, TransformEnumFlag.All, null);
            }
            catch (SharpDXException c)
            {
                if (c.ResultCode.Code == ResultCode.NotFound.Code)
                {
                    // Don't worry if we didn't find any - just means no encoder available for this type
                    return new MediaType[0];
                }
                throw;
            }
            int count = availableTypes.ElementCount;
            var mediaTypes = new List<MediaType>(count);
            for (int n = 0; n < count; n++)
            {
                var mediaTypeObject = (ComObject)availableTypes.GetElement(n);
                mediaTypes.Add(new MediaType( mediaTypeObject.NativePointer));
            }
            availableTypes.Dispose();
            return mediaTypes.ToArray();
        }

        /// <summary>
        /// Helper function to simplify encoding Window Media Audio
        /// Should be supported on Vista and above (not tested)
        /// </summary>
        /// <param name="inputProvider">Input provider, must be PCM</param>
        /// <param name="outputFile">Output file path, should end with .wma</param>
        /// <param name="desiredBitRate">Desired bitrate. Use GetEncodeBitrates to find the possibilities for your input type</param>
        public static void EncodeToWma(IWaveProvider inputProvider, string outputFile, int desiredBitRate = 192000)
        {
            var mediaType = SelectMediaType(AudioFormatGuids.WMAudioV8, inputProvider.WaveFormat, desiredBitRate);
            if (mediaType == null) throw new InvalidOperationException("No suitable WMA encoders available");
            using var encoder = new SharpMediaFoundationEncoder(mediaType);
            encoder.Encode(outputFile, inputProvider);
        }

        /// <summary>
        /// Helper function to simplify encoding to MP3
        /// By default, will only be available on Windows 8 and above
        /// </summary>
        /// <param name="inputProvider">Input provider, must be PCM</param>
        /// <param name="outputFile">Output file path, should end with .mp3</param>
        /// <param name="desiredBitRate">Desired bitrate. Use GetEncodeBitrates to find the possibilities for your input type</param>
        public static void EncodeToMp3(IWaveProvider inputProvider, string outputFile, int desiredBitRate = 192000)
        {
            var mediaType = SelectMediaType(AudioFormatGuids.Mp3, inputProvider.WaveFormat, desiredBitRate);
            if (mediaType == null) throw new InvalidOperationException("No suitable MP3 encoders available");
            using var encoder = new SharpMediaFoundationEncoder(mediaType);
            encoder.Encode(outputFile, inputProvider);
        }

        /// <summary>
        /// Helper function to simplify encoding to AAC
        /// By default, will only be available on Windows 7 and above
        /// </summary>
        /// <param name="inputProvider">Input provider, must be PCM</param>
        /// <param name="outputFile">Output file path, should end with .mp4 (or .aac on Windows 8)</param>
        /// <param name="desiredBitRate">Desired bitrate. Use GetEncodeBitrates to find the possibilities for your input type</param>
        public static void EncodeToAac(IWaveProvider inputProvider, string outputFile, int desiredBitRate = 192000)
        {
            // Information on configuring an AAC media type can be found here:
            // http://msdn.microsoft.com/en-gb/library/windows/desktop/dd742785%28v=vs.85%29.aspx
            var mediaType = SelectMediaType(AudioFormatGuids.Aac, inputProvider.WaveFormat, desiredBitRate);
            if (mediaType == null) throw new InvalidOperationException("No suitable AAC encoders available");
            using var encoder = new SharpMediaFoundationEncoder(mediaType);
            // should AAC container have ADTS, or is that just for ADTS?
            // http://www.hydrogenaudio.org/forums/index.php?showtopic=97442
            encoder.Encode(outputFile, inputProvider);
        }

        /// <summary>
        /// Tries to find the encoding media type with the closest bitrate to that specified
        /// </summary>
        /// <param name="audioSubtype">Audio subtype, a value from AudioSubtypes</param>
        /// <param name="inputFormat">Your encoder input format (used to check sample rate and channel count)</param>
        /// <param name="desiredBitRate">Your desired bitrate</param>
        /// <returns>The closest media type, or null if none available</returns>
        public static MediaType SelectMediaType(Guid audioSubtype, WaveFormat inputFormat, int desiredBitRate)
        {
            return GetOutputMediaTypes(audioSubtype)
                .Where(mt => mt.Get(MediaTypeAttributeKeys.AudioSamplesPerSecond) == inputFormat.SampleRate && 
                    mt.Get(MediaTypeAttributeKeys.AudioNumChannels) == inputFormat.Channels)
                .Select(mt => new { MediaType = mt, Delta = Math.Abs(desiredBitRate - mt.Get(MediaTypeAttributeKeys.AudioAvgBytesPerSecond) * 8) })
                .OrderBy(mt => mt.Delta)
                .Select(mt => mt.MediaType)
                .FirstOrDefault();
        }

        private readonly MediaType outputMediaType;
        private bool disposed;

        /// <summary>
        /// Creates a new encoder that encodes to the specified output media type
        /// </summary>
        /// <param name="outputMediaType">Desired output media type</param>
        public SharpMediaFoundationEncoder(MediaType outputMediaType)
        {
            MediaManager.Startup();
            this.outputMediaType = outputMediaType ?? throw new ArgumentNullException(nameof(outputMediaType));
        }

        /// <summary>
        /// Encodes a file
        /// </summary>
        /// <param name="outputFile">Output filename (container type is deduced from the filename)</param>
        /// <param name="inputProvider">Input provider (should be PCM, some encoders will also allow IEEE float)</param>
        public void Encode(string outputFile, IWaveProvider inputProvider)
        {
            if (inputProvider.WaveFormat.Encoding != WaveFormatEncoding.Pcm && inputProvider.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            {
                throw new ArgumentException("Encode input format must be PCM or IEEE float");
            }

            // possibly could use Marshaling to convert instead
            // given that input should be PCM or IEEE float, this should work just fine
            var sharpWf = SharpDX.Multimedia.WaveFormat.CreateCustomFormat(
                (SharpDX.Multimedia.WaveFormatEncoding) inputProvider.WaveFormat.Encoding,
                inputProvider.WaveFormat.SampleRate,
                inputProvider.WaveFormat.Channels,
                inputProvider.WaveFormat.AverageBytesPerSecond,
                inputProvider.WaveFormat.BlockAlign,
                inputProvider.WaveFormat.BitsPerSample);

            
            using var inputMediaType = new MediaType();
            var size = 18 + sharpWf.ExtraSize;
            
            MediaFactory.InitMediaTypeFromWaveFormatEx(inputMediaType, new[] { sharpWf }, size);

            using var writer = CreateSinkWriter(outputFile);
            writer.AddStream(outputMediaType, out int streamIndex);

            // n.b. can get 0xC00D36B4 - MF_E_INVALIDMEDIATYPE here
            writer.SetInputMediaType(streamIndex, inputMediaType, null);

            PerformEncode(writer, streamIndex, inputProvider);
        }

        private static SinkWriter CreateSinkWriter(string outputFile)
        {
            // n.b. could try specifying the container type using attributes, but I think
            // it does a decent job of working it out from the file extension 
            // n.b. AAC encode on Win 8 can have AAC extension, but use MP4 in win 7
            // http://msdn.microsoft.com/en-gb/library/windows/desktop/dd389284%28v=vs.85%29.aspx
            SinkWriter writer;
            using (var attributes = new MediaAttributes())
            {
                MediaFactory.CreateAttributes(attributes, 1);
                attributes.Set(SinkWriterAttributeKeys.ReadwriteEnableHardwareTransforms.Guid, (UInt32) 1);
                try
                {
                    writer = MediaFactory.CreateSinkWriterFromURL(outputFile, null, attributes);
                }
                catch (COMException e)
                {
                    if (e.GetHResult() == MF_E_NOT_FOUND)
                    {
                        throw new ArgumentException("Was not able to create a sink writer for this file extension");
                    }
                    throw;
                }
            }
            return writer;
        }

        private void PerformEncode(SinkWriter writer, int streamIndex, IWaveProvider inputProvider)
        {
            int maxLength = inputProvider.WaveFormat.AverageBytesPerSecond * 4;
            var managedBuffer = new byte[maxLength];

            writer.BeginWriting();

            long position = 0;
            long duration;
            do
            {
                duration = ConvertOneBuffer(writer, streamIndex, inputProvider, position, managedBuffer);
                position += duration;
            } while (duration > 0);

            writer.Finalize();
        }

        private static long BytesToNsPosition(int bytes, WaveFormat waveFormat)
        {
            long nsPosition = (10000000L * bytes) / waveFormat.AverageBytesPerSecond;
            return nsPosition;
        }

        private long ConvertOneBuffer(SinkWriter writer, int streamIndex, IWaveProvider inputProvider, long position, byte[] managedBuffer)
        {
            long durationConverted = 0;
            using var buffer = MediaFactory.CreateMemoryBuffer(managedBuffer.Length);          
            using var sample = MediaFactory.CreateSample();
            sample.AddBuffer(buffer);

            var ptr = buffer.Lock(out int maxLength, out int currentLength);
            int read = inputProvider.Read(managedBuffer, 0, maxLength);
            if (read > 0)
            {
                durationConverted = BytesToNsPosition(read, inputProvider.WaveFormat);
                Marshal.Copy(managedBuffer, 0, ptr, read);
                buffer.CurrentLength = read;
                buffer.Unlock();
                sample.SampleTime = position;
                sample.SampleDuration = durationConverted;
                writer.WriteSample(streamIndex, sample);
                //writer.Flush(streamIndex);
            }
            else
            {
                buffer.Unlock();
            }
            return durationConverted;
        }

        /// <summary>
        /// Disposes this instance
        /// </summary>
        /// <param name="disposing"></param>
        protected void Dispose(bool disposing)
        {
            outputMediaType.Dispose();            
        }

        /// <summary>
        /// Disposes this instance
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                Dispose(true);
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~SharpMediaFoundationEncoder()
        {
            Dispose(false);
        }
    }
}
