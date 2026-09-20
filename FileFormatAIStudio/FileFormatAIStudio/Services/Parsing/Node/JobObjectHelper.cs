using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FileFormatAIStudio.Services.Parsing.Node
{
    /// <summary>
    /// Utility using Windows Job Objects to guarantee child processes (like node.exe)
    /// are automatically terminated when the host WinUI 3 process terminates.
    /// </summary>
    public static class JobObjectHelper
    {
        private static readonly IntPtr JobHandle;
        private static readonly bool IsSupported;

        static JobObjectHelper()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    JobHandle = CreateJobObject(IntPtr.Zero, null);
                    if (JobHandle != IntPtr.Zero)
                    {
                        var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                        {
                            LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                        };

                        var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                        {
                            BasicLimitInformation = info
                        };

                        int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                        IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
                        try
                        {
                            Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);
                            if (SetInformationJobObject(JobHandle, JobObjectExtendedLimitInformation, extendedInfoPtr, (uint)length))
                            {
                                IsSupported = true;
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(extendedInfoPtr);
                        }
                    }
                }
                catch
                {
                    IsSupported = false;
                }
            }
        }

        /// <summary>
        /// Associates a child process with the application's Job Object.
        /// </summary>
        public static void AssociateProcess(Process process)
        {
            if (IsSupported && JobHandle != IntPtr.Zero && process != null && !process.HasExited)
            {
                try
                {
                    AssignProcessToJobObject(JobHandle, process.Handle);
                }
                catch
                {
                    // Fallback gracefully if process handle cannot be assigned
                }
            }
        }

        #region Win32 Native APIs

        private const int JobObjectExtendedLimitInformation = 9;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInformationClass, IntPtr lpJobObjectInformation, uint cbJobObjectInformationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryLimit;
            public UIntPtr PeakJobMemoryLimit;
        }

        #endregion
    }
}

