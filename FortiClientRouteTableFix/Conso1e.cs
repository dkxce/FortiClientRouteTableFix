//
// C# (.Net Framework) Windows Win7+
// System.Conso1e
// v 0.2, 18.07.2023
// https://github.com/dkxce
// en,ru,1251,utf-8
//

using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace System
{
    /// <summary>
    ///     Console Class to ReadKey / ReadLine with Read Timeout
    /// </summary>
    public class Conso1e
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private static Thread inputThread;
        private static AutoResetEvent getInput, gotInput;

        private static string input;
        private static bool retasis = false;
        private static char pswchar = (char)0;

        static Conso1e()
        {
            getInput = new AutoResetEvent(false);
            gotInput = new AutoResetEvent(false);
            inputThread = new Thread(reader);
            inputThread.IsBackground = true;
            inputThread.Start();
        }

        private static void reader()
        {
            while (true)
            {
                getInput.WaitOne();
                input = readLine();
                gotInput.Set();
            }
        }

        /// <summary>
        ///     Read Line
        /// </summary>
        /// <param name="timeOutMillisecs">Read TimeOut in milliseconds or 0 (zero)</param>
        /// <param name="defaultValue">Default Return Value if Timeout Expired</param>
        /// <param name="returnAsIs">Return Any Entered Value instead of defaultValue or not</param>
        /// <param name="passwordChar">password char</param>
        /// <returns></returns>
        public static string ReadLine(int timeOutMillisecs = 0, string defaultValue = "", bool returnAsIs = false, char passwordChar = (char)0)
        {
            retasis = returnAsIs;
            pswchar = passwordChar;
            if (timeOutMillisecs <= 0) return readLine();
            getInput.Set();
            bool success = gotInput.WaitOne(timeOutMillisecs);
            if (!success) Console.WriteLine();
            return success ? input : (returnAsIs ? input : defaultValue);
        }

        /// <summary>
        ///     Read Password Line
        /// </summary>
        /// <param name="timeOutMillisecs">Read TimeOut in milliseconds or 0 (zero)</param>
        /// <param name="defaultValue">Default Return Value if Timeout Expired</param>
        /// <param name="returnAsIs">Return Any Entered Value instead of defaultValue or not</param>
        /// <returns></returns>
        public static string ReadPassword(int timeOutMillisecs = 0, string defaultValue = "", bool returnAsIs = false)
        {
            return ReadLine(timeOutMillisecs, defaultValue, returnAsIs, '*');
        }

        /// <summary>
        ///     Read Key
        /// </summary>
        /// <param name="timeOutMillisecs">Read TimeOut in milliseconds or 0 (zero)</param>
        /// <param name="defaultKey">Default Return Key if Timeout Expired</param>
        /// <returns></returns>
        public static ConsoleKeyInfo ReadKey(int timeOutMillisecs = 0, ConsoleKey defaultKey = (ConsoleKey)0)
        {
            if (timeOutMillisecs <= 0) return Console.ReadKey();
            ConsoleKeyInfo result = new ConsoleKeyInfo((char)defaultKey, (ConsoleKey)defaultKey, false, false, false);
            Thread thr = new Thread(new ThreadStart(() => { result = Console.ReadKey(); }));
            thr.Start();
            thr.Join(timeOutMillisecs);
            return result;
        }

        private static string readLine()
        {
            bool charIsNull = pswchar == (char)0;
            if (charIsNull && !retasis) return Console.ReadLine();

            string rln = string.Empty;
            if (retasis) input = "";

            ConsoleKey key;
            do
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);
                key = keyInfo.Key;
                if (key == ConsoleKey.Backspace && rln.Length > 0)
                {
                    Console.Write("\b \b");
                    if (rln.Length > 0) rln = rln.Substring(0, rln.Length - 1);
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    Console.Write(charIsNull ? keyInfo.KeyChar.ToString() : pswchar.ToString());
                    rln += keyInfo.KeyChar;
                };
                if (retasis) input = rln;
            } while (key != ConsoleKey.Enter);
            Console.WriteLine();
            return rln;
        }

        public static void AutoClose(int timeOutMillisecs, string text = "Done, Press Enter to close (autoclose in {0} seconds)...")
        {
            if (timeOutMillisecs <= 0) return;
            int wait = timeOutMillisecs / 1000;
            (new Thread(() => {
                int top = Console.CursorTop;
                while (wait > 0)
                {
                    Console.SetCursorPosition(0, top);
                    Console.WriteLine(text, wait--);
                    Thread.Sleep(1000);
                };
            })).Start();
            Conso1e.ReadLine(timeOutMillisecs);
            wait = 0;
        }

        public static void ShowConsole(int timeOutMillisecs = 0) { ShowConsole(IntPtr.Zero, timeOutMillisecs); }

        public static void ShowConsole(IntPtr handle, int timeOutMillisecs = 0)
        {
            IntPtr cHandle = handle == IntPtr.Zero ? GetConsoleWindow() : handle;
            if (cHandle == IntPtr.Zero) return;
            if (timeOutMillisecs <= 0)
                ShowWindow(cHandle, SW_SHOW);
            else
                (new Thread(new ThreadStart(() => {
                    Thread.Sleep(timeOutMillisecs);
                    ShowWindow(cHandle, SW_SHOW);
                }))).Start();
        }

        public static void HideConsole(int timeOutMillisecs = 0) { HideConsole(IntPtr.Zero, timeOutMillisecs); }

        public static void HideConsole(IntPtr handle, int timeOutMillisecs = 0)
        {
            IntPtr cHandle = handle == IntPtr.Zero ? GetConsoleWindow() : handle;
            if (cHandle == IntPtr.Zero) return;
            if (timeOutMillisecs <= 0)
                ShowWindow(cHandle, SW_HIDE);
            else
                (new Thread(new ThreadStart(() => {
                    Thread.Sleep(timeOutMillisecs);
                    ShowWindow(cHandle, SW_HIDE);
                }))).Start();
        }
    }

    internal static class ProcWindows
    {
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        public static void ShowLaunched()
        {
            string currProcName = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location);
            Process[] procs = Process.GetProcessesByName(currProcName);
            foreach (Process proc in procs)
                if ((proc.MainWindowHandle != IntPtr.Zero) && (proc.MainWindowHandle != Process.GetCurrentProcess().MainWindowHandle))
                {
                    ShowWindow(proc.MainWindowHandle, 5);
                    SetForegroundWindow(proc.MainWindowHandle);
                    break;
                };
        }

        public static void Kill()
        {
            string currProcName = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location);
            Process[] procs = Process.GetProcessesByName(currProcName);
            foreach (Process proc in procs)
                if ((proc.MainWindowHandle != IntPtr.Zero) && (proc.MainWindowHandle != Process.GetCurrentProcess().MainWindowHandle))
                {
                    try { TerminateProcess(proc.Handle, 0); } catch { };
                    try { proc.Kill(); } catch { };
                    break;
                };
        }

        public static void Kill(string procName)
        {
            Process[] procs = Process.GetProcessesByName(procName);
            foreach (Process proc in procs)
                if ((proc.MainWindowHandle != IntPtr.Zero) && (proc.MainWindowHandle != Process.GetCurrentProcess().MainWindowHandle))
                {
                    try { TerminateProcess(proc.Handle, 0); } catch { };
                    try { proc.Kill(); } catch { };
                    break;
                };
        }

        public static void Kill(int procId)
        {
            try
            {
                Process proc = Process.GetProcessById(procId);
                if ((proc.MainWindowHandle != IntPtr.Zero) && (proc.MainWindowHandle != Process.GetCurrentProcess().MainWindowHandle))
                {
                    try { TerminateProcess(proc.Handle, 0); } catch { };
                    try { proc.Kill(); } catch { };
                };
            }
            catch { };
        }
    }
}

