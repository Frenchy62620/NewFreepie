using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using FreePIE.Core.Plugins.Extensions;
using FreePIE.Core.Contracts;
using FreePIE.Core.Plugins.DCSBIOS;
using FreePIE.Core.Plugins.ScriptAuto;

namespace FreePIE.Core.Plugins
{
    using Gx = GlobalExtensionMethods;
    [GlobalType(Type = typeof(DCSGlobal))]
    public class DCSPlugin : Plugin
    {
        private ScriptDCS SF;
        public SerialPort serialPort;
        private DCSBiosStateEnum _state;
        private uint _address;
        private uint _count;
        private int _data;
        private int _syncByteCount;
        //private const int LENREC = 87;
        private bool Stopped;
        private IPAddress ipadressToSend;
        private IPEndPoint ipEndPointSender;
        private int UdpPortRecept, UdpPortSend;
        private string AirPlane;
        private bool rebuildfiles;
        private UdpClient UdpSockRecept, UdpSockSend;
        private byte[] buffer;
        private BitArray isok;

        private readonly Object _lockObject = new object();
        public override string FriendlyName => "DCS";

        public override Action Start()
        {
  //          Script.ScriptDCS SF = new Script.ScriptDCS(this);
            //System.Reflection.MethodInfo[] Methods = GetType().GetMethods().Where(m => m.Name.Length == 1).ToArray();
            //trim = new Dictionary<string, Delegate>();
            //foreach(var m in Methods)
            //    trim[m.Name] = (Fct)Delegate.CreateDelegate(typeof(Fct), m);
 //           System.Reflection.MethodInfo mi = typeof(Script.ScriptDCS).GetMethod("A");
            //mi.Invoke(SF, null);
 //           var Function = (Fct)Delegate.CreateDelegate(typeof(Fct), SF, mi);
 //           Function();
            //Methods[6].Invoke(plugin, null);
            ipEndPointSender = new IPEndPoint(ipadressToSend, UdpPortSend);
            UdpSockSend = new UdpClient();
            UdpSockSend.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            UdpSockSend.EnableBroadcast = true;

            isok = new BitArray(512);
            isok.Not();
            buffer = new byte[1024];
            //_state = DCSBiosStateEnum.WAIT_FOR_SYNC;
            //_syncByteCount = 0;
            //Loadjson();
            return RunSensorPoll;
        }

        public override void DoBeforeNextExecute()
        {
            Gx.CheckScriptTimer();
            if (Gx.cmd == 'D')
            {
                if (SF == null)
                {
                    SF = new ScriptDCS(this);
                }
                Gx.InvokeMethodinfo(ref SF);
            }
        }

        public override bool GetProperty(int index, IPluginProperty property)
        {
            if (index > 5)
                return false;

            if (index == 0)
            {
                property.Name = "SendIPADRESS";
                property.Caption = "IP_ADDRESS";
                property.DefaultValue = "127.0.0.1";
                property.HelpText = "Give IP ADDRESS to send DCS Commands";
            }
            else if (index == 1)
            {
                property.Name = "UDPPortSendToDCS";
                property.Caption = "UDP Port (Send)";
                property.DefaultValue = 7778;
                property.HelpText = "UDP Port to send to DCS (default 7778)";
            }
            else if (index == 2)
            {
                property.Name = "UDPPortReceptFromDCS";
                property.Caption = "UDP Port (Recept)";
                property.DefaultValue = 7777;
                property.HelpText = "UDP Port to receive from DCS (default 7777)";
            }
            else if (index == 3)
            {
                property.Name = "JsonDir";
                property.Caption = "Json DIR";
                property.DefaultValue = @"C:\Users\ThierryK\Saved Games\DCS\Scripts\DCS-BIOS\doc\json";
                property.HelpText = "Folder of .json files : " + property.Value;
            }
            else if (index == 4)
            {
                property.Name = "RebuildFiles";
                property.Caption = "Rebuild Files";
                property.DefaultValue = true;
                property.HelpText = "if true, rebuild files in dcs folder, each time\nif false, rebuild in case of missing file";
            }
           else // (index == 5)
            {
                property.Name = "AirFrame";
                property.Caption = "Plane Selected";
                property.DefaultValue = "FC3";
                property.HelpText = "Select plane DCS: A-10C, M-2000C, KA-50, FC3 for just Common Data";
            }
            return true;
        }

        public override bool SetProperties(Dictionary<string, object> properties)
        {
            ipadressToSend = IPAddress.Parse(properties["SendIPADRESS"].ToString());
            UdpPortSend = (int)properties["UDPPortSendToDCS"];
            UdpPortRecept = (int)properties["UDPPortReceptFromDCS"];
            AirPlane = properties["AirFrame"].ToString().ToUpper().Trim();
            rebuildfiles = (bool)properties["RebuildFiles"];
            var jsondir = properties["JsonDir"].ToString().Trim();
            if(!Directory.Exists(jsondir))
                throw new ArgumentException($"json folder: {jsondir} doesn't exist, please fix!!");
            Directory.CreateDirectory(FriendlyName.FreePiePath());
            var filefolder = (FriendlyName + @"\folder_json.txt").FreePiePath();
            using (StreamWriter writer = new StreamWriter(filefolder))
            {
                writer.WriteLine(jsondir);
            }
            DCSBIOSJson json = new DCSBIOSJson(AirPlane, FriendlyName, rebuildfiles);
            return true;
        }

        public override void Stop()
        {
            Stopped = true;
            SF = null;
            // This will cause the blocking read to kick out an exception and complete
            UdpSockRecept?.Close();
            UdpSockSend?.Close();

            if (serialPort != null)
            {
                serialPort.Close();
                serialPort.Dispose();
            }
        }

        public void Write(string text)
        {
            serialPort.Write(text);
        }
        public override object CreateGlobal()
        {
            return new DCSGlobal(this);
        }
        private void RunSensorPoll()
        {
            try
            {
                var ipEndPointReceiver = new IPEndPoint(IPAddress.Any, UdpPortRecept);
                var ipEndPointTablette = new IPEndPoint(IPAddress.Parse("192.168.1.165"), 7776);
                UdpSockRecept = new UdpClient(UdpPortRecept);


                Stopped = false;
                OnStarted(this, new EventArgs());
                var started = DateTime.Now;

                while (!Stopped)
                {
                    byte[] bytes = UdpSockRecept.Receive(ref ipEndPointReceiver);
                    int len = bytes.Length;
                    if (len == 0)
                        continue;
                    _state = DCSBiosStateEnum.WAIT_FOR_SYNC;
                    _syncByteCount = 0;
                    //if (len==1)
                    //{
                    //    var unicodeBytes = Encoding.Unicode.GetBytes(stringData + "\n");
                    //    var asciiBytes = new List<byte>(stringData.Length);
                    //    asciiBytes.AddRange(Encoding.Convert(Encoding.Unicode, Encoding.ASCII, unicodeBytes));
                    //    UdpSockSend.Send(asciiBytes.ToArray(), asciiBytes.ToArray().Length, ipEndPointSender);
                    //}
                    //if (len == 3)
                    //{
                    //string d = GetData(4544, 240);
                    //var unicodeBytes = Encoding.Unicode.GetBytes(d);
                    //var asciiBytes = new List<byte>(d.Length);
                    //asciiBytes.AddRange(Encoding.Convert(Encoding.Unicode, Encoding.Default, unicodeBytes));
                    // IPEndPoint EndPointSender = new IPEndPoint(IPAddress.Parse("192.168.1.165"), 7776);
                    //int result = UdpSockSend.Send(asciiBytes.ToArray(), asciiBytes.ToArray().Length, EndPointSender);
                    UdpSockSend.Send(bytes, len, ipEndPointTablette);
                        //continue;
                    //}

                    //UdpSockSend.Send(asciiBytes.ToArray(), asciiBytes.ToArray().Length, ipEndPointSender);

                    //using (BinaryWriter bx = new BinaryWriter(File.Open(@"E:\file.bin", FileMode.Append)))
                    //{
                    //    bx.Write(0x11111111);
                    //    bx.Write(len);
                    //    bx.Write(bytes);
                    //}
                    // Console.WriteLine("debut " + len);
                    for (int i = 0; i< len; i++)
                        ProcessByte(bytes[i]);

                    //Console.WriteLine("fin");

                }
            }
            catch (SocketException err)
            {
                // A graceful shutdown calls close socket and throws an exception while blocked in Receive()
                // Ignore this exception unless it was not generated during shutdown sequence
                if (!Stopped)
                    throw err;
            }
        }

