using Reloaded.Assembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Full_Bot
{
    public class AsmRunner : IDisposable
    {
        private const uint AllocationSize = 1024;
        private const uint MemCommitReserve = 0x1000 | 0x2000;
        private const uint PageExecuteReadWrite = 0x40;
        private const uint MemRelease = 0x8000;
        private const uint ThreadWaitTimeoutMs = 3000;

        private readonly Assembler _assembler = new Assembler();
        private readonly object _lock = new object();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, uint lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, uint lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        public void RunScript(MemoryProcess mem, string scriptPath, Dictionary<string, uint> extraVars = null)
        {
            if (mem == null) throw new ArgumentNullException(nameof(mem));
            if (string.IsNullOrWhiteSpace(scriptPath)) throw new ArgumentException("Caminho do script ASM inválido.", nameof(scriptPath));
            if (!File.Exists(scriptPath)) throw new FileNotFoundException($"Script ASM não encontrado: '{scriptPath}'.", scriptPath);

            lock (_lock)
            {
                string asm = File.ReadAllText(scriptPath);
                asm = Regex.Replace(asm, @"//.*", string.Empty);
                asm = Regex.Replace(asm, @"\b(byte|word|dword)\s+ptr\b", "$1", RegexOptions.IgnoreCase);

                IntPtr alloc = VirtualAllocEx(mem.Handle, IntPtr.Zero, AllocationSize, MemCommitReserve, PageExecuteReadWrite);
                if (alloc == IntPtr.Zero)
                    throw new InvalidOperationException("Falha ao alocar memória no processo alvo.");

                uint allocAddr = unchecked((uint)alloc.ToInt64());

                try
                {
                    var vars = mem.GetAsmVariables();
                    vars["ALLOC_ADDR"] = allocAddr;

                    if (extraVars != null)
                    {
                        foreach (var kv in extraVars)
                            vars[kv.Key] = kv.Value;
                    }

                    foreach (var kv in vars)
                        asm = asm.Replace($"{{{kv.Key}}}", $"0x{kv.Value:X}");

                    var leftover = Regex.Match(asm, @"\{[A-Z0-9_]+\}", RegexOptions.IgnoreCase);
                    if (leftover.Success)
                        throw new InvalidOperationException($"Variável ASM não resolvida: {leftover.Value} em '{scriptPath}'.");

                    byte[] bytes = _assembler.Assemble(asm);
                    if (!WriteProcessMemory(mem.Handle, allocAddr, bytes, (uint)bytes.Length, out int written) || written != bytes.Length)
                        throw new InvalidOperationException("Falha ao escrever código ASM na memória do processo alvo.");

                    IntPtr thread = CreateRemoteThread(mem.Handle, IntPtr.Zero, 0, allocAddr, IntPtr.Zero, 0, IntPtr.Zero);
                    if (thread != IntPtr.Zero)
                    {
                        try
                        {
                            WaitForSingleObject(thread, ThreadWaitTimeoutMs);
                        }
                        finally
                        {
                            CloseHandle(thread);
                        }
                    }
                }
                finally
                {
                    VirtualFreeEx(mem.Handle, alloc, 0, MemRelease);
                }
            }
        }

        public void Dispose() => _assembler.Dispose();
    }
}