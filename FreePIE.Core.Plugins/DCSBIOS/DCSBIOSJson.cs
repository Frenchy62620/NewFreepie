using System;
using System.Collections.Generic;
using FreePIE.Core.Plugins.Extensions;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace FreePIE.Core.Plugins.DCSBIOS
{
     public class DCSBIOSJson
    {
         string folder;
         string AirPlane;
        public DCSBIOSJson(String AirPlane, String Folder, bool flag)
        {
            this.folder = Folder;
            this.AirPlane = AirPlane;
            var ff = folder.FreePiePath();
            if (flag || !File.Exists(ff + @"\" + AirPlane + ".txt")) TransformJson();
        }

        private void TransformJson()
        {
            List<DCSBIOSControl> __dcsGeneral = new List<DCSBIOSControl>(600);
            Dictionary<object, string> _dcsIdentifier = new Dictionary<object, string>();
            Dictionary<string, string> _dcsCategory = new Dictionary<string, string>();
            Dictionary<uint, string> _dcsAddress = new Dictionary<uint, string>();
            var filefolder = (folder + @"\folder_json.txt").FreePiePath();
            String _jsonDirectory;
            using (StreamReader reader = new StreamReader(filefolder))
            {
                _jsonDirectory = reader.ReadLine();
            }

            if (!AirPlane.Equals("FC3"))
            {
                String file = _jsonDirectory + @"\" + AirPlane + ".json";

                if (!File.Exists(file))
                    throw new Exception("Failed to find DCS-BIOS files. -> " + file);

                string jsonDataText;
                using (StreamReader reader = new StreamReader(file))
                {
                    jsonDataText = reader.ReadToEnd();
                }
                var jsonData = Format(jsonDataText);

                var dcsBiosControlList = JsonConvert.DeserializeObject<DCSBIOSControlRootObject>(jsonData);
                __dcsGeneral.AddRange(dcsBiosControlList.DCSBIOSControls);
            }

            var commonDataText = File.ReadAllText(_jsonDirectory + "\\CommonData.json");
            var commonDataControlsText = Format(commonDataText);
            var commonDataControls = JsonConvert.DeserializeObject<DCSBIOSControlRootObject>(commonDataControlsText);
            __dcsGeneral.AddRange(commonDataControls.DCSBIOSControls);

            var metaDataEndText = File.ReadAllText(_jsonDirectory + "\\MetadataEnd.json");
            var metaDataEndControlsText = Format(metaDataEndText);
            var metaDataEndControls = JsonConvert.DeserializeObject<DCSBIOSControlRootObject>(metaDataEndControlsText);
            __dcsGeneral.AddRange(metaDataEndControls.DCSBIOSControls);

            CreateCSVFile(__dcsGeneral);

            StringBuilder errortxt = new StringBuilder();

            foreach (var x in __dcsGeneral.OrderBy(s => s.identifier))
            {
                //by identifier just check if no duplicate identifier
                if(!_dcsIdentifier.ContainsKey(x.identifier))
                    _dcsIdentifier[x.identifier] = x.category;
                else
                    errortxt.AppendFormat("dup ident : {0}  category( {1} / {2} )\n", x.identifier, x.category, _dcsIdentifier[x.identifier]);

                //by address
                foreach (var y in x.outputs)
                {
                    if (!_dcsAddress.ContainsKey(y.address))
                        _dcsAddress[y.address] = x.identifier;
                    else
                    {
                        string st;
                        _dcsAddress.TryGetValue(y.address, out st);
                        st = st + "|" + x.identifier;
                        _dcsAddress[y.address] = st;
                    }
                }

                //by category
                if (!_dcsCategory.ContainsKey(x.category))
                    _dcsCategory[x.category] = x.identifier;
                else
                {
                    string st;
                    _dcsCategory.TryGetValue(x.category, out st);
                    st = st + "|" + x.identifier;
                    _dcsCategory[x.category]  = st;
                }
            }
            _dcsIdentifier.Clear();
            if (errortxt.Length != 0)
                throw new ArgumentException(errortxt.ToString());

            using (StreamWriter b = new StreamWriter((folder + @"\" + AirPlane + ".txt").FreePiePath()))
            //using (FileStream fsi = File.OpenWrite((folder + @"\" + AirPlane + "_I.bin").FreePiePath()))
            //using (FileStream fsc = File.OpenWrite((folder + @"\" + AirPlane + "_C.bin").FreePiePath()))
            //using (FileStream fsa = File.OpenWrite((folder + @"\" + AirPlane + "_A.bin").FreePiePath()))
            //using (BinaryWriter writeri = new BinaryWriter(fsi))
            //using (BinaryWriter writerc = new BinaryWriter(fsc))
            //using (BinaryWriter writera = new BinaryWriter(fsa))
            {
                //int i = 0;
                StringBuilder t = new StringBuilder();
                //writeri.Write(__dcsGeneral.Count);
                foreach (var x in __dcsGeneral.OrderBy(s => s.identifier))
                {
                    t.Clear();
                    //uint first, second;
                    //writeri.Write(x.identifier.ToUpper());// writeri.Write(i++);//(identifier, num)
                    //writeri.Write(x.outputs.Count);
                    //foreach (var o in x.outputs)
                    //{
                    //    first = (uint)((uint)o.OutputDataType + (o.address << 16));
                    //    second = (uint)(o.max_value + (o.mask << 16));
                    //    writeri.Write(first); writeri.Write(second);
                    //}

                    
                    t.Append(x.identifier.ToUpper().PadRight(34));;
                    foreach (var y in x.outputs)
                    {
                        t.AppendFormat(";{0,-5};{1};{2,-5}", y.address, (int)y.OutputDataType,  (uint)y.max_value);
                        t.AppendFormat(";{0,-5};{1,-5}", y.mask, y.shift_by);
                    }

                    for (int i = 2; i != x.outputs.Count; i--)
                        t.AppendFormat(";99999; ;{0,-5};{0,-5};{0,-5}", ' ');

                    t.Append("\n");
                    b.Write(t.ToString());
                }
                b.Write("\n");
                //writeri.Flush();

                //writerc.Write(_dcsCategory.Count);
                foreach (var k in _dcsCategory.OrderBy(s =>s.Key))
                {
                    //writerc.Write(k.Key.ToUpper()); writerc.Write(k.Value.ToUpper());//(category, all identifiers)

                    t.Clear();
                    t.AppendFormat("{0,-40};{1}\n", k.Key, k.Value);
                    b.Write(t.ToString().ToUpper());
                }
                b.Write("\n");
                //writerc.Flush();

                //writera.Write(_dcsAddress.Count);
                foreach (var k in _dcsAddress.OrderBy(s =>s.Key))
                {
                    //writera.Write(k.Key); writera.Write(k.Value.ToUpper());//(address, all identifiers)

                    t.Clear();
                    t.AppendFormat("{0,-5} 0x{0,-5:X} ; {1}\n", k.Key, k.Value.ToUpper());
                    b.Write(t.ToString());
                }
                b.Write("\n");
                //writera.Flush();

                b.Flush();
            }
            __dcsGeneral.Clear();
            _dcsAddress.Clear();
            _dcsCategory.Clear();
        }

         private void CreateCSVFile(List<DCSBIOSControl> d)
        {

            using (StreamWriter b = new StreamWriter((folder + @"\" + AirPlane + ".csv").FreePiePath()))
            {
                StringBuilder t = new StringBuilder();
                t.Append("identifier|category|description|control type|physical variant|momentary pos|");
                t.Append("o[0] address [descript] [Data Type : Max Val] [Mask : Shift] [type : suffix]|");
                t.Append("o[1] address [descript] [Data Type : Max Val] [Mask : Shift] [type : suffix]|");
                t.Append("i[0] argument [descript] [interface : Max Val]|");
                t.Append("i[1] argument [descript] [interface : Max Val]|");
                t.Append("i[2] argument [descript] [interface : Max Val]");
                t.Append("\n");
                b.Write(t.ToString());
                foreach (var x in d.OrderBy(i => i.identifier))
                {
                    t.Clear();
                    t.AppendFormat("{0}|{1}|{2}|", x.identifier, x.category, x.description);
                    t.AppendFormat("{0}|{1}|{2}", x.control_type, x.physical_variant, x.momentary_positions);

                    foreach (var o in x.outputs)
                    {
                        t.AppendFormat("|{0} [{1}] [{2} : {3} ]", o.address, o.description, o.OutputDataType.ToString(), o.max_value);
                        t.AppendFormat("[0x{0:X} : {1}][{2} : {3}]", o.mask, o.shift_by, o.type, o.suffix);
                    }
                    for (int i = 2; i != x.outputs.Count; i--)
                        t.Append("|");

                    foreach (var i in x.inputs)
                    {
                        t.AppendFormat("|{0} [{1}] [{2} : {3}]", i.argument, i.description, i.@interface, i.max_value);
                    }
                    for (int i = 3; i != x.inputs.Count; i--)
                        t.Append("|");


                    t.Append("\n");

                    b.Write(t.ToString().ToUpper());
                }
                b.Flush();
            }
        }
        private String Format(String fileText)
        {
            //make it easier to know what line endings are used.
            var result = Regex.Replace(fileText, "\r", "");
            //Replacing all dangling Control Type Strings  e.g. "ADI_CRSWARN_FLAG": {  , next line contains "category" or "api_variant"  [POSITIVE LOOKAHEAD]
            var strRegex = @"^[\s]+""[A-Za-z0-9_\s()-:]+"":\s\{(?=\n^[\s]+""category"")";
            result = Regex.Replace(result, strRegex, "                                                               {", RegexOptions.Multiline | RegexOptions.CultureInvariant);
            strRegex = @"^[\s]+""[A-Za-z0-9_\s()-:]+"":\s\{(?=\n^[\s]+""api_variant"")";
            result = Regex.Replace(result, strRegex, "                                                               {", RegexOptions.Multiline | RegexOptions.CultureInvariant);

            //Replacing first category entry with array start of DCSBIOSControls[
            strRegex = @"^[\s]+""[A-Za-z0-9_\s()-:]+"":\s\{(?<=^\{\n[\s]*""[A-Za-z0-9_\s]+"":\s\{)";
            result = Regex.Replace(result, strRegex, "   	\"DCSBIOSControls\": [", RegexOptions.Multiline | RegexOptions.CultureInvariant);

            //Replacing all trailing category entries
            strRegex = @"},\n^[\s]+""[A-Za-z0-9_&\s()-:]+"":\s\{(?=\n^[\s]+\{)";
            result = Regex.Replace(result, strRegex, "                                 ,", RegexOptions.Multiline | RegexOptions.CultureInvariant);

            //Replacing max_length by max_value
            strRegex = @"max_length";
            result = Regex.Replace(result, strRegex, "max_value", RegexOptions.Multiline | RegexOptions.CultureInvariant);

            //Add array end to the end of the file
            strRegex = @"}(?=\n^\})";
            result = Regex.Replace(result, strRegex, "         ]", RegexOptions.Multiline | RegexOptions.CultureInvariant);

            //Replace , inside "..." by ; (csv file future)
            //strRegex = @"(?'open'([a-zA-Z']),)";
            //result = Regex.Replace(result, strRegex, "$1;", RegexOptions.Multiline | RegexOptions.CultureInvariant);


            return result;
        }
    }
}
