//using System;
using System.IO;
//using System.Collections.Generic;
using System.Xml;
using System.Linq;
using System.Xml.Linq;
using System.Speech.Recognition.SrgsGrammar;
//using System.Speech.Recognition;
namespace FreePIE.Core.Plugins.Extensions
{
    public static class SpeechExtensionMethods
    {
        public static void GrammarCompile(this string xml_grammar, string cfg_grammar)
        {
            FileStream fs = new FileStream(cfg_grammar, FileMode.Create);
            XmlReader reader = XmlReader.Create(xml_grammar);
            SrgsGrammarCompiler.Compile(reader, (Stream)fs);
            fs.Close();
        }
        public static string GetName(this string file, bool add_dir = false)
        {
            if (add_dir) file = file.FreePiePath();
            XDocument xdoc = XDocument.Load(file);
            XNamespace ns = xdoc.Root.Attribute("xmlns").Value;
            return xdoc.Root.Descendants(ns + "meta").First().Attribute("name").Value;
        }
        //public static void SetGrammar(this string name)
        //{
        //    XDocument xdoc = XDocument.Load(xml.FreePiePath());
        //    XNamespace ns = xdoc.Root.Attribute("xmlns").Value;
        //    return xdoc.Root.Descendants(ns + "meta").First().Attribute("name").Value;
        //}


    }
}
