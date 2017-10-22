using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using FreePIE.Core.Persistence.Paths;
using FreePIE.Core.Common;
using System.Reflection;

namespace FreePIE.Core.Plugins.Extensions
{
    using Gx = GlobalExtensionMethods;
    public static class GlobalExtensionMethods
    {
        private static readonly IPortable portable = new Portable();
        private static readonly PortablePaths portablepaths = new PortablePaths();
        private static readonly UacCompliantPaths uacPaths = new UacCompliantPaths(new FileSystem());
        private static List<string> commandes;
        private static Stopwatch Utime = new Stopwatch();
        private static int Ctime;
        private static long timer;

        public static int lapse_singleclick;
        public static char cmd;
        public static char subcmd1;
        public static int subcmd2;
        public static string[] wd;
        public static string keystyped;
        public static string keystosay;
        public static Dictionary<char, List<MethodInfo>> dico_mi;

        static GlobalExtensionMethods()
        {
            lapse_singleclick = 300;
            wd = new string[2];
            cmd = '\0';
            keystosay = keystyped = "";
            Ctime = 0;
            timer = -1;
        }
        // --------------------- Extension Executing Auto function -----------------------------------
        public static void AddListOfFct(Type t)
        {
            if (dico_mi == null)
                dico_mi = new Dictionary<char, List<MethodInfo>>();

            if (!dico_mi.ContainsKey(cmd))
            {
                var list_mi = t.GetMethods().Where(m => m.Name.Length == 1);
                dico_mi[cmd] = new List<MethodInfo>(list_mi);
            }
        }
        public static void InvokeMethodinfo<T>(ref T instance)
        {
            if (subcmd1 == '9')
            {
                instance = default(T);
                dico_mi.Remove(cmd);
                if (dico_mi.Count == 0)
                    dico_mi = null;
                NextAction();
                return;
            }
            List<MethodInfo> mi;
            dico_mi.TryGetValue(cmd, out mi);
            var method = mi.FirstOrDefault(m => m.Name.Equals(new string(subcmd1, 1)));
            if (method != null)
                method.Invoke(instance, null);
            else
                NextAction();
        }


        internal static void Testfunction(Func<int, bool, bool> f)
        {
            var val = wd[1].Split(':');
            if (f(Convert.ToInt32(val[0]), false)) // True
            {
                if (val.Length > 1)         // Goto Etiq true
                    $"G0;{val[1]}".DecodelineOfCommand(section: null, priority: 3);
                else
                    NextAction();
            }
            else                            // False
            {
                if (val.Length > 2)         // Goto Etiq False
                    $"G0;{val[2]}".DecodelineOfCommand(section: null, priority: 3);
            }
        }

        // ----------- END ------- Extension Executing Auto function --------------------------

        public static SpeechPlugin speechplugin {get; set; }

        public static int SearchPos(string str, string substr, int lg)
        {
            if (str.Contains('-'))
            {
                var s = Convert.ToInt32((substr));
                var bornes = str.Split('-').Select(c => Convert.ToInt32(c)).ToArray();
                if (s < bornes[0] || s > bornes[1])
                    return -1;
                return s - bornes[0];
            }
            var tab = (str.Split(',').Select(s =>
            {
                var st = new string('0', lg) + s;
                return st.Substring(st.Length - lg, lg);
            })).ToArray();
            for (int indice = 0; indice < tab.Count(); indice++)
            {
                if (tab[indice].Equals(substr))
                    return indice;
            }
            return -1;
        }
        // --------------------- Extension Global Timer -----------------------------------
        public static long StartCount()
        {
                if (Ctime++ == 0)
                    Utime.Restart();
                return Utime.ElapsedMilliseconds;
        }
        public static long ReStartCount()
        {
            return Utime.ElapsedMilliseconds;
        }
        public static long StopCount()
        {
            if (--Ctime <= 0)
            {
                Utime.Stop();
                Ctime = 0;
            }
            return -1;
        }
        public static long GetLapse(this long time)
        {
            if (time < 0)
                return -1;
            return Utime.ElapsedMilliseconds - time;
        }
        public static void CheckScriptTimer()
        {
            if (timer >= 0 && timer.GetLapse() >= Convert.ToInt32(wd[1]))
            {
                timer = StopCount();
                NextAction();
            }
        }
        // ------ END ---------- Extension Global Timer -----------------------------------
        public static string ConvertByteToString(this byte[] source)
        {
            return source != null ? System.Text.Encoding.UTF8.GetString(source) : null;
        }
        public static string FreePiePath(this string file)
        {
            return string.Format("{0}\\{1}\\{2}", portable.IsPortable ? portablepaths.Data : uacPaths.Data, "files_freepie", file);
        }
        public static string getDataTyped(string indice = null)
        {
            if (indice == null) return keystyped;
            var i = Convert.ToInt32(indice);
            var k = keystyped.Split(';');
            return k.Length > i ? k[i] : "";
        }

        public static bool Compare<T>(string op, T buf, T val) where T : IComparable
        {
            // @C:val   ou @C,adress,lg:val ou @C,adress1,lg,adress2
            // @I,indice:val ou @I,indice.......
            // @L,lg:val ou @
            //==:val ou == address,mask,shift:val

            switch (op.Substring(0, op.Length > 1 ? 2 : 1))
            {
                case "@C": return buf.ToString().Contains(val.ToString());
                case "@D": return !buf.ToString().Contains(val.ToString());
                case "@I":
                    var indice = Convert.ToInt32(op.Substring(2));
                    return buf.ToString()[indice] == val.ToString()[0];
                case "@L":
                    return Compare(op.Split(',')[1], buf.ToString().Length, Convert.ToInt32(val));
                case "==": return buf.CompareTo(val) == 0;
                case "<>": return buf.CompareTo(val) != 0;
                case ">": return buf.CompareTo(val) > 0;
                case ">=": return buf.CompareTo(val) >= 0;
                case "<": return buf.CompareTo(val) < 0;
                case "<=": return buf.CompareTo(val) <= 0;
            }

            return false;
        }
        public static void NextAction()
        {
            cmd = '\0';
            LoadCommand();
        }


        public static void DecodelineOfCommand(this string command, string section=null, int priority = 0)
        {
            if (command.Contains(";") || command.Contains("C0"))     // commande unitaire ou Clear Buffer C0?
            {
                AddCommand(command, priority);
            }
            else
            {
                using (StreamReader file = new StreamReader((@"command\" + command).FreePiePath(), System.Text.Encoding.Default))
                {
                    StringBuilder linesb = new StringBuilder(64);
                    string line;
                    bool startsection = !String.IsNullOrWhiteSpace(section);
                    bool endsection = startsection;
                    int id;

                    while ((line = file.ReadLine()) != null)
                    {
                        if (startsection && (!line.Contains(section) || line.IndexOf('#') != 0)) continue;
                        if (endsection && !startsection && line.Contains(section) && line.IndexOf('#') == 0) break;
                        startsection = false;

                        if ((id = line.IndexOf('#')) == 0)
                        {
                            if (line.Contains("END_SCRIPT")) break;
                            continue;
                        }
                        linesb.Clear().Append(line);
                        //if (line.IndexOf('{') != -1)
                        //    linesb.AppendFormat(line, getDataTyped().Split(';'));
                        //else
                        //    linesb.Append(line);
                        
                        //id = id > 0 ? id - (line.Length - linesb.Length)  - 1 : linesb.Length - 1;
                        id = id > 0 ? id - 1 : linesb.Length - 1;

                        for (; id >= 0; id--)
                        {
                            var c = linesb[id];
                            switch (c)
                            {
                                case ' ':
                                case '\t':
                                    continue;
                                default:
                                    break;
                            }
                            break;
                        }
                        if (id != -1)
                            AddCommand(linesb.ToString(0, id + 1), priority);
                    }
                }
            }
            LoadCommand();
        }
        private static void AddCommand(string commande, int priority = 0)
        {
            if (commandes == null)
                commandes = new List<string>();

            switch (priority)
            {
                case 0: // Add new command in last position and execute only if no active command
                    foreach (var line in commande.Split('!'))
                        commandes.Add(line);
                    return;
                case 1: // Add new command in first position but after the active command if exist
                    break;
                //case 2:
                //    // Insert new file of command
                //    cmd = '\0';
                //    break;
                //case 3: // insert new command or group of command in first position and overwrite the active command (NO command file)
                //    cmd = '\0';
                //    break;
                default:
                    cmd = '\0';
                    break;
            }
            var st = commande.Split('!').Reverse();
            foreach (var line in st)
            {
                if (priority == 2)
                    commandes.Insert(subcmd2, line);
                else
                    commandes.Insert(0, line);
            }
            subcmd2 += st.Count();
        }
        private static void LoadCommand()
        {
            if (commandes == null || commandes.Count == 0 || cmd != '\0') return;
            if (commandes[0].IndexOf('{') != -1)
                commandes[0] = string.Format(commandes[0], getDataTyped().Split(';'));
            wd = commandes[0].Split(new char[] { ';' }, 2);
            cmd = wd[0][0];
            if (wd[0].Length > 1) subcmd1 = wd[0][1];
            if (wd[0].Length > 2 && cmd != '*') subcmd2 = Convert.ToInt32(wd[0].Substring(2));
            Console.WriteLine($"cde: {commandes[0]}");
            commandes.RemoveAt(0);
            if (commandes.Count == 0)
                commandes = null;
            switch (cmd)
            {
                case '*':
                case '0':    // 0 = nop
                    NextAction();
                    break;
                case 'T':
                    timer = StartCount();
                    break;
                case 'B':
                    BeepPlugin.BackgroundBeep.Beep(Convert.ToInt32(wd[1]));
                    NextAction();
                    break;
                case 'G':    // Goto etiq
                    if (string.IsNullOrEmpty(wd[1])) NextAction();
                    while(!commandes[0].Contains(wd[1]) || commandes[0][0] != '*')
                        commandes.RemoveAt(0);
                    NextAction();
                    break;
                case 'L':
                    subcmd2 = 0;
                    var st = wd[1].Split(',');
                    if (st.Length == 2)
                        st[0].DecodelineOfCommand(st[1], 2);
                    else
                        st[0].DecodelineOfCommand(null, 2);
                    break;
                case 'C':   // Clear Buffer or dico or  Put in buffer
                    if (subcmd1 == '9') // Clear Dico of Method script function
                    {
                        dico_mi.Clear();
                        dico_mi = null;
                    }
                    else    // Clear Buffer if C0 else Put in buffer
                    {
                        keystosay = "";
                        keystyped = (subcmd1 == '1') ? wd[1] : "";
                    }                       
                    NextAction();
                    break;
                case '9':   // Clear Dico of Method script function
                    dico_mi.Clear();
                    dico_mi = null;
                    break;
            }
            //if (commandes.Count == 0)
            //    commandes = null;
        }

        public static bool PlaySound(this bool value, string audio)
        {
            if (value)
            {
                //if (!audio.Contains('\\'))
                //    audio = audio.FreePiePath();
                //NAudioPlugin.Instance.PlaySound(audio);
            }
            return value;
        }
        //public static bool PlaySound(this bool value, int idcache)
        //{
        //    //if (value)
        //    //    NAudioPlugin.Instance.PlaySound(idcache);
        //    return value;
        //}
        public static bool PlaySound(this bool value, int frequency, int duration = 300)
        {
            if (value)
                BeepPlugin.BackgroundBeep.Beep(frequency, duration);
            return value;
        }
        public static void PlaySound(this bool value, IList<int> bip1 = null, IList<int> bip2 = null)
        {
            if (value)
            {
                if (bip1 != null)
                {
                    if (bip1.Count < 2)
                        BeepPlugin.BackgroundBeep.Beep(1000, 300);
                    else
                        BeepPlugin.BackgroundBeep.Beep(bip1[0], bip1[1]);
                }
            }
            else
            {
                if (bip2 != null)
                {
                    if (bip2.Count < 2)
                        BeepPlugin.BackgroundBeep.Beep(200, 300);
                    else
                        BeepPlugin.BackgroundBeep.Beep(bip2[0], bip2[1]);
                }
            }
        }

    }
}
