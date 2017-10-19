// The MouseKeyIO class encapsulates the Win32 function SendInput()
// and the supporting data structures that are used to emulate both
// mouse and keyboard input

using System;
using System.Runtime.InteropServices;

namespace FreePIE.Core.Plugins {

   static class MouseKeyIO {

      // These are copies of DirectX constants
      public const int INPUT_MOUSE = 0;
      public const int INPUT_KEYBOARD = 1;
      public const int INPUT_HARDWARE = 2;
      public const uint KEYEVENTF_EXTENDEDKEY   = 0x0001;
      public const uint KEYEVENTF_KEYUP         = 0x0002;
      public const uint KEYEVENTF_UNICODE       = 0x0004;
      public const uint KEYEVENTF_SCANCODE      = 0x0008;
      public const uint XBUTTON1 = 0x0001;
      public const uint XBUTTON2 = 0x0002;
      public const uint MOUSEEVENTF_MOVE = 0x0001;
      public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
      public const uint MOUSEEVENTF_LEFTUP = 0x0004;
      public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
      public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
      public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
      public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
      public const uint MOUSEEVENTF_XDOWN = 0x0080;
      public const uint MOUSEEVENTF_XUP = 0x0100;
      public const uint MOUSEEVENTF_WHEEL = 0x0800;
      public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
      public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

      [StructLayout(LayoutKind.Sequential)]
      public struct MOUSEINPUT
      {
         public int dx;
         public int dy;
         public uint mouseData;
         public uint dwFlags;
         public uint time;
         public IntPtr dwExtraInfo;
      }

      [StructLayout(LayoutKind.Sequential)]
      public struct KEYBDINPUT
      {
         public ushort wVk;
         public ushort wScan;
         public uint dwFlags;
         public uint time;
         public IntPtr dwExtraInfo;
      }

      [StructLayout(LayoutKind.Sequential)]
      public struct HARDWAREINPUT
      {
         public uint uMsg;
         public ushort wParamL;
         public ushort wParamH;
      }

      [StructLayout(LayoutKind.Explicit)]
      public struct INPUT
      {
         [FieldOffset(0)]
         public int type;
         [FieldOffset(4)]
         public MOUSEINPUT mi;
         [FieldOffset(4)]
         public KEYBDINPUT ki;
         [FieldOffset(4)]
         public HARDWAREINPUT hi;
      }


        [Flags]
        public enum SPIF
        {
            None = 0x00,
            /// <summary>Writes the new system-wide parameter setting to the user profile.</summary>
            SPIF_UPDATEINIFILE = 0x01,
            /// <summary>Broadcasts the WM_SETTINGCHANGE message after updating the user profile.</summary>
            SPIF_SENDCHANGE = 0x02,
            /// <summary>Same as SPIF_SENDCHANGE.</summary>
            SPIF_SENDWININICHANGE = 0x02
        }
        public class NativeMethods
        {
            [DllImport("user32.dll", SetLastError = true)]
            internal static extern uint SendInput(uint num_inputs, INPUT[] inputs, int size);

            [DllImport("user32.dll", EntryPoint = "SystemParametersInfo", SetLastError = true)]
            public static extern bool SystemParametersInfoGet(uint action, uint param, IntPtr vparam, SPIF fWinIni);
            public const UInt32 SPI_GETMOUSE = 0x0003;
            [DllImport("user32.dll", EntryPoint = "SystemParametersInfo", SetLastError = true)]
            public static extern bool SystemParametersInfoSet(uint action, uint param, IntPtr vparam, SPIF fWinIni);
            public const UInt32 SPI_SETMOUSE = 0x0004;

            [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
            public static extern short GetKeyState(int keyCode);
        }
    }
}
