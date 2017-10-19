using System;
using System.Collections;
using System.Collections.Generic;
using FreePIE.Core.Contracts;
using FreePIE.Core.Plugins.Extensions;
using FreePIE.Core.Plugins.ScriptAuto;
using HidSharp;

// see http://members.aon.at/mfranz/warthog.html to learn to use HID with warthog

namespace FreePIE.Core.Plugins
{
    using Gx = GlobalExtensionMethods;

    [GlobalType(Type = typeof (WarthogGlobal))]
    public class WarthogPlugin : Plugin
    {
        const int VID_WARTHOG_THROTTLE = 0x44F;
        const int PID_WARTHOG_THROTTLE = 0x404;
        private HidDeviceLoader loader;
        private HidDevice HidDevice;
        private HidStream stream;
        private blinking bk;
        private readonly byte[] b = new byte[] { 0x04, 0x02, 0x10, 0x01, 0x40, 0x08 };
        private byte[] buffer;

        private ScriptWarthog SF;

        public class blinking
        {
            public BitArray stateleds;
            public int[] numturn;
            public int [] duration;
            public long [] timer;
            
            public blinking()
            {
                // 00-05 current state, 06-11 state saved (before blinking)
                // 12-17 blinking, 18 if true -> at less one update, 19 unused
                stateleds = new BitArray(20, false);    
                timer = new long[] { -1, -1, -1, -1, -1, -1 };
                duration = new[] { -1, -1, -1, -1, -1, -1 };
                //numturn = new int[6];
            }
        }
         public int GetBrightness()
        {
            return buffer[3];
        }
        public void SetBrightness(int value)
        {
            if (value > 5 || value < 0) value = 5;
            buffer[3] = (byte)value;
        }
        public override object CreateGlobal()
        {
            return new WarthogGlobal(this);
        }

        public override string FriendlyName => "warthog";

        public override Action Start()
        {
            bk = new blinking();
            loader = new HidDeviceLoader();
            HidDevice = loader.GetDeviceOrDefault(VID_WARTHOG_THROTTLE, PID_WARTHOG_THROTTLE);
            if (HidDevice == null)
                throw new Exception("The Warthog Trottle seems to be not connected");
            buffer = new byte[HidDevice.MaxInputReportLength];
            buffer[0] = 1; buffer[1] = 6; buffer[2] = 0; buffer[3] = 5;

            if (!HidDevice.TryOpen(out stream))
                throw new Exception("Failed to open the device Warthog");
            SetAllLeds(false);
            OnStarted(this, new EventArgs());
            return null;
        }

        public override void Stop()
        {
            SF = null;
            SetAllLeds(false);
            stream.Close();
            buffer = null;
            HidDevice = null;
            loader = null;
        }

        public override void DoBeforeNextExecute()
        {
            Gx.CheckScriptTimer();

            for (int led = 0; led < 6; led++)
            {
                if (!bk.stateleds[led + 12]) continue;

                var lapse = bk.timer[led].GetLapse();
                if (lapse < bk.duration[led]) continue;

                bk.timer[led] += bk.timer[led].GetLapse();
                UpdateBufferLed(led, !bk.stateleds[led]);
            }
            if (bk.stateleds[18])
                stream.Write(buffer, 0, buffer.Length);
            bk.stateleds[18] = false;

            if (Gx.cmd == 'W')
            {
                if (SF == null)
                {
                    SF = new ScriptWarthog(this);
                }
                Gx.InvokeMethodinfo(ref SF);
            }
        }

        public void SetAllLeds(bool on)
         {
             buffer[2] = (byte)(on ? 0x5F : 0);
             bk.stateleds[18] = true;
             for (int led = 0; led < 6; led++)
                    bk.stateleds[led] = on;
         }
         public void StartLoop(int led, int duration = 800)
         {
             if (bk.stateleds[led + 12])
             {
                 bk.duration[led] = duration;
             }
             else
             {
                 bk.stateleds[led + 12] = true;
                 bk.stateleds[led + 6] = bk.stateleds[led];

                 bk.duration[led] = duration;
                 UpdateBufferLed(led, false);
                 bk.timer[led] = Gx.StartCount();
             }
         }

         public void StopLoop(int led)
         {
             bk.timer[led] = Gx.StopCount();
             bk.stateleds[led + 12] = false; // blinking off
             UpdateBufferLed(led, bk.stateleds[led + 6]); // restore previous status of led
         }

        public bool IsLedFlashing(int led)
        {
            return bk.stateleds[led + 12];
        }
        public bool IsLedOn(int led)
        {
            return (buffer[2] & b[led]) != 0;
        }
 
        private void UpdateBufferLed(int led, bool on)
        {
            if (bk.stateleds[led] == on) return;
            bk.stateleds[led] = on;

            if (on)
                buffer[2] |= b[led];
            else
                buffer[2] &= (byte)~b[led];
            bk.stateleds[18] = true;
        }
        public void SetLed(int led, bool on)
        {
            if (IsLedFlashing(led)) StopLoop(led);
            UpdateBufferLed(led, on);
        }
        public void ToggleLed(int led)
        {
            UpdateBufferLed(led, !IsLedOn(led));
        }
    }
    // led1, led2, led3, led4, led5 and backlight -> 0 to 4 and 5
    [Global(Name = "warthog")]
    public class WarthogGlobal 
    {
        private readonly WarthogPlugin plugin;

        public WarthogGlobal(WarthogPlugin plugin)
        {
            this.plugin = plugin;
        }
        public void setLed(IList<int> leds, bool on)
        {
            foreach (var numled in leds)
                plugin.SetLed(numled, on);
        }
        public void toggleLed(IList<int> leds)
        {
            foreach (var numled in leds)
                plugin.ToggleLed(numled);
        }
        public void startFlashing(IList<int> leds, int duration = 800 )
        {
            foreach (var numled in leds)
                plugin.StartLoop(numled, duration);
        }
        public void stopFlashing(IList<int> leds)
        {
            foreach (var numled in leds)
                plugin.StopLoop(numled);
        }
        public void setLedOn(IList<int> leds)
        {
           setLed(leds, true);
        }
        public void setLedOff(IList<int> leds)
        {
            setLed(leds, false);
        }
        public void setLeds(bool on)
        {
            plugin.SetAllLeds(on);
        }
        public int brigthness
        {
            set { plugin.SetBrightness(value); }
            get { return plugin.GetBrightness(); }
        }
        public bool isLedOn(int led)
        {
            return plugin.IsLedOn(led);
        }
        public bool isLedFlashing(int led)
        {
            return plugin.IsLedFlashing(led);
        }
    }
}

