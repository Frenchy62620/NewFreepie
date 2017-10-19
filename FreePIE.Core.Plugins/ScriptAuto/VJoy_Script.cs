using System;
using FreePIE.Core.Plugins.Extensions;

namespace FreePIE.Core.Plugins.ScriptAuto
{
    using Gx = GlobalExtensionMethods;
    public class ScriptVJoy
    {
        private readonly VJoyPlugin plugin;
        public ScriptVJoy(VJoyPlugin plugin)
        {
            this.plugin = plugin;
            Gx.AddListOfFct(GetType());
        }

        public void Z()
        {
            foreach (var vbut in Gx.wd[1].Split(':'))
                plugin.holders[Gx.subcmd2].PressAndRelease(Convert.ToInt32(vbut));
            Gx.NextAction();
        }
        public void P()
        {
            foreach (var vbut in Gx.wd[1].Split(':'))
                 plugin.holders[Gx.subcmd2].SetButton(Convert.ToInt32(vbut), true);
            Gx.NextAction();
        }
        public void R()
        {
            foreach (var vbut in Gx.wd[1].Split(':'))
                plugin.holders[Gx.subcmd2].SetButton(Convert.ToInt32(vbut), false);
            Gx.NextAction();
        }

    }
}

