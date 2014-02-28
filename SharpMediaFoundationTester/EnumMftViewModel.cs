using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using NAudio.MediaFoundation;
using NAudio.Utils;
using SharpDX.MediaFoundation;

namespace SharpMediaFoundationTester
{
    class EnumMftViewModel : ViewModelBase, IDisposable
    {
        public EnumMftViewModel()
        {
            MediaManager.Startup();
            EnumerateCommand = new DelegateCommand(Enumerate);
        }

        public ICommand EnumerateCommand { get; private set; }

        public List<string> Transforms { get; private set; }

        private void Enumerate()
        {
            Transforms = new List<string>();

            var effects = EnumerateTransforms(TransformCategoryGuids.AudioEffect);
            AddTransforms(effects, "Audio Effect");
            AddTransforms(EnumerateTransforms(TransformCategoryGuids.AudioDecoder), "Audio Decoder");
            AddTransforms(EnumerateTransforms(TransformCategoryGuids.AudioEncoder), "Audio Encoder");
            OnPropertyChanged("Transforms");
        }


        /// <summary>
        /// Enumerate the installed MediaFoundation transforms in the specified category
        /// </summary>
        /// <param name="category">A category from MediaFoundationTransformCategories</param>
        /// <returns></returns>
        public static IEnumerable<Activate> EnumerateTransforms(Guid category)
        {
            return MediaFactory.FindTransform(category, TransformEnumFlag.All, null, null);
        }

        private void AddTransforms(IEnumerable<Activate> effects, string type)
        {
            foreach (var mft in effects)
            {
                int attributeCount = mft.Count;
                var sb = new StringBuilder();
                sb.AppendFormat(type);
                sb.AppendLine();
                for (int n = 0; n < attributeCount; n++)
                {
                    Guid key;
                    var value = mft.GetByIndex(n, out key);
                    string propertyName = FieldDescriptionHelper.Describe(typeof(MediaFoundationAttributes), key);
                    if (key == TransformAttributeKeys.MftInputTypesAttributes.Guid || key == TransformAttributeKeys.MftOutputTypesAttributes.Guid)
                    {
                        byte[] blob = (byte[]) value;
                        var count = blob.Length/32;
                        var types = new TRegisterTypeInformation[count];
                        
                        for (int j = 0; j < count; j++)
                        {
                            types[j].GuidMajorType = new Guid(blob.Skip(j*32).Take(16).ToArray());
                            types[j].GuidSubtype = new Guid(blob.Skip(j * 32 +16).Take(16).ToArray());
                        }

                        sb.AppendFormat("{0}: {1} items:", propertyName, types.Length);
                        sb.AppendLine();
                        foreach (var t in types)
                        {
                            sb.AppendFormat("    {0}-{1}", 
                                FieldDescriptionHelper.Describe(typeof(MediaTypes), t.GuidMajorType), 
                                FieldDescriptionHelper.Describe(typeof(AudioSubtypes), t.GuidSubtype));
                            sb.AppendLine();
                        }
                    }
                    else if (key == TransformAttributeKeys.TransformCategoryAttribute.Guid)
                    {
                        sb.AppendFormat("{0}: {1}", propertyName, FieldDescriptionHelper.Describe(typeof(MediaFoundationTransformCategories), (Guid)value));
                        sb.AppendLine();
                    }
                    else if (value is byte[])
                    {
                        var b = (byte[])value;
                        sb.AppendFormat("{0}: Blob of {1} bytes", propertyName, b.Length);
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendFormat("{0}: {1}", propertyName, value);
                        sb.AppendLine();
                    }
                }
                Transforms.Add(sb.ToString());
            }
        }

        public void Dispose()
        {
            MediaManager.Shutdown();
        }
    }
}