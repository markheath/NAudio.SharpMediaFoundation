using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using NAudio.SharpMediaFoundation;
using NAudio.Wave;
using SharpDX.MediaFoundation;

namespace SharpMediaFoundationTester
{
    internal class MediaFoundationEncodeViewModel : ViewModelBase, IDisposable
    {
        private readonly Dictionary<Guid, List<MediaTypeViewModel>> allMediaTypes;
        private EncoderViewModel selectedOutputFormat;
        private MediaTypeViewModel selectedMediaType;
        private string inputFile;
        private string inputFormat;
        private WaveFormat inputWaveFormat;

        public List<EncoderViewModel> OutputFormats { get; private set; }
        public List<MediaTypeViewModel> SupportedMediaTypes { get; private set; }
        public ICommand EncodeCommand { get; private set; }
        public ICommand SelectInputFileCommand { get; private set; }

        public MediaFoundationEncodeViewModel()
        {
            //MediaFactory.Startup();
            allMediaTypes = new Dictionary<Guid, List<MediaTypeViewModel>>();
            SupportedMediaTypes = new List<MediaTypeViewModel>();
            EncodeCommand = new DelegateCommand(Encode);
            SelectInputFileCommand = new DelegateCommand(SelectInputFile);

            // TODO: fill this by asking the encoders what they can do
            OutputFormats = new List<EncoderViewModel>();
            OutputFormats.Add(new EncoderViewModel { Name = "AAC", Guid = AudioFormatGuids.Aac, Extension = ".mp4" }); // Windows 8 can do a .aac extension as well
            OutputFormats.Add(new EncoderViewModel { Name = "Windows Media Audio", Guid = AudioFormatGuids.WMAudioV8, Extension = ".wma" });
            OutputFormats.Add(new EncoderViewModel { Name = "Windows Media Audio Professional", Guid = AudioFormatGuids.WMAudioV9, Extension = ".wma" });
            OutputFormats.Add(new EncoderViewModel { Name = "MP3", Guid = AudioFormatGuids.Mp3, Extension = ".mp3" });
            OutputFormats.Add(new EncoderViewModel { Name = "Windows Media Audio Voice", Guid = AudioFormatGuids.MultisampledP1, Extension = ".wma" }); // AudioSubtypes.MFAudioFormat_MSP1
            OutputFormats.Add(new EncoderViewModel { Name = "Windows Media Audio Lossless", Guid = AudioFormatGuids.WMAudioLossless, Extension = ".wma" });
            OutputFormats.Add(new EncoderViewModel { Name = "Fake for testing", Guid = Guid.NewGuid(), Extension = ".xyz" });
            SelectedOutputFormat = OutputFormats[0];
        }

        private void SelectInputFile()
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "Audio files|*.mp3;*.wav;*.wma;*.aiff;*.aac";
            if (ofd.ShowDialog() == true)
            {
                if (TryOpenInputFile(ofd.FileName))
                {
                    InputFile = ofd.FileName;
                    SetMediaTypes();
                }
            }
        }

