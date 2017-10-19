using System;
using FreePIE.Core.Plugins.Extensions;

namespace FreePIE.Core.Plugins.ScriptAuto
{
    using Gx = GlobalExtensionMethods;
    public class ScriptWarthog
    {
        private readonly WarthogPlugin plugin;
        public ScriptWarthog(WarthogPlugin plugin)
        {
            this.plugin = plugin;
            Gx.AddListOfFct(GetType());
        }
        private void Testled(Func<int, bool> f)
        {
            var val = Gx.wd[1].Split(':');
            if (f(Convert.ToInt32(val[0]))) // True
            {
                if (val.Length > 1)         // Goto Etiq true
                    string.Format("G0;{0}", val[1]).DecodelineOfCommand(section: null, priority: 3);
                else
                    Gx.NextAction();
            }
            else                            // False
            {
                if (val.Length > 2)         // Goto Etiq False
                    string.Format("G0;{0}", val[2]).DecodelineOfCommand(section: null, priority: 3);
            }
        }

        public void S()   // light on led W0;led1,led2,led3,led4,led5,led6
        {
            foreach (var led in Gx.wd[1].Split(','))
                plugin.SetLed(Convert.ToInt32(led), true);
            Gx.NextAction();
        }
        public void R()   // light off led W0;led1,led2,led3,led4,led5,led6
        {
            foreach (var led in Gx.wd[1].Split(','))
                plugin.SetLed(Convert.ToInt32(led), false);
            Gx.NextAction();
        }
        public void F()   // start flashing led W0;led1,led2,led3,led4,led5,led6
        {
            foreach (var led in Gx.wd[1].Split(','))
                plugin.StartLoop(Convert.ToInt32(led));
            Gx.NextAction();
        }
        public void Q()   // stop flashing led WQ;led1,led2,led3,led4,led5,led6
        {
            foreach (var led in Gx.wd[1].Split(','))
                plugin.StopLoop(Convert.ToInt32(led));
            Gx.NextAction();
        }
        public void T()   // test if led on WT;led<:etiq True:etiq False>
        {
            Testled(plugin.IsLedOn);
        }
        public void Z()   // test if led flashing WZ;led<:etiq True:etiq False>
        {
            Testled(plugin.IsLedFlashing);
        }

    }
}

