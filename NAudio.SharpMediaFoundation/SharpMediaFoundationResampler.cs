using System;
using System.Runtime.InteropServices;
using NAudio.Wave;
using SharpDX.MediaFoundation;

namespace NAudio.SharpMediaFoundation
{
    /// <summary>
    /// The Media Foundation Resampler Transform
    /// </summary>
    public class SharpMediaFoundationResampler : SharpMediaFoundationTransform
    {
        private int resamplerQuality;

        /// <summary>
        /// Creates the Media Foundation Resampler, allowing modifying of sample rate, bit depth and channel count
        /// </summary>
        /// <param name="sourceProvider">Source provider, must be PCM</param>
        /// <param name="outputFormat">Output format, must also be PCM</param>
        public SharpMediaFoundationResampler(IWaveProvider sourceProvider, WaveFormat outputFormat)
            : base(sourceProvider, outputFormat)
        {
            if (!sourceProvider.WaveFormat.IsPcmOrIeeeFloat())
                throw new ArgumentException("Input must be PCM or IEEE float", "sourceProvider");
            if (!outputFormat.IsPcmOrIeeeFloat())
                throw new ArgumentException("Output must be PCM or IEEE float", "outputFormat");
            MediaManager.Startup();
            ResamplerQuality = 60; // maximum quality

            // n.b. we will create the resampler COM object on demand in the Read method, 
            // to avoid threading issues but just
            // so we can check it exists on the system we'll make one so it will throw an 
            // exception if not exists
            var comObject = CreateResamplerComObject();
            FreeComObject(comObject);
        }

        /// <summary>
        /// Creates a resampler with a specified target output sample rate
        /// </summary>
        /// <param name="sourceProvider">Source provider</param>
        /// <param name="outputSampleRate">Output sample rate</param>
        public SharpMediaFoundationResampler(IWaveProvider sourceProvider, int outputSampleRate)
            : this(sourceProvider, CreateOutputFormat(sourceProvider.WaveFormat, outputSampleRate))
        {

        }

        private Activate activate;

        private void FreeComObject(object comObject)
        {
            if (activate != null) activate.ShutdownObject();
            Marshal.ReleaseComObject(comObject);
        }

        private object CreateResamplerComObject()
        {
#if NETFX_CORE            
            return CreateResamplerComObjectUsingActivator();
#else
            return new ResamplerMediaComObject();
#endif
        }

#if NETFX_CORE

        

#endif
        private static readonly Guid ResamplerClsid = new Guid("f447b69e-1884-4a7e-8055-346f74d6edb3");
        private static readonly Guid IMFTransformIid = new Guid("bf94c121-5b05-4e6f-8000-ba598961414d");

        private IntPtr CreateResamplerComObjectUsingActivator()
        {
            var activators = MediaFactory.FindTransform(TransformCategoryGuids.AudioEffect, TransformEnumFlag.All);
            foreach (var a in activators)
            {
                if (a.Get(TransformAttributeKeys.MftTransformClsidAttribute).Equals(ResamplerClsid))
                {
                    IntPtr comObjectPtr;
                    a.ActivateObject(IMFTransformIid, out comObjectPtr);
                    activate = a;
                    return comObjectPtr;
                }
            }
            return IntPtr.Zero;
        }
        /// <summary>
        /// Creates and configures the actual Resampler transform
        /// </summary>
        /// <returns>A newly created and configured resampler MFT</returns>
        protected override Transform CreateTransform(WaveFormat sourceWaveFormat)
        {
            var resamplerTransform = new Transform(ResamplerClsid);

            // Just to test we've
            //Debug.WriteLine("Transform {0}", resamplerTransform.Attributes.Count);

            using (var inputMediaType = sourceWaveFormat.ToMediaType())
            {
                resamplerTransform.SetInputType(0, inputMediaType, 0);
            }

            using (var outputMediaType = WaveFormat.ToMediaType())
            {
                resamplerTransform.SetOutputType(0, outputMediaType, 0);
            }

            //MFT_OUTPUT_STREAM_INFO pStreamInfo;
            //resamplerTransform.GetOutputStreamInfo(0, out pStreamInfo);
            // if pStreamInfo.dwFlags is 0, then it means we have to provide samples

            var o = Marshal.GetObjectForIUnknown(resamplerTransform.NativePointer);

            // setup quality
            var resamplerProps = (IWMResamplerProps)o;
            // 60 is the best quality, 1 is linear interpolation
            resamplerProps.SetHalfFilterLength(ResamplerQuality);
            // may also be able to set this using MFPKEY_WMRESAMP_CHANNELMTX on the
            // IPropertyStore interface.
            // looks like we can also adjust the LPF with MFPKEY_WMRESAMP_LOWPASS_BANDWIDTH
            return resamplerTransform;
        }

        /// <summary>
        /// Gets or sets the Resampler quality. n.b. set the quality before starting to resample.
        /// 1 is lowest quality (linear interpolation) and 60 is best quality
        /// </summary>
        public int ResamplerQuality
        {
            get { return resamplerQuality; }
            set
            {
                if (value < 1 || value > 60)
                    throw new ArgumentOutOfRangeException("value", "Resampler Quality must be between 1 and 60");
                resamplerQuality = value;
            }
        }

        private static WaveFormat CreateOutputFormat(WaveFormat inputFormat, int outputSampleRate)
        {
            WaveFormat outputFormat;
            if (inputFormat.Encoding == WaveFormatEncoding.Pcm)
            {
                outputFormat = new WaveFormat(outputSampleRate,
                    inputFormat.BitsPerSample,
                    inputFormat.Channels);
            }
            else if (inputFormat.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                outputFormat = WaveFormat.CreateIeeeFloatWaveFormat(outputSampleRate,
                    inputFormat.Channels);
            }
            else
            {
                throw new ArgumentException("Can only resample PCM or IEEE float");
            }
            return outputFormat;
        }

        /// <summary>
        /// Disposes this resampler
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (activate != null)
            {
                activate.ShutdownObject();
                activate = null;
            }

            base.Dispose(disposing);
        }

    }


    /// <summary>
    /// Windows Media Resampler Props
    /// wmcodecdsp.h
    /// </summary>
    [Guid("E7E9984F-F09F-4da4-903F-6E2E0EFE56B5"),
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IWMResamplerProps
    {
        /// <summary>
        /// Range is 1 to 60
        /// </summary>
        int SetHalfFilterLength(int outputQuality);

        /// <summary>
        ///  Specifies the channel matrix.
        /// </summary>
        int SetUserChannelMtx([In] float[] channelConversionMatrix);
    }

    [ComImport, Guid("f447b69e-1884-4a7e-8055-346f74d6edb3")]
    class ResamplerMediaComObject
    {
    }
}