        private enum DCSBiosStateEnum
        {
            WAIT_FOR_SYNC = 0,
            ADDRESS_LOW = 1,
            ADDRESS_HIGH = 2,
            COUNT_LOW = 3,
            COUNT_HIGH = 4,
            DATA_LOW = 5,
            DATA_HIGH = 6,
        }

        //private int Deserialize(string ident)
        //{
        //    int value = -1;

        //    using (FileStream fs = File.OpenRead((Folder + @"\" + AirPlane + ".bin").FreePiePath()))
        //    using (BinaryReader reader = new BinaryReader(fs))
        //    {
        //        int count = reader.ReadInt32();
        //        for (int n = 0; n < count; n++)
        //        {
        //            var key = reader.ReadString();
        //            var val = reader.ReadInt32();
        //            if (key.Equals(ident))
        //            {
        //                value = val;
        //                break;
        //            }
        //        }
        //    }
        //    return value;
        //}
        private void ProcessByte(byte b)
        {
            //using (BinaryWriter bx = new BinaryWriter(File.Open(@"E:\file.bin", FileMode.Append)))
            //{
            //    bx.Write(b);
            //}
            
            switch (_state)
            {
                case DCSBiosStateEnum.WAIT_FOR_SYNC:
                    /* do nothing */
                    break;
                case DCSBiosStateEnum.ADDRESS_LOW:
                    _address = b;
                    _state = DCSBiosStateEnum.ADDRESS_HIGH;
                    break;
                case DCSBiosStateEnum.ADDRESS_HIGH:
                    _address = (uint)(b << 8) | _address;
                    _state = _address != 0x5555 ? DCSBiosStateEnum.COUNT_LOW : DCSBiosStateEnum.WAIT_FOR_SYNC;
                    break;
                case DCSBiosStateEnum.COUNT_LOW:
                    _count = b;
                    _state = DCSBiosStateEnum.COUNT_HIGH;
                    break;
                case DCSBiosStateEnum.COUNT_HIGH:
                    _count = (uint)(b << 8) | _count;
                    _state = DCSBiosStateEnum.DATA_LOW;
                    break;
                case DCSBiosStateEnum.DATA_LOW:
                    _data = b;
                    _count--;
                    _state = DCSBiosStateEnum.DATA_HIGH;
                    break;
                case DCSBiosStateEnum.DATA_HIGH:
                     _data = (b << 8) | _data;
                    _count--;

                    if (_address > 0x20)            // Dont take Start Data
                    {
                        int adr = (int)GetTranslatedAddressToBuffer(_address) >> 1;
                        unsafe
                        {
                            fixed (byte* pBuffer = buffer)
                            {
                                short* pSample = (short*)pBuffer;
                                var bufdata = (int) pSample[adr];
                                if (_data != bufdata)
                                //if (isok[adr] && _data != bufdata)
                                {
                                    lock (_lockObject)
                                    {
                                        pSample[adr] = (short)_data;
                                    }
                                }
                            }
                        }
                    }

                    if (_count == 0)
                        _state = DCSBiosStateEnum.ADDRESS_LOW;
                    else
                    {
                        _address += 2;
                        _state = DCSBiosStateEnum.DATA_LOW;
                    }
                    break;
            }
            if (b == 0x55)
            {
                //Console.WriteLine(Environment.TickCount - ticks);
                //ticks = Environment.TickCount;
                _syncByteCount++;
            }
            else
            {
                _syncByteCount = 0;
            }
            if (_syncByteCount == 4)
            {
                _state = DCSBiosStateEnum.ADDRESS_LOW;
                _syncByteCount = 0;
            }
        }