        private bool TryOpenInputFile(string file)
        {
            bool isValid = false;
            try
            {
                using (var reader = new SharpMediaFoundationReader(file))
                {
                    InputFormat = reader.WaveFormat.ToString();
                    inputWaveFormat = reader.WaveFormat;
                    isValid = true;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(String.Format("Not a supported input file ({0})", e.Message));
            }
            return isValid;
        }

        public string InputFile
        {
            get { return inputFile; }
            set
            {
                if (inputFile != value)
                {
                    inputFile = value;
                    OnPropertyChanged("InputFile");
                }
            }
        }

        public string InputFormat
        {
            get { return inputFormat; }
            set
            {
                if (inputFormat != value)
                {
                    inputFormat = value;
                    OnPropertyChanged("InputFormat");
                }
            }
        }

        public EncoderViewModel SelectedOutputFormat
        {
            get { return selectedOutputFormat; }
            set { 
                if (selectedOutputFormat != value)
                {
                    selectedOutputFormat = value;
                    SetMediaTypes();
                    OnPropertyChanged("SelectedOutputFormat");
                }
            }
        }

        private void SetMediaTypes()
        {
            if (!allMediaTypes.ContainsKey(SelectedOutputFormat.Guid))
            {
                TryGetSupportedMediaTypes();
            }
            FilterSupportedMediaTypes();
            OnPropertyChanged("SupportedMediaTypes");
            SelectedMediaType = SupportedMediaTypes.FirstOrDefault();
        }

        private void FilterSupportedMediaTypes()
        {
            //SupportedMediaTypes.Clear();
            SupportedMediaTypes = new List<MediaTypeViewModel>();
            if (inputWaveFormat == null)
            {
                SupportedMediaTypes.Add(new MediaTypeViewModel() {Name="Select an input file"});
                return;
            }
            SupportedMediaTypes = allMediaTypes[SelectedOutputFormat.Guid]
                .Where(m => m.MediaType != null)
                .Where(m => m.MediaType.Get(MediaTypeAttributeKeys.AudioSamplesPerSecond) == inputWaveFormat.SampleRate)
                .Where(m => m.MediaType.Get(MediaTypeAttributeKeys.AudioNumChannels) == inputWaveFormat.Channels)
                .ToList();
        }

        private void TryGetSupportedMediaTypes()
        {
            var list = SharpMediaFoundationEncoder.GetOutputMediaTypes(SelectedOutputFormat.Guid)
                                  .Select(mf => new MediaTypeViewModel(mf))
                                  .ToList();
            if (list.Count == 0)
            {
                list.Add(new MediaTypeViewModel() {Name = "Not Supported", Description = "No encoder found for this output type"});
            }
            allMediaTypes[SelectedOutputFormat.Guid] = list;
        }

        public MediaTypeViewModel SelectedMediaType 
        {
            get { return selectedMediaType; }
            set
            {
                if (selectedMediaType != value)
                {
                    selectedMediaType = value;
                    OnPropertyChanged("SelectedMediaType");
                }
            }
        }

        private void Encode()
        {
            if (String.IsNullOrEmpty(InputFile)||!File.Exists(InputFile))
            {
                MessageBox.Show("Please select a valid input file to convert");
                return;
            }
            if (SelectedMediaType == null || SelectedMediaType.MediaType == null)
            {
                MessageBox.Show("Please select a valid output format");
                return;
            }

            using (var reader = new SharpMediaFoundationReader(InputFile))
            {
                string outputUrl = SelectSaveFile(SelectedOutputFormat.Name, SelectedOutputFormat.Extension);
                if (outputUrl == null) return;
                using (var encoder = new SharpMediaFoundationEncoder(SelectedMediaType.MediaType))
                {
                    encoder.Encode(outputUrl, reader);
                }
            }
        }

        private string SelectSaveFile(string formatName, string extension)
        {
            var sfd = new SaveFileDialog();
            sfd.FileName = Path.GetFileNameWithoutExtension(InputFile) + " converted" + extension;
            sfd.Filter = formatName + "|*" + extension;
            //return (sfd.ShowDialog() == true) ? new Uri(sfd.FileName).AbsoluteUri : null;
            return (sfd.ShowDialog() == true) ? sfd.FileName : null;
        }

        public void Dispose()
        {
            MediaFactory.Shutdown();
        }
    }

    enum AacPayloadType
    {
        /// <summary>
        /// The stream contains raw_data_block elements only.
        /// </summary>
        RawData = 0,
        /// <summary>
        /// Audio Data Transport Stream (ADTS). The stream contains an adts_sequence, as defined by MPEG-2.
        /// </summary>
        Adts = 1,
        /// <summary>
        /// Audio Data Interchange Format (ADIF). The stream contains an adif_sequence, as defined by MPEG-2.
        /// </summary>
        Adif = 2,
        /// <summary>
        /// The stream contains an MPEG-4 audio transport stream with a synchronization layer (LOAS) and a multiplex layer (LATM).
        /// </summary>
        LoasLatm = 3
    }

    internal class EncoderViewModel : ViewModelBase
    {
        public string Name { get; set; }
        public Guid Guid { get; set; }
        public string Extension { get; set; }
    }
}