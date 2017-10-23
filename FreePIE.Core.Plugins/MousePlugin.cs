using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FreePIE.Core.Contracts;
using FreePIE.Core.Plugins.Strategies;
using FreePIE.Core.Plugins.Extensions;
using FreePIE.Core.Plugins.ScriptAuto;
using SharpDX.DirectInput;

namespace FreePIE.Core.Plugins
{
    using System.Collections.Generic;
    using Gx = GlobalExtensionMethods;

    [GlobalType(Type = typeof(MouseGlobal))]
    public class MousePlugin : Plugin
    {
        // Mouse position state variables
        private double deltaXOut;
        private double deltaYOut;
        private int wheel;
        public const int WheelMax = 120;

        private DirectInput directInputInstance = new DirectInput();
        private Mouse mouseDevice;
        private MouseState currentMouseState;
        private bool leftPressed;
        private bool rightPressed;
        private bool middlePressed;
        private GetPressedStrategy<int> getButtonPressedStrategy;
        private SetPressedStrategy<int> setButtonPressedStrategy;
        private ScriptMouse SF;
        public override object CreateGlobal()
        {
            return new MouseGlobal(this);
        }

        public override Action Start()
        {
            IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;

            mouseDevice = new Mouse(directInputInstance);
            if (mouseDevice == null)
                throw new Exception("Failed to create mouse device");

            mouseDevice.SetCooperativeLevel(handle, CooperativeLevel.Background | CooperativeLevel.NonExclusive);
            mouseDevice.Properties.AxisMode = DeviceAxisMode.Relative;   // Get delta values
            mouseDevice.Acquire();

            getButtonPressedStrategy = new GetPressedStrategy<int>(IsDown);
            setButtonPressedStrategy = new SetPressedStrategy<int>(SetButtonDown, SetButtonUp);
          
            OnStarted(this, new EventArgs());
            return null;
        }

        public override void Stop()
        {
            SF = null;
            if (mouseDevice != null)
            {
                mouseDevice.Unacquire();
                mouseDevice.Dispose();
                mouseDevice = null;
            }

            if (directInputInstance != null)
            {
                directInputInstance.Dispose();
                directInputInstance = null;
            }
        }
        
        public override string FriendlyName => "Mouse";

        private static MouseKeyIO.MOUSEINPUT MouseInput(int x, int y, uint data, uint t, uint flag)
        {
           var mi = new MouseKeyIO.MOUSEINPUT {dx = x, dy = y, mouseData = data, time = t, dwFlags = flag};
            return mi;
        }

        public override void DoBeforeNextExecute()
        {
            Gx.CheckScriptTimer();

            if (Gx.cmd == 'M')
            {
                if (SF == null)
                {
                    SF = new ScriptMouse(this);
                }
                Gx.InvokeMethodinfo(ref SF);
            }
            // If a mouse command was given in the script, issue it all at once right here
            if ((int)deltaXOut != 0 || (int)deltaYOut != 0 || wheel != 0)
            {
                var input = new MouseKeyIO.INPUT[1];
                input[0].type = MouseKeyIO.INPUT_MOUSE;
                input[0].mi = MouseInput((int)deltaXOut, (int)deltaYOut, (uint)wheel, 0, MouseKeyIO.MOUSEEVENTF_MOVE | MouseKeyIO.MOUSEEVENTF_WHEEL);

                MouseKeyIO.NativeMethods.SendInput(1, input, Marshal.SizeOf(input[0].GetType()));

                // Reset the mouse values
                if ((int)deltaXOut != 0)
                {
                    deltaXOut = deltaXOut - (int)deltaXOut;
                }
                if ((int)deltaYOut != 0)
                {
                    deltaYOut = deltaYOut - (int)deltaYOut;
                }

                wheel = 0;
            }

            currentMouseState = null;  // flush the mouse state

            setButtonPressedStrategy.Do();
        }
        public MouseState getmouse() => mouseDevice.GetCurrentState();
        public double DeltaX
        {
            set
            {
                deltaXOut = deltaXOut + value;
            }

            get { return CurrentMouseState.X; }
        }

        public double DeltaY
        {
            set
            {
                deltaYOut = deltaYOut + value;
            }

            get { return CurrentMouseState.Y; }
        }

        public int Wheel
        {
            get { return CurrentMouseState.Z; }
            set { wheel = value; }
        }

        private MouseState CurrentMouseState => currentMouseState ?? (currentMouseState = mouseDevice.GetCurrentState());

        public bool IsDown(int button, bool value = false) => CurrentMouseState.Buttons[button];
        public bool IsPressed(int button) => getButtonPressedStrategy.IsPressed(button);
        public bool IsReleased(int button) => getButtonPressedStrategy.IsReleased(button);
        private void SetButtonDown(int button) => SetButtonPressed(button, true);
        private void SetButtonUp(int button) => SetButtonPressed(button, false);

