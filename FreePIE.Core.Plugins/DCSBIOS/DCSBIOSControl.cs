using System;
using System.Collections.Generic;

namespace FreePIE.Core.Plugins.DCSBIOS
{
    public enum DCSBiosOutputType
    {
        STRING_TYPE,
        INTEGER_TYPE
    }
 
    public class DCSBIOSControlInput
    {
        public string description { get; set; }
        public string @interface { get; set; }
        public int? max_value { get; set; }
        public string argument { get; set; }
    }

    public class DCSBIOSControlOutput
    {
        private string _type;
        public uint address { get; set; }
        public string description { get; set; }
        public uint mask { get; set; }
        public int max_value { get; set; }
        public int shift_by { get; set; }
        public string suffix { get; set; }

        public string type
        {
            get { return _type; }
            set
            {
                _type = value;
                if (_type.Equals("string"))
                {
                    OutputDataType = DCSBiosOutputType.STRING_TYPE;
                }
                if (_type.Equals("integer"))
                {
                    OutputDataType = DCSBiosOutputType.INTEGER_TYPE;
                }
            }
        }

        public DCSBiosOutputType OutputDataType { get; set; }
    }

    public class DCSBIOSControl
    {
        public string category { get; set; }
        public string control_type { get; set; }
        public string description { get; set; }
        public string identifier { get; set; }
        public List<DCSBIOSControlInput> inputs { get; set; }
        public string momentary_positions { get; set; }
        public List<DCSBIOSControlOutput> outputs { get; set; }
        public string physical_variant { get; set; }
    }

    public class DCSBIOSControlRootObject
    {
        public List<DCSBIOSControl> DCSBIOSControls { get; set; }
    }
}