        //public string GetA10C_Alt()
        //{
        //    float FT10000, FT1000, FT100;
        //    var address = GetTranslatedAddressToBuffer(4224);
        //    lock (_lockObject)
        //    {
        //        FT10000 = buffer[address] + (buffer[address + 1] << 8);
        //        FT1000 = buffer[address + 2] + (buffer[address + 3] << 8);
        //        FT100 = buffer[address + 4] + (buffer[address + 5] << 8);
        //    }
        //    FT10000 = (float)Math.Round(FT10000 * 10 / 65535);
        //    FT1000 = (float)Math.Round(FT1000 * 10 / 65535);
        //    FT100 = (float)Math.Round(FT100 * 100 / 65535);
        //    return ((int)FT10000 * 10000 + (int)FT1000 * 1000 + (int)FT100 * 10).ToString();
        //}
        //public string GetA10C_VVI()
        //{
        //    float VVI;
        //    var address = GetTranslatedAddressToBuffer(4206);
        //    lock (_lockObject)
        //    {
        //        VVI = buffer[address] + (buffer[address + 1] << 8);
        //    }
        //    VVI = (float)Math.Round(VVI * 1200 / 65535);
        //    return ((int)VVI).ToString();
        //}
        private uint GetTranslatedAddressToBuffer(uint adr)
        {
            if (adr == 0xFFFE)          // Counter
                return 1022;
            if (adr < 0x410)            // CommonData 1024 to 1035
                return 1000 + adr - 1024;
            if (adr < 0x2600)           // A-10C (0x1000 = 4096, M-2000C (0x1400 = 5120)  
                return adr - (adr / 0x400) * 0x400;
            throw new Exception($"Adress Error. -> {_address} ");
        }

        //public void SetA10CRS(int new_heading_degree)
        //{
        //    "A-10C_radio.txt".DecodelineOfCommand("COURSE_KNOB");
        //}
        //public void SetA10Pressure()
        //{
        //    "A-10C_pressure.txt".DecodelineOfCommand();
        //}
        //public void SetA10Laste()
        //{
        //    "A-10C_test.txt".DecodelineOfCommand("TEST2");
        //}
        public int SendDCSCommand(string stringData)
        {
            var result = 0;

            try
            {
                //byte[] bytes = _iso8859_1.GetBytes(stringData);
                var unicodeBytes = Encoding.Unicode.GetBytes(stringData + "\n");
                var asciiBytes = new List<byte>(stringData.Length);
                asciiBytes.AddRange(Encoding.Convert(Encoding.Unicode, Encoding.ASCII, unicodeBytes));
                result = UdpSockSend.Send(asciiBytes.ToArray(), asciiBytes.ToArray().Length, ipEndPointSender);
                //result = _udpSendClient.Send(bytes, bytes.Length, _ipEndPointSender);

            }
            catch (SocketException err)
            {
                if (!Stopped)
                    throw err;
            }

            return result;
        }

        public void SendCommand(string command, int priority)
        {
            command.DecodelineOfCommand();
        }

        internal void SendFileCommand(string command, string section, int priority)
        {
            command.DecodelineOfCommand(section, priority);
        }
        public void SelectPlane(string plane)
        {
            AirPlane = plane.ToUpper().Trim();
            DCSBIOSJson  j = new DCSBIOSJson(AirPlane, FriendlyName, true);
        }

