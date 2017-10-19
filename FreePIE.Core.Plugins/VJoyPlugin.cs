using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.ComponentModel;
using FreePIE.Core.Contracts;
using FreePIE.Core.Plugins.Globals;
using FreePIE.Core.Plugins.Extensions;
using FreePIE.Core.Plugins.ScriptAuto;
using FreePIE.Core.Plugins.Strategies;
using vJoyInterfaceWrap;

namespace FreePIE.Core.Plugins
{
    using Gx = GlobalExtensionMethods;

    [GlobalEnum]
    public enum Pov
    {
        Up = 0,
        U = 0,
        UpRight = 4,
        UR = 4,
        Right = 1,
        R = 1,
        DownRight = 5,
        DR = 5,
        Down = 2,
        D = 2,
        DownLeft = 6,
        DL = 6,
        Left = 3,
        L = 3,
        UpLeft = 7,
        UL = 7,
        Nil = -1
    }

    [GlobalType(Type= typeof(VJoyGlobal), IsIndexed = true)]
    public class VJoyPlugin : Plugin
    {
        public List<VJoyGlobalHolder> holders;
        private ScriptVJoy SF;
        public override object CreateGlobal()
        {
            holders = new List<VJoyGlobalHolder>();

            return new GlobalIndexer<VJoyGlobal, uint>(Create);
        }

        public override void Stop()
        {
            SF = null;
            holders.ForEach(h => h.Dispose());
            VJoyGlobalHolder.modeDiffered = false;
        }

        private VJoyGlobal Create(uint index)
        {
            var holder = new VJoyGlobalHolder(index + 1);
            holders.Add(holder);
            
            return holder.Global;
        }

        public override void DoBeforeNextExecute()
        {
            Gx.CheckScriptTimer();

            if (Gx.cmd == 'V')
            {
                if (SF == null)
                {
                    SF = new ScriptVJoy(this);
                }
                Gx.InvokeMethodinfo(ref SF);
            }


            //for (int i = 0; i < holders.Count; i++)
            //{
            //    if (Gx.cmd == 'V' && Gx.subcmd2 == i)
            //    {
            //        switch (Gx.subcmd1)
            //        {
            //            case 'Z':
            //                foreach (var vbut in Gx.wd[1].Split(':'))
            //                    holders[i].PressAndRelease(Convert.ToInt32(vbut));
            //                break;
            //            case 'P':
            //                foreach (var vbut in Gx.wd[1].Split(':'))
            //                    holders[i].SetButton(Convert.ToInt32(vbut), true);
            //                break;
            //            case 'R':
            //                foreach (var vbut in Gx.wd[1].Split(':'))
            //                    holders[i].SetButton(Convert.ToInt32(vbut), false);
            //                break;
            //            default:
            //                throw new Exception(string.Format("Subcommand unknown -> {0};{1}", Gx.wd[0], Gx.wd[1]));
            //        }
            //        Gx.NextAction();
            //    }
            //    holders[i].SendPressed();
            //}
            holders.ForEach(h => h.SendPressed());
        }

        public override string FriendlyName => "vJoy (SourceForge)";
    }
    //struct Inbits 
    //{
    //    public Inbits(int initialBitValue)
    //    {
    //        bits = initialBitValue;
    //    }
    //    public bool this[int index]
    //    {
    //        get { return (bits & (1 << index)) != 0; }
    //        set
    //        {
    //            if (value)
    //                bits |= (1 << index);
    //            else
    //                bits &= ~(1 << index);
    //        }
    //    }
    //    public int value
    //    {
    //        get { return bits;}
    //    }
    //    private int bits;
    //}
    public sealed class VJoyGlobalHolder : IDisposable
    {
        private readonly vJoy _joystick;
        private vJoy.JoystickState report;//tkz
        private readonly Dictionary<HID_USAGES, bool> enabledAxis;
        private readonly Dictionary<HID_USAGES, int> currentAxisValue;
       // private readonly Dictionary<int, bool> currentButtonValue;
        private readonly Stopwatch timer;
        private readonly int maxButtons;
        private readonly int maxDirPov;
        private readonly int maxContinuousPov;
        public const int ContinuousPovMax = 35900;
        public static bool modeDiffered;
       
        private readonly SetPressedStrategy<int> setPressedStrategy;

