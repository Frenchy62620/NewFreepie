using FreePIE.Core.Plugins.Extensions;

namespace FreePIE.Core.Plugins.ScriptAuto
{
    using Gx = GlobalExtensionMethods;
    public class ScriptSpeech
    {
        private readonly SpeechPlugin plugin;
        public ScriptSpeech(SpeechPlugin plugin)
        {
            this.plugin = plugin;
            Gx.AddListOfFct(GetType());
        }
        public void Y()       //speech.Say
        {
            plugin.Say(Gx.wd[1]);
            Gx.NextAction();
        }
        public void W()       //wait speaking off before speech.say
        {
            if (plugin.Speaking) return;
            plugin.Say(Gx.wd[1]);
            Gx.NextAction();
        }
        public void E()       //wait speaking false
        {
            if (!plugin.Speaking)
                Gx.NextAction();
        }
        public void X()       // = SW;text!SE
        {
            string.Format("SW;{0}!SE", Gx.wd[1]).DecodelineOfCommand(section: null, priority: 3);
        }
        public void S()       //wait said(text)
        {
            if (plugin.Said(Gx.wd[1], 0.9f)) Gx.NextAction();
        }
    }
}


