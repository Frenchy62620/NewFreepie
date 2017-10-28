using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;//tkz joy
using FreePIE.Core.Contracts;
using FreePIE.Core.Plugins.Globals;
using FreePIE.Core.Plugins.Strategies;
using FreePIE.Core.Plugins.Extensions;
using FreePIE.Core.Plugins.ScriptAuto;
using SharpDX.DirectInput;

//using FreePIE.Core.Persistence.Paths;
//using FreePIE.Core.Common;

namespace FreePIE.Core.Plugins
{
    using System.Xml;
    using Gx = GlobalExtensionMethods;
  
    //[GlobalEnum]
    //public enum Axis
    //{
    //    XY = 0,
    //    Y = 1,
    //    X = 2,
    //}


    [GlobalType(Type = typeof(JoystickGlobal), IsIndexed = true)]
    public class JoystickPlugin : Plugin
    {
        //delegate void Fct();
        public List<Device> devices;
        private ScriptJoystick SF;
        public override object CreateGlobal()
        {
            var directInput = new DirectInput();
            var handle = Process.GetCurrentProcess().MainWindowHandle;
            devices = new List<Device>();
            Directory.CreateDirectory("joysticks".FreePiePath());
            string pathlog =  @"joysticks\joy.log".FreePiePath();

            File.Delete(pathlog);

            XDocument xdoc;
            xdoc = new XDocument(new XElement("Devices"));
            //xdoc.Save(pathlog);

            // fin tkz
            var diDevices = directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);
            XElement el = xdoc.Element("Devices");
            el.Add(
            new XElement("Joysticks_Connected",
                new XAttribute("NBR", diDevices == null ? 0: diDevices.Count()),
            from j in diDevices
            select new XElement("IDENT",
            new XAttribute("id", diDevices.IndexOf(j)),
            new XAttribute("Name", j.InstanceName.TrimEnd('\0'))))
                );
            xdoc.Save(pathlog);
            var creator = new Func<DeviceInstance, JoystickGlobal>(d =>
            {
                var controller = new Joystick(directInput, d.InstanceGuid);
                controller.SetCooperativeLevel(handle, CooperativeLevel.Exclusive | CooperativeLevel.Background);
                controller.Acquire();
                
                var device = new Device(controller, pathlog, devices.Count());
                devices.Add(device);
                return new JoystickGlobal(device);
            });

            //return new GlobalIndexer<JoystickGlobal, int, string>(index => creator(diDevices[index]), index => creator(diDevices.Single(di => di.InstanceName.Contains(index))));

                        //example watch the pov's of dual thrustmaster T16000M joysticks
                        //diagnostics.watch(joystick["T.16000M", 0].pov[0])
                        //diagnostics.watch(joystick["T.16000M", 1].pov[0])

                        //or specify the first device found same syntax as before (the index is optional and defaults to 0)
                        //diagnostics.watch(joystick["T.16000M"].pov[0])
            return new GlobalIndexer<JoystickGlobal, int, string>(intIndex => creator(diDevices[intIndex]), (strIndex, idx) =>
            {
                var d = diDevices.Where(di => di.InstanceName.TrimEnd('\0') == strIndex).ToArray();
                return d.Length > 0 && d.Length > idx ? creator(d[idx]) : null;
            });
        }

        public override void Stop()
        {
            devices.ForEach(d => d.Dispose());
            SF = null;
        }

        public override void DoBeforeNextExecute()
        {
            Gx.CheckScriptTimer();
            if (Gx.cmd == 'J')
            {
                if (SF == null)
                {
                    SF = new ScriptJoystick(this);
                }
                Gx.InvokeMethodinfo(ref SF);
            }

            devices.ForEach(d => d.Reset());
        }

