using System;
using FreePIE.Core.Plugins.Extensions;

namespace FreePIE.Core.Plugins.ScriptAuto
{
    using Gx = GlobalExtensionMethods;
    public class ScriptMouse
    {
        private readonly MousePlugin plugin;
        public ScriptMouse(MousePlugin plugin)
        {
            this.plugin = plugin;
            Gx.AddListOfFct(GetType()); 
        }
        public void D() // MP;button
        {
            //int button = Convert.ToInt32(Gx.wd[1]);
            //if (plugin.IsDown(button)) Gx.NextAction();
            Gx.Testfunction(plugin.IsDown);
        }
        public void P() // MP;button
        {
            int button = Convert.ToInt32(Gx.wd[1]);
            if (plugin.IsPressed(button)) Gx.NextAction();
            //Gx.Testfunction(plugin.IsPressed);
        }
        public void R() // MR;button
        {
            int button = Convert.ToInt32(Gx.wd[1]);
            if (plugin.IsReleased(button)) Gx.NextAction();
           // Gx.Testfunction(plugin.IsReleased);
        }

    }
}

