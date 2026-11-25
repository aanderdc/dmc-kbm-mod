using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace DmcKbmMod
{
    public static class NativeInput
    {
        // Unique signature ("DMCK") attached to all synthetic SendInput events so our hook ignores them.
        public static readonly UIntPtr INJECTED_SIGNATURE = new UIntPtr(0x444D434Bu);

        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;

        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;

        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;
        public const int WM_MBUTTONDOWN = 0x0207;
        public const int WM_MBUTTONUP = 0x0208;
        public const int WM_MOUSEWHEEL = 0x020A;
        public const int WM_XBUTTONDOWN = 0x020B;
        public const int WM_XBUTTONUP = 0x020C;
        public const int WM_INPUT = 0x00FF;

        public const int XBUTTON1 = 0x0001;
        public const int XBUTTON2 = 0x0002;

        public const uint INPUT_MOUSE = 0;
        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_SCANCODE = 0x0008;

        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        public const uint MOUSEEVENTF_XDOWN = 0x0080;
        public const uint MOUSEEVENTF_XUP = 0x0100;

        public const uint MAPVK_VK_TO_VSC = 0;

        public const uint RIDEV_INPUTSINK = 0x00000100;
        public const uint RID_INPUT = 0x10000003;
        public const uint RIM_TYPEMOUSE = 0;
        public const ushort MOUSE_MOVE_ABSOLUTE = 0x01;

        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const uint TOKEN_QUERY = 0x0008;
        public const int TokenElevation = 20;

        public delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public INPUTUNION U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct RAWMOUSE
        {
            [FieldOffset(0)] public ushort usFlags;
            [FieldOffset(4)] public uint ulButtons;
            [FieldOffset(4)] public ushort usButtonFlags;
            [FieldOffset(6)] public ushort usButtonData;
            [FieldOffset(8)] public uint ulRawButtons;
            [FieldOffset(12)] public int lLastX;
            [FieldOffset(16)] public int lLastY;
            [FieldOffset(20)] public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUT
        {
            public RAWINPUTHEADER header;
            public RAWMOUSE mouse;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClipCursor(ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClipCursor(IntPtr lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, out uint TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        public static bool IsCurrentProcessAdmin()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(id);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool IsProcessElevated(uint pid)
        {
            IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProc == IntPtr.Zero)
            {
                // Access denied usually means the target process is elevated while we are not.
                int err = Marshal.GetLastWin32Error();
                return err == 5; // ERROR_ACCESS_DENIED
            }
            try
            {
                IntPtr hToken;
                if (!OpenProcessToken(hProc, TOKEN_QUERY, out hToken))
                    return false;
                try
                {
                    uint elevation = 0;
                    uint len = 0;
                    if (GetTokenInformation(hToken, TokenElevation, out elevation, sizeof(uint), out len))
                        return elevation != 0;
                    return false;
                }
                finally
                {
                    CloseHandle(hToken);
                }
            }
            finally
            {
                CloseHandle(hProc);
            }
        }

        public static bool GetWindowClientScreenRect(IntPtr hwnd, out RECT rect)
        {
            rect = new RECT();
            RECT client;
            if (!GetClientRect(hwnd, out client))
                return false;

            POINT tl = new POINT { X = client.Left, Y = client.Top };
            POINT br = new POINT { X = client.Right, Y = client.Bottom };
            if (!ClientToScreen(hwnd, ref tl) || !ClientToScreen(hwnd, ref br))
                return false;

            rect.Left = tl.X;
            rect.Top = tl.Y;
            rect.Right = br.X;
            rect.Bottom = br.Y;
            return rect.Width > 50 && rect.Height > 50;
        }

        public static bool RegisterMouseRawInput(IntPtr hwnd)
        {
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01; // Generic Desktop Controls
            rid[0].usUsage = 0x02;     // Mouse
            rid[0].dwFlags = RIDEV_INPUTSINK;
            rid[0].hwndTarget = hwnd;
            return RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        }

        public static bool TryReadRawMouseDelta(IntPtr lParam, out int dx, out int dy)
        {
            dx = 0;
            dy = 0;
            uint dwSize = 0;
            uint headerSize = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, headerSize);
            if (dwSize == 0 || dwSize > 1024)
                return false;

            IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
            try
            {
                if (GetRawInputData(lParam, RID_INPUT, buffer, ref dwSize, headerSize) != dwSize)
                    return false;

                RAWINPUT raw = (RAWINPUT)Marshal.PtrToStructure(buffer, typeof(RAWINPUT));
                if (raw.header.dwType != RIM_TYPEMOUSE)
                    return false;

                // Ignore absolute pointer devices (tablets / RDP absolute) for relative camera delta
                if ((raw.mouse.usFlags & MOUSE_MOVE_ABSOLUTE) != 0)
                    return false;

                dx = raw.mouse.lLastX;
                dy = raw.mouse.lLastY;
                return dx != 0 || dy != 0;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        // DirectInput-safe scan code table for standard & extended keys.
        private static readonly HashSet<ushort> ExtendedVirtualKeys = new HashSet<ushort>
        {
            0x21, // PageUp
            0x22, // PageDown
            0x23, // End
            0x24, // Home
            0x25, // Left
            0x26, // Up
            0x27, // Right
            0x28, // Down
            0x2D, // Insert
            0x2E, // Delete
            0xA3, // RControl
            0xA5, // RMenu (RAlt)
            0x6F, // Divide (Numpad /)
            0x90  // NumLock
        };

        public static ushort GetScanCode(ushort vk, out bool isExtended)
        {
            isExtended = ExtendedVirtualKeys.Contains(vk);
            // Explicit scan codes for keys where MapVirtualKey can vary by keyboard layout
            switch (vk)
            {
                case 0x26: isExtended = true; return 0x48; // Up
                case 0x28: isExtended = true; return 0x50; // Down
                case 0x25: isExtended = true; return 0x4B; // Left
                case 0x27: isExtended = true; return 0x4D; // Right
                case 0xA0: isExtended = false; return 0x2A; // LShift
                case 0xA1: isExtended = false; return 0x36; // RShift
                case 0xA2: isExtended = false; return 0x1D; // LCtrl
                case 0xA3: isExtended = true; return 0x1D;  // RCtrl
                case 0xA4: isExtended = false; return 0x38; // LAlt
                case 0xA5: isExtended = true; return 0x38;  // RAlt
                case 0x20: isExtended = false; return 0x39; // Space
                case 0x0D: isExtended = false; return 0x1C; // Enter
                case 0x1B: isExtended = false; return 0x01; // Escape
                case 0x09: isExtended = false; return 0x0F; // Tab
                case 0x08: isExtended = false; return 0x0E; // Backspace
            }

            uint mapped = MapVirtualKey(vk, MAPVK_VK_TO_VSC);
            return (ushort)(mapped & 0xFF);
        }

        public static void SendScanKey(ushort vk, bool keyDown)
        {
            if (vk == 0) return;

            bool isExtended;
            ushort scan = GetScanCode(vk, out isExtended);
            if (scan == 0) return;

            uint flags = KEYEVENTF_SCANCODE;
            if (isExtended) flags |= KEYEVENTF_EXTENDEDKEY;
            if (!keyDown) flags |= KEYEVENTF_KEYUP;

            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = 0; // Must be 0 when KEYEVENTF_SCANCODE is used for cleanest DirectInput behavior
            inputs[0].U.ki.wScan = scan;
            inputs[0].U.ki.dwFlags = flags;
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = INJECTED_SIGNATURE;

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SendMouseButton(InputCode mouseCode, bool isDown)
        {
            uint flags = 0;
            uint data = 0;
            switch (mouseCode)
            {
                case InputCode.MouseLeft:
                    flags = isDown ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;
                    break;
                case InputCode.MouseRight:
                    flags = isDown ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP;
                    break;
                case InputCode.MouseMiddle:
                    flags = isDown ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP;
                    break;
                case InputCode.MouseX1:
                    flags = isDown ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP;
                    data = XBUTTON1;
                    break;
                case InputCode.MouseX2:
                    flags = isDown ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP;
                    data = XBUTTON2;
                    break;
                default:
                    return;
            }

            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].U.mi.dx = 0;
            inputs[0].U.mi.dy = 0;
            inputs[0].U.mi.mouseData = data;
            inputs[0].U.mi.dwFlags = flags;
            inputs[0].U.mi.time = 0;
            inputs[0].U.mi.dwExtraInfo = INJECTED_SIGNATURE;

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SendTargetInput(InputCode target, bool isDown)
        {
            if (ModConfig.IsKeyboardCode(target))
                SendScanKey((ushort)target, isDown);
            else if (ModConfig.IsMouseCode(target))
                SendMouseButton(target, isDown);
        }
    }
}