        public VJoyGlobalHolder(uint index)
        {
            Index = index;
            Global = new VJoyGlobal(this);
            setPressedStrategy = new SetPressedStrategy<int>(b => SetButton(b, true), b => SetButton(b, false));

            _joystick = new vJoy();
            report = new vJoy.JoystickState();//tkz
            if (index < 1 || index > 16)
                throw new ArgumentException($"Illegal joystick device id: {index}");

            if (!_joystick.vJoyEnabled())
                throw new Exception("vJoy driver not enabled: Failed Getting vJoy attributes");

            uint apiVersion = 0;
            uint driverVersion = 0;
            bool match = _joystick.DriverMatch(ref apiVersion, ref driverVersion);
            if (!match)
                Console.WriteLine("vJoy version of Driver ({0:X}) does NOT match DLL Version ({1:X})", driverVersion, apiVersion);

            Version = new VjoyVersionGlobal(apiVersion, driverVersion);

            var status = _joystick.GetVJDStatus(index);
            
            
            string error = null;
            switch (status)
            {
                case VjdStat.VJD_STAT_BUSY:
                    error = "vJoy Device {0} is already owned by another feeder";
                    break;
                case VjdStat.VJD_STAT_MISS:
                    error = "vJoy Device {0} is not installed or disabled";
                    break;
                case VjdStat.VJD_STAT_UNKN:
                    error = ("vJoy Device {0} general error");
                    break;
            }

            if (error == null && !_joystick.AcquireVJD(index))
                error = "Failed to acquire vJoy device number {0}";

            if (error != null)
                throw new Exception(string.Format(error, index));

            long max = 0, min = 0;
            _joystick.GetVJDAxisMax(index, HID_USAGES.HID_USAGE_X, ref max);
            _joystick.GetVJDAxisMin(index, HID_USAGES.HID_USAGE_X, ref min);
            AxisMax = (int)max / 2 + 1;//modif TKZ erreur -1 -> +1
            AxisMin = (int)min;
            enabledAxis = new Dictionary<HID_USAGES, bool>();
            foreach (HID_USAGES axis in Enum.GetValues(typeof (HID_USAGES)))
                enabledAxis[axis] = _joystick.GetVJDAxisExist(index, axis);

            maxButtons = _joystick.GetVJDButtonNumber(index);
            maxDirPov = _joystick.GetVJDDiscPovNumber(index);
            maxContinuousPov = _joystick.GetVJDContPovNumber(index);

            timer = new Stopwatch();
            //currentButtonValue = new Dictionary<int, bool>();
            currentAxisValue = new Dictionary<HID_USAGES, int>();

            _joystick.ResetVJD(index);
        }
        public bool IsAxisExist(uint axis)
        {
            return _joystick.GetVJDAxisExist(Index, (HID_USAGES) axis);
        }
        public void SetButton(int button, bool pressed)
        {
            if (button < 0) return;

            if (button > 999)
            {
                int numpov = button / 1000 - 1;
                int direction = pressed ? button % 1000: -1;
                SetDirectionalPov(numpov, direction);
                return;
            }
            if (button >= maxButtons)
                throw new Exception(
                    $"Maximum buttons are {maxButtons}. You need to increase number of buttons in vJoy config");
            if (!modeDiffered)
                _joystick.SetBtn(pressed, Index, (uint)(button) + 1);
            else
            {
                switch (button / 32)
                {
                    case 0:
                        setButtonBuffer(ref report.Buttons, button, pressed);
                        break;
                    case 1:
                        setButtonBuffer(ref report.ButtonsEx1, button, pressed);
                        break;
                    case 2:
                        setButtonBuffer(ref report.ButtonsEx2, button, pressed);
                        break;
                    case 3:
                        setButtonBuffer(ref report.ButtonsEx3, button, pressed);
                        break;
                }
            }
        }

        private void setButtonBuffer(ref uint rpt, int button, bool pressed)
        {
            if (pressed)
                rpt |= (uint)(1 << button);
            else
                rpt &= (uint)~(1 << button);
        }
        public bool _UpdateVJD()
        {
            report.bDevice = (byte)Index;
            return _joystick.UpdateVJD(Index, ref report);
        }
        public bool IsListEmpty() => setPressedStrategy.IsListEmpty();
        public void PressAndRelease(int button) => setPressedStrategy.Add(button);

        public void SendPressed() => setPressedStrategy.Do();

        public void SetAxis(int x, HID_USAGES usage)
        {
            if (!enabledAxis[usage])
                return;
                //throw new Exception(string.Format("Axis {0} not enabled, enable it from VJoy config", usage));
            x += AxisMax;
            if (!modeDiffered)
                _joystick.SetAxis(x, Index, usage);
            else
            {
                switch (usage)
                {
                    case HID_USAGES.HID_USAGE_X:
                        report.AxisX = x;
                        break;
                    case HID_USAGES.HID_USAGE_Y:
                        report.AxisY = x;
                        break;
                    case HID_USAGES.HID_USAGE_Z:
                        report.AxisZ = x;
                        break;
                    case HID_USAGES.HID_USAGE_RX:
                        report.AxisXRot = x;
                        break;
                    case HID_USAGES.HID_USAGE_RY:
                        report.AxisYRot = x;
                        break;
                    case HID_USAGES.HID_USAGE_RZ:
                        report.AxisZRot = x;
                        break;
                    case HID_USAGES.HID_USAGE_SL0:
                        report.Slider = x;
                        break;
                    case HID_USAGES.HID_USAGE_SL1:
                        report.Dial = x;
                        break;
                    default:
                        throw new Exception($"Axis {usage} not tested");
                }
            }
            currentAxisValue[usage] = x - AxisMax;
        }

        public int GetAxis(HID_USAGES usage) => currentAxisValue.ContainsKey(usage) ? currentAxisValue[usage] : 0;
        public void SetDirectionalPov(int pov, int direction)
        {
            if (pov >= maxDirPov)
                throw new Exception(
                    $"Maximum digital POV hats are {maxDirPov}. You need to increase number of digital POV hats in vJoy config");
            if (direction > 3)
                throw new Exception($"Different values of direction are -1, 0 to 3. Your value is {direction}.");

            if (!modeDiffered)
                _joystick.SetDiscPov(direction, Index, (uint)pov + 1);
            else
            {
                uint b = (uint)0xF << 4 * pov;
                direction = (direction & 0xF) << 4 * pov;
                report.bHats = (report.bHats & ~b) | (uint)direction;
            }
        }

        public void SetContinuousPov(int pov, int value)
        {
            if(pov >= maxContinuousPov)
                throw new Exception(
                    $"Maximum analog POV sticks are {maxContinuousPov}. You need to increase number of analog POV hats in vJoy config");
            if (!modeDiffered)
                _joystick.SetContPov(value, Index, (uint)pov+1);
            else
            {
                switch(pov)
                {
                    case 0:
                        report.bHats = (uint)value;
                        break;
                    case 1:
                        report.bHatsEx1 = (uint)value;
                        break;
                    case 2:
                        report.bHatsEx2 = (uint)value;
                        break;
                    case 3:
                        report.bHatsEx3 = (uint)value;
                        break;
                }
            }
        }
        public List<int> MinMaxValue()
        {
            return new List<int>(){ AxisMin, AxisMax} ;
        }
        private int GetXDir(float limit, float x)
        {
            if (Math.Abs(x) < limit) return -1;
            return x < 0 ? 3 : 1;
        }
        private int GetYDir(float limit, float y)
        {
            if (Math.Abs(y) < limit) return -1;
            return y < 0 ? 0 : 2;
        }
        public int GetDirection(float limit, float x, float y) => Math.Max(GetXDir(limit, x), GetYDir(limit, y));
        public int GetDirection(int pov) => pov >= 0 ? pov / 9000 : -1;  // only [U,R,D,L] = [0,1,2,3]
        public VJoyGlobal Global { get; private set; }
        public uint Index { get; private set; }
        public int AxisMax { get; private set; }
        public int AxisMin { get; private set; }
        public VjoyVersionGlobal Version { get; private set; }

        public void Dispose()
        {
            _joystick.RelinquishVJD(Index);   
        }

    }

    public class VjoyVersionGlobal
    {
        public uint driver { get; private set; }
        public uint api { get; private set; }

        public VjoyVersionGlobal(uint driver, uint api)
        {
            this.driver = driver;
            this.api = api;
        }
    }

    [Global(Name = "vJoy")]
    public class VJoyGlobal
    {
        private readonly VJoyGlobalHolder holder;
        public VJoyGlobal(VJoyGlobalHolder holder)
        {
            this.holder = holder;
        }

        //public int axisMax { get { return holder.AxisMax; }}

        public List<int> MinMaxAxis => holder.MinMaxValue();
        public int continuousPovMax => VJoyGlobalHolder.ContinuousPovMax;
        public bool modeDiffered
        {
            set { VJoyGlobalHolder.modeDiffered = value; }
            get { return VJoyGlobalHolder.modeDiffered; }

        }
        public int x
        {
            get { return holder.GetAxis(HID_USAGES.HID_USAGE_X); }
            set { holder.SetAxis(value, HID_USAGES.HID_USAGE_X); }
        }

        public int y
        {
            get { return holder.GetAxis(HID_USAGES.HID_USAGE_Y); }
            set { holder.SetAxis(value, HID_USAGES.HID_USAGE_Y); }
        }

        public int z
        {
            get { return holder.GetAxis(HID_USAGES.HID_USAGE_Z); }
            set { holder.SetAxis(value, HID_USAGES.HID_USAGE_Z); }
        }

        public int rx
        {
            get { return holder.GetAxis(HID_USAGES.HID_USAGE_RX); }
            set { holder.SetAxis(value, HID_USAGES.HID_USAGE_RX); }
        }

        public int ry
        {
            get { return holder.GetAxis(HID_USAGES.HID_USAGE_RY); }
            set { holder.SetAxis(value, HID_USAGES.HID_USAGE_RY); }
        }