        public int GetData(uint address, int mask, int shift)
        {
            address = GetTranslatedAddressToBuffer(address);
            lock (_lockObject)
            {
                int data = buffer[address] + (buffer[address + 1] << 8);
                return ((data & mask) >> shift);
            }
        }

        public string GetData(uint address, int len)
        {
            byte[] dst = new byte[len];
            address = GetTranslatedAddressToBuffer(address);
            lock (_lockObject)
            {
                Buffer.BlockCopy(buffer, (int)address, dst, 0, len);
            }
           // string data = Encoding.UTF8.GetString(dst).TrimEnd('\0');
            string data = Encoding.Default.GetString(dst);
            if (data.Length == 0) data = "0";
            return data;
        }

        internal void InitSerial(int baudrate, string port)
        {
            serialPort = new SerialPort(port, baudrate);
            //serialPort.DtrEnable = true;  // reboot Arduino
            serialPort.Open();
            serialPort.Write("port connected\n");
        }

    }

    [Global(Name = "dcs")]
    public class DCSGlobal
    {
        private readonly DCSPlugin Device;
        public DCSGlobal(DCSPlugin plugin)
        {
            this.Device = plugin;
        }
        
        public void selectPlane(string plane)
        {
            Device.SelectPlane(plane);
        }
        public void sendCommand(IList<string> commands, string section = "", int priority = 0)
        {
            foreach(var command in commands)
                sendCommand(command, section, priority);
        }
        public void sendCommand(string command, string section ="", int priority = 0, bool execute = true)
        {
            if (!execute) return;
            if (command.Contains(' '))
                Device.SendDCSCommand(command);
            else if (command.Contains(';') || command.Contains("K0"))
                Device.SendCommand(command, priority);
            else
                Device.SendFileCommand(command, section, priority);
        }

        public void toggleCommand(uint address, int mask, int shift, string command)
        {
            Device.SendDCSCommand(command + " " + (1 - getData(address, mask, shift)));
        }
        //public void selectAddressFile(string file)
        //{
        //    file = (@"dcs\" + file).FreePiePath();
        //    if (!File.Exists(file)) return;
        //    Device.ReadAddressesFromFile(file);
        //}
        public void setDataInBuffer(string data)
        {
            Gx.keystyped = data;
        }
        public string getDataFromBuffer()
        {
            return Gx.keystyped;
        }
        public string getData(uint address, int length)//string identifier, int output = 0)
        {
            string result = Device.GetData(address, length);
            return result;
        }

        public int getData(uint address, int mask, int shift)//string identifier, int output = 0)
        {
            int result = Device.GetData(address, mask, shift);
            return result;
        }

        public List<int> getDataIFromDCS()
        {
            return Gx.getDataTyped().Split(';').Select(q => Convert.ToInt32(q)).ToList();
        }

        public bool IsActionFinished() => Gx.cmd == '\0';
        public List<string> getDataSFromDCS()
        {
            return Gx.getDataTyped().Split(';').Select(q => q).ToList();
        }

        //public void setLasteWind()
        //{
        //    Gx.keystyped = "38;-7;4;55;6;130;9;224"; 
        //    Device.SetA10Laste();
        //}
        //public void setPressure()
        //{
        //    Device.SetA10Pressure();
        //}
        //public void setCRS(int heading = 0)
        //{
        //    Device.SetA10CRS(heading);
        //}
        //public string getAlt()
        //{
        //    return Device.GetA10C_Alt();
        //}
        //public string getVVI()
        //{
        //    return Device.GetA10C_VVI();
        //}
        public void openPort(int baudrate = 115200, string port = "COM3")
        {
            Device.InitSerial(baudrate, port);
        }
        public void sendToPort(string text)
        {
            Device.Write(text + '\n');
        }

    }
}