        public void SetButtonPressed(int button, bool pressed)
        {
            uint btn_flag = 0;

            if (button == 0)
            {
               if (pressed)
               {
                  if (!leftPressed)
                     btn_flag = MouseKeyIO.MOUSEEVENTF_LEFTDOWN;
               }
               else
               {
                  if (leftPressed)
                     btn_flag = MouseKeyIO.MOUSEEVENTF_LEFTUP;
               }
               leftPressed = pressed;
            }
            else if (button == 1)
            {
               if (pressed)
               {
                  if (!rightPressed)
                     btn_flag = MouseKeyIO.MOUSEEVENTF_RIGHTDOWN;
               }
               else
               {
                  if (rightPressed)
                     btn_flag = MouseKeyIO.MOUSEEVENTF_RIGHTUP;
               }
               rightPressed = pressed;
            }
            else
            {
               if (pressed)
               {
                  if (!middlePressed)
                     btn_flag = MouseKeyIO.MOUSEEVENTF_MIDDLEDOWN;
               }
               else
               {
                  if (middlePressed)
                     btn_flag = MouseKeyIO.MOUSEEVENTF_MIDDLEUP;
               }
               middlePressed = pressed;
            }
           
            if (btn_flag != 0) {
               var input = new MouseKeyIO.INPUT[1];
               input[0].type = MouseKeyIO.INPUT_MOUSE;
               input[0].mi = MouseInput(0, 0, 0, 0, btn_flag);
            
               MouseKeyIO.NativeMethods.SendInput(1, input, Marshal.SizeOf(input[0].GetType()));
            }
        }

        public void PressAndRelease(int button) => setButtonPressedStrategy.Add(button);
        public bool IsSingleClicked(int button) => getButtonPressedStrategy.IsSingleClicked(button);
        public bool IsDoubleClicked(int button) => getButtonPressedStrategy.IsDoubleClicked(button);
        public bool IsHeldDown(int button, int duration) => getButtonPressedStrategy.IsHelDowned(button, IsDown(button), duration);
        public int IsHeldDown(int button, long[] duration) => getButtonPressedStrategy.IsHelDowned(button, IsDown(button), duration);
        public bool SetEnhancePointerPrecision(int state = -1)
        {
            int[] mouseParams = new int[3];
            if (state < 0)
            {
                // toggle value, Get the current values.
                MouseKeyIO.NativeMethods.SystemParametersInfoGet(MouseKeyIO.NativeMethods.SPI_GETMOUSE, 0, GCHandle.Alloc(mouseParams, GCHandleType.Pinned).AddrOfPinnedObject(), 0);
                mouseParams[2] = 1 - mouseParams[2];
            }
            else
                mouseParams[2] = state;
            // Update the system setting.
            return MouseKeyIO.NativeMethods.SystemParametersInfoSet(MouseKeyIO.NativeMethods.SPI_SETMOUSE, 0, GCHandle.Alloc(mouseParams, GCHandleType.Pinned).AddrOfPinnedObject(), MouseKeyIO.SPIF.SPIF_SENDCHANGE);
        }

    }

    [Global(Name = "mouse")]
    public class MouseGlobal
    {
        private readonly MousePlugin plugin;

        public MouseGlobal(MousePlugin plugin)
        {
            this.plugin = plugin;
        } 

        public int wheelMax => MousePlugin.WheelMax;
        public double deltaX
        {
            get { return plugin.DeltaX; }
            set { plugin.DeltaX = value; }
        }

        public double deltaY
        {
            get { return plugin.DeltaY; }
            set { plugin.DeltaY = value; }
        }

        public int wheel
        {
            get { return plugin.Wheel; }
            set { plugin.Wheel = value; }
        }

        public bool wheelUp
        {
            get { return plugin.Wheel == wheelMax; }
            set { plugin.Wheel = value ? wheelMax : 0; }
        }

        public bool wheelDown
        {
            get { return plugin.Wheel == -wheelMax; }
            set { plugin.Wheel = value ? -wheelMax : 0; }
        }

        public bool leftButton
        {
            get { return plugin.IsDown(0); }
            set { plugin.SetButtonPressed(0, value); }
        }

        public bool middleButton
        {
            get { return plugin.IsDown(2); }
            set { plugin.SetButtonPressed(2, value); }
        }

        public bool rightButton
        {
            get { return plugin.IsDown(1); }
            set { plugin.SetButtonPressed(1, value); }
        }

        public bool getButton(int button) => plugin.IsDown(button);
        public void setButton(int button, bool pressed) => plugin.SetButtonPressed(button, pressed);
        public bool getPressed(int button) => plugin.IsPressed(button);
        public bool getReleased(int button) => plugin.IsReleased(button);
        public bool getClicked(int button, bool dblclick = false) => dblclick ? plugin.IsDoubleClicked(button) : plugin.IsSingleClicked(button);
        public void setPressed(int button) => plugin.PressAndRelease(button);
        public bool getHelddown(int button, int duration) => plugin.IsHeldDown(button, duration);
        public int getHeldDown(int button, IList<int> duration)
        {
            long[] durations = new long[duration.Count];
            for (int i = 0; i < duration.Count; i++)
                durations[i] = duration[i];
            return plugin.IsHeldDown(button, durations);
        }
        public bool setPointerPrecision(int state = -1 /* 0 = disable, 1 = enable, -1 toogle */)
        {
            return plugin.SetEnhancePointerPrecision(state);
        }
    }
}