        public int rz
        {
            get { return holder.GetAxis(HID_USAGES.HID_USAGE_RZ); }
            set { holder.SetAxis(value, HID_USAGES.HID_USAGE_RZ); }
        }

        public int slider
        {
            get { return holder.GetAxis(HID_USAGES.HID_USAGE_SL0); }
            set { holder.SetAxis(value, HID_USAGES.HID_USAGE_SL0); }
        }

        public int dial
        {
            get { return holder.GetAxis(HID_USAGES.HID_USAGE_SL1); }
            set { holder.SetAxis(value, HID_USAGES.HID_USAGE_SL1); }
        }

        //public void setAxis(int x = int.MaxValue, int y = int.MaxValue, int z = int.MaxValue,
        //                       int rx = int.MaxValue, int ry = int.MaxValue, int rz = int.MaxValue,
        //                       int slider = int.MaxValue, int dial = int.MaxValue)
        //{
        //    if (x != int.MaxValue)
        //        this.x = x;
        //    if (y != int.MaxValue)
        //        this.y = y;
        //    if (z != int.MaxValue)
        //        this.z = z;
        //    if (rx != int.MaxValue)
        //        this.rx = rx;
        //    if (ry != int.MaxValue)
        //        this.ry = ry;
        //    if (rz != int.MaxValue)
        //        this.rz = rz;
        //    if (slider != int.MaxValue)
        //        this.slider = slider;
        //    if (dial != int.MaxValue)
        //        this.dial = dial;
        //}
        public void setAxis(int? x = null, int? y = null, int? z = null,
                               int? rx = null, int? ry = null, int? rz = null,
                               int? slider = null, int? dial = null)
        {
            this.x = x ?? this.x;
            this.y = y ?? this.y;
            this.z = z ?? this.z;
            this.rx = rx ?? this.rx;
            this.ry = ry ?? this.ry;
            this.rz = rz ?? this.rz;
            this.slider = slider ?? this.slider;
            this.dial = dial ?? this.dial;
        }
        public bool IsAxisExist(uint axis) => holder.IsAxisExist(axis);

        // ************************ setButton ***************************************************
        public void setButton(int button, bool pressed) => holder.SetButton(button, pressed);
        // n bool to n vjoybuttons
        public void setButton(IList<int> buttons, IList<bool> values)
        {
            int min = Math.Min(buttons.Count, values.Count);
            for (int i = 0; i < min; i++)
                holder.SetButton(buttons[i], values[i]);
        }
        // 1 bool to n vjoybuttons
        public void setButton(IList<int> buttons, bool value)
        {
            foreach (var b in buttons)
                holder.SetButton(b, value);
        }
        // pov value to 4 vjoybuttons
        public void setButton(IList<int> buttons, int pov)
        {
            int direction = holder.GetDirection(pov);
            for (int i = 0; i < 4; i++)
                holder.SetButton(buttons[i], i == direction);
        }
        // Axis value to 4 vjoybuttons
        public void setButton(IList<int> buttons, float limit, float x, float y)
        {
            int direction = holder.GetDirection(limit, x, y);
            for (int i = 0; i < 4; i++)
                holder.SetButton(buttons[i], i == direction);
        }
        public void setButtonBip(int button, bool pressed, IList<int> bip1 = null, IList<int> bip2 = null)
        {
            holder.SetButton(button, pressed);
            pressed.PlaySound(bip1, bip2);
        }

        // ************************ setdigitalPov **********************************************
        // pov value to direction -1, 0 to 3 (neutral, up, left, down and right)
        public void setDigitalPov(int numpov, int pov)
        {
            holder.SetDirectionalPov(numpov, holder.GetDirection(pov));
        }
        // Axis value to direction -1, 0 to 3 (neutral, up, left, down and right)
        public void setDigitalPov(int numpov, float limit, float x, float y)
        {
            holder.SetDirectionalPov(numpov, holder.GetDirection(limit, x, y));
        }
        // 4 buttons to direction -1, 0 to 3 (neutral, up, left, down and right)
        public void setDigitalPov(int numpov, IList<bool> buttons)
        {
            int direction = -1;
            for (int i = 0; i < 4; i++)
            {
                if (buttons[i])
                {
                    direction = i;
                    break;
                }
            }
            holder.SetDirectionalPov(numpov, direction);
        }

        // ************************ setAnalogPov ***********************************************
        public void setAnalogPov(int numpov, int value)
        {
            holder.SetContinuousPov(numpov, value);
        }
        // Update data in mode DIFFERED **********************************************************
        public void _updateVJD()
        {
            holder._UpdateVJD();
        }
        // DIVERS **********************
        public void setPressed(int button)
        {
            holder.PressAndRelease(button);
        }

        public VjoyVersionGlobal version => holder.Version;
    }
}
