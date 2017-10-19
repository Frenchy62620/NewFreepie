using System;
using FreePIE.Core.Plugins.Extensions;

namespace FreePIE.Core.Plugins.ScriptAuto
{
    using Gx = GlobalExtensionMethods;
    public class ScriptJoystick
    {
        private readonly JoystickPlugin plugin;
        public ScriptJoystick(JoystickPlugin plugin)
        {
            this.plugin = plugin;
            Gx.AddListOfFct(GetType());
            //dico_mi = new Dictionary<string, List<MethodInfo>>();
            //var list_mi = GetType().GetMethods().Where(m => m.Name.Length == 1);
            //dico_mi[new string(Gx.cmd, 1)] = new List<MethodInfo>(list_mi); 
        }

        //public MethodInfo getMethodinfo()
        //{
        //    List<MethodInfo> mi;
        //    dico_mi.TryGetValue(new string(Gx.cmd, 1), out mi);
        //    return mi.FirstOrDefault(m => m.Name == new string(Gx.subcmd1, 1));
        //}
        public void X() // JX0;numAxis:op:value
        {
            var val = Gx.wd[1].Split(':');
            int numAxis = Convert.ToInt32(val[0]);
            int valueAxis;
            switch(numAxis)
            {
                case 0:
                    valueAxis = plugin.devices[Gx.subcmd2].State.X;
                    break;
                case 1:
                    valueAxis = plugin.devices[Gx.subcmd2].State.Y;
                    break;
                case 2:
                    valueAxis = plugin.devices[Gx.subcmd2].State.Z;
                    break;
                case 3:
                    valueAxis = plugin.devices[Gx.subcmd2].State.RotationX;
                    break;
                case 4:
                    valueAxis = plugin.devices[Gx.subcmd2].State.RotationY;
                    break;
                case 5:
                    valueAxis = plugin.devices[Gx.subcmd2].State.RotationZ;
                    break;
                case 6:
                    valueAxis = plugin.devices[Gx.subcmd2].State.Sliders[0];
                    break;
                case 7:
                    valueAxis = plugin.devices[Gx.subcmd2].State.Sliders[1];
                    break;
            }
            var value = Convert.ToInt32(val[2]);
            var op = val[1];
            var s = plugin.devices[Gx.subcmd2].State.X;
            Gx.Compare(op, s, value);
        }
        public void P()   // JP0;button:etiq true:etiq false
        {
            int button = Convert.ToInt32(Gx.wd[1]);
            if (plugin.devices[Gx.subcmd2].IsPressed(button)) Gx.NextAction();

        }
        public void R()   // JR0;but:etiq true:etiq false
        {
            int button = Convert.ToInt32(Gx.wd[1]);
            if (plugin.devices[Gx.subcmd2].IsReleased(button)) Gx.NextAction();
        }
        public void D()   // JD0;button:etiq true:etiq false
        {
            Gx.Testfunction(plugin.devices[Gx.subcmd2].IsDown);
        }
    }
}

