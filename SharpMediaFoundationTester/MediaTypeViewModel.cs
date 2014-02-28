using System;
using System.Text;
using NAudio.Utils;
using SharpDX;
using SharpDX.MediaFoundation;

namespace SharpMediaFoundationTester
{
    internal class MediaTypeViewModel
    {
        public MediaTypeViewModel()
        {
            
        }

        public MediaTypeViewModel(MediaType mediaType)
        {
            this.MediaType = mediaType;
            this.Name = ShortDescription(mediaType);
            this.Description = DescribeMediaType(mediaType);
        }

        public MediaType MediaType { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        private string ShortDescription(MediaType mediaType)
        {
            Guid subType = mediaType.Get(MediaTypeAttributeKeys.Subtype);
            int sampleRate = mediaType.Get(MediaTypeAttributeKeys.AudioSamplesPerSecond);
            int bytesPerSecond = mediaType.Get(MediaTypeAttributeKeys.AudioAvgBytesPerSecond);
            int channels = mediaType.Get(MediaTypeAttributeKeys.AudioNumChannels);
            // TODO: this one is optional
            int bitsPerSample = -1;
            try
            {
                bitsPerSample = mediaType.Get(MediaTypeAttributeKeys.AudioBitsPerSample);
            }
            catch (SharpDXException)
            {
                
                // key doesn't exist
            }
             

            //int bitsPerSample;
            //mediaType.GetUINT32(MediaFoundationAttributes.MF_MT_AUDIO_BITS_PER_SAMPLE, out bitsPerSample);
            var shortDescription = new StringBuilder();
            shortDescription.AppendFormat("{0:0.#}kbps, ", (8 * bytesPerSecond) / 1000M);
            shortDescription.AppendFormat("{0:0.#}kHz, ", sampleRate / 1000M);
            if (bitsPerSample != -1)
                shortDescription.AppendFormat("{0} bit, ", bitsPerSample);
            shortDescription.AppendFormat("{0}, ", channels == 1 ? "mono" : channels == 2 ? "stereo" : channels + " channels");
            if (subType == AudioFormatGuids.Aac)
            {
                int payloadType = -1;
                try
                {
                    payloadType = mediaType.Get(MediaTypeAttributeKeys.AacPayloadType);
                }
                catch (SharpDXException)
                {
                    // key doesn't exist
                }
                
                if (payloadType != -1)
                    shortDescription.AppendFormat("Payload Type: {0}, ", (AacPayloadType)payloadType);
            }
            shortDescription.Length -= 2;
            return shortDescription.ToString();
        }

        private string DescribeMediaType(MediaType mediaType)
        {
            int attributeCount = mediaType.Count;
            var sb = new StringBuilder();
            for (int n = 0; n < attributeCount; n++)
            {
                Guid key;
                var val = MediaType.GetByIndex(n, out key);
                string propertyName = FieldDescriptionHelper.Describe(typeof(NAudio.MediaFoundation.MediaFoundationAttributes), key);
                sb.AppendFormat("{0}={1}\r\n", propertyName, val);
                //val.Clear();
            }
            return sb.ToString();
        }
    }
}