        public override string FriendlyName => "Joystick";
    }

    public sealed class Device : IDisposable
    {
        private readonly Joystick joystick;
        private JoystickState state;

        private readonly GetPressedStrategy<int> getPressedStrategy;
        //deb tkz
        private class InfoJoystick
        {
            public List<axis> Axis = new List<axis>();

            public InfoJoystick(Joystick joystick)
            {
                foreach (DeviceObjectInstance devobj in joystick.GetObjects().Where(d => (d.ObjectId.Flags & DeviceObjectTypeFlags.Axis) != 0))
                {
                    int id = (int)devobj.ObjectId;
                    int min = joystick.GetObjectPropertiesById(devobj.ObjectId).Range.Minimum;
                    int max = joystick.GetObjectPropertiesById(devobj.ObjectId).Range.Maximum;
                    Axis.Add(new axis() { Name = devobj.Name, Id = id, MinValue = min, MaxValue = max });
                }
            }
        }
        private class axis
        {
            public string Name { get; set; }
            public int MinValue { get; set; }
            public int MaxValue { get; set; }
            public int Id { get; set; }
        }
        //fin tkz
        public Device(Joystick joystick, string pathlog, int id)
        {
            getPressedStrategy = new GetPressedStrategy<int>(IsDown);
            this.joystick = joystick;
            GetInfoStick(id, pathlog);//tkz joy
            SetRange(-16383, 16383);
        }

        public void Dispose()
        {
            joystick.Dispose();
        }

        public JoystickState State => state ?? (state = joystick.GetCurrentState());

        public void Reset(/*int id*/) => state = null;

        public void GetInfoStick(int id, string pathlog)  //tkz joy
        {
            InfoJoystick info = new InfoJoystick(joystick);

            XDocument xdoc = XDocument.Load(pathlog);
            XElement el = xdoc.Element("Devices");
            
            el.Add(
                new XElement("Joystick_Name",
                    new XAttribute("ID_SCRIPT", id/*joystick.Properties.JoystickId*/), joystick.Information.InstanceName.TrimEnd('\0')),
                new XElement("Joystick_Char",
                        new XAttribute("Buttons", joystick.Capabilities.ButtonCount),
                        new XAttribute("Axis", joystick.Capabilities.AxeCount),
                        new XAttribute("POV", joystick.Capabilities.PovCount),
                        from ax in info.Axis
                        orderby ax.Id ascending
                        select new XElement("AXIS",
                            new XAttribute("Id", ax.Id),
                            new XAttribute("MinVal", ax.MinValue),
                            new XAttribute("MaxVal", ax.MaxValue), ax.Name.TrimEnd('\0')
                        )
               )
            );
            xdoc.Save(pathlog);
        }
        public void SetRange(int minvalue, int maxvalue, int idAxis = -1)
        {
            foreach (var devobj in joystick.GetObjects().Where(d => (d.ObjectId.Flags & DeviceObjectTypeFlags.Axis) != 0))
            {
                int id = (int)devobj.ObjectId;
                if (id == idAxis || idAxis < 0)
                    joystick.GetObjectPropertiesById(devobj.ObjectId).Range = new InputRange(minvalue, maxvalue);
                //if ((devobj.ObjectId.Flags & DeviceObjectTypeFlags.Axis) != 0)
                //{
                //    int id = (int)devobj.ObjectId;
                //    if (id==idAxis || idAxis < 0)
                //        joystick.GetObjectPropertiesById(devobj.ObjectId).Range =  new InputRange(minvalue, maxvalue);
                //}
            }
        }
        public List<int> GetRange(int idAxis)
        {
            int min = 0, max = 0;
            foreach (var devobj in joystick.GetObjects().Where(d => (d.ObjectId.Flags & DeviceObjectTypeFlags.Axis) != 0))
            {
                int id = (int)devobj.ObjectId;
                if (id == idAxis)
                {
                    min = joystick.GetObjectPropertiesById(devobj.ObjectId).Range.Minimum;
                    max = joystick.GetObjectPropertiesById(devobj.ObjectId).Range.Maximum;
                    break;
                }
            }
            return new List<int>() { min, max };
        }

        public string getNameOfJoy()
        {
            return joystick.Information.InstanceName.TrimEnd('\0');
        }
        public bool IsPressed(int button) => getPressedStrategy.IsPressed(button);
        public bool IsReleased(int button) => getPressedStrategy.IsReleased(button);

        public bool IsDown(int button, bool value = false)
        {
            if (button < 128)
                return State.Buttons[button];
            // its joy pov
            int numpov = button / 1000 - 1;
            int pov = (button % 1000) * 9000;
            return State.PointOfViewControllers[numpov] == pov;
        }

        public bool IsSingleClicked(int button) => getPressedStrategy.IsSingleClicked(button);
        public bool IsDoubleClicked(int button) => getPressedStrategy.IsDoubleClicked(button);
        public int HeldDown(int button, int nbvalue, int lapse) => getPressedStrategy.HelDowned(button, IsDown(button), nbvalue, lapse);
        public void HeldDownStop(int button) => getPressedStrategy.HelDownStop(button);

        //public int getDirection(int numpov, bool eightdirection = false)
        //{
        //    int value = State.PointOfViewControllers[numpov];
        //    if (value >= 0)
        //    {
        //        if (eightdirection) // intermediaire position 
        //        {                   // [U,R,D,L] = [0,1,2,3] and [UR, RD, DL, LU] =  [4,5,6,7];
        //            value /= 4500;  // U0,1,R2,3,D4,5,L6,7 -> 0
        //            return value % 2 == 0 ? value / 2 : (value / 2) + 4;
        //        }
        //        else
        //            return value / 9000;     // only [U,R,D,L] = [0,1,2,3]
        //    }
        //    return -1;
        //}
    }

    [Global(Name = "joystick")]
    public class JoystickGlobal
    {
        private readonly Device device;
        public JoystickGlobal(Device device)
        {
            this.device = device;
        }
        private JoystickState State => device.State;

        private int IdentPov(int numpov, int direction) => 1000 * (numpov + 1) + direction;


        // ****************** button getDown ****************************************
        public bool getDown(int button, bool value = true) => value && device.IsDown(button);
        public List<bool> getDown(IList<int> buttons, bool value = true)
        {
            List<bool> b = new List<bool>();
            foreach (var button in buttons)
                b.Add(value && device.IsDown(button));
            
            return b;
        }
 
        // ****************** button getPressed *************************************
        public bool getPressed(int button, bool value = true) => value && device.IsPressed(button);
        public bool getPressedBip(int button, int frequency, int duration = 300) => getPressed(button).PlaySound(frequency, duration);

        // ****************** button getReleased ************************************
        public bool getReleased(int button) => device.IsReleased(button);
        public bool getReleasedBip(int button, int frequency, int duration = 300) => getReleased(button).PlaySound(frequency, duration);

        // ****************** button single or double clicked ************************************
        public bool getClicked(int button, bool dblclick = false) => dblclick ? device.IsDoubleClicked(button) : device.IsSingleClicked(button);
        public bool getClickedBip(int button, bool dblclick = false, int frequency = 300, int duration = 300) => getClicked(button, dblclick).PlaySound(frequency, duration);

        // ****************** button Helddown ************************************

        public int getHeldDown(int button, int nbvalue, int duration) => device.HeldDown(button, nbvalue, duration);
        public void getHeldDownStop(int button) => device.HeldDownStop(button);

        // ****************** button getXstates in list, down, pressed and released **
        public List<bool> getStates(int button, int state = 3 /* 1 down 2 Pressed, 4 Released */)
        {
            List<bool> b = new List<bool>();
            if ((state & 0x01) != 0) b.Add(device.IsDown(button));
            if ((state & 0x02) != 0) b.Add(device.IsPressed(button));
            if ((state & 0x04) != 0) b.Add(device.IsReleased(button));
            return b;
        }

        // *************** getPovDirection *************************************************************

        // joypov is down to this direction?
        // direction = -1 (neutral), 0 (up), 1 (right), 2 (down) or 3 (right)
        public bool getPovDown(int numpov, int direction) => device.IsDown(IdentPov(numpov, direction));

        // joypov is pressed/released to/from this direction?
        public bool getPovPressed(int numpov, int direction) => device.IsPressed(IdentPov(numpov, direction));
        public bool getPovReleased(int numpov, int direction) => device.IsReleased(IdentPov(numpov, direction));
       
        public void setRange(int minvalue, int maxvalue, int idAxis = -1)
        {
            device.SetRange(minvalue, maxvalue, idAxis);
        }

        public void setRange(int minvalue, int maxvalue, IList<int> idAxis)
        {
            foreach(var id in idAxis)
                device.SetRange(minvalue, maxvalue, id);
        }

        public string getName() => device.getNameOfJoy(); 
        public int x => State.X;
        public int y => State.Y;
        public int z => State.Z;
        public int xRotation => State.RotationX;
        public int yRotation => State.RotationY;
        public int zRotation => State.RotationZ;
        public int[] sliders => State.Sliders;
        public int[] pov => State.PointOfViewControllers;
 
    }
}
