using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PcControl.Client.data;

public static class BloqueoTeclado
    {
        // Códigos de teclas especiales
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104; // Para teclas con ALT
        
        // Teclas a bloquear
        private const int VK_LWIN = 0x5B; // Windows Izquierda
        private const int VK_RWIN = 0x5C; // Windows Derecha
        private const int VK_TAB = 0x09;  // Tab (Para Alt+Tab)
        private const int VK_ESCAPE = 0x1B; // Esc (Para Ctrl+Esc)
        private const int VK_DELETE = 0x2E; // Delete

        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        public static void Bloquear()
        {
            if (_hookID == IntPtr.Zero)
                _hookID = SetHook(_proc);
        }

        public static void Desbloquear()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool bloquear = false;

                // 1. Bloquear Tecla Windows (Izquierda y Derecha)
                if (vkCode == VK_LWIN || vkCode == VK_RWIN) bloquear = true;

                // 2. Bloquear Alt+Tab (El sistema marca el bit de contexto para Alt)
                // Nota: Detectar Alt+Tab exacto es complejo, aquí bloqueamos TAB si es tecla de sistema
                // O simplemente bloqueamos teclas comunes de escape
                
                // Si quieres ser agresivo, bloquea Alt+Tab y Ctrl+Esc:
                if (vkCode == VK_TAB && ((GetKeyState(0x12) & 0x8000) != 0)) bloquear = true; // Alt+Tab
                if (vkCode == VK_ESCAPE && ((GetKeyState(0x11) & 0x8000) != 0)) bloquear = true; // Ctrl+Esc (Menu Inicio)

                if (bloquear) return (IntPtr)1; // 1 significa "Ignorar tecla"
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
    }