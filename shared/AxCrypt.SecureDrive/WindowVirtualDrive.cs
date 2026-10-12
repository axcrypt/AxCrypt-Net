using AxCrypt.Core.Runtime;
using Fsp;
using Fsp.Interop;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Enumeration;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using static System.Net.WebRequestMethods;

namespace AxCrypt.SecureDrive
{
    internal class MemNode
    {
        public string Name;
        public bool IsDir;
        public byte[] Data = Array.Empty<byte>();
        public uint Attrs;
        public ulong CTime, ATime, MTime, ChangeTime;

        public ConcurrentDictionary<string, MemNode>? Kids;  // dirs only

        internal MemNode(string name, bool isDir)
        {
            Name = name;
            IsDir = isDir;
            Attrs = isDir
                ? (uint)System.IO.FileAttributes.Directory
                : (uint)System.IO.FileAttributes.Normal;
            ulong now = (ulong)DateTime.UtcNow.ToFileTimeUtc();
            CTime = ATime = MTime = ChangeTime = now;
            if (isDir)
                Kids = new ConcurrentDictionary<string, MemNode>(
                           StringComparer.OrdinalIgnoreCase);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  FileSystemBase implementation
    // ─────────────────────────────────────────────────────────────────────────
    public class WindowVirtualDrive : FileSystemBase, IInMemoryFileSystem
    {
        // ── Root of the in-memory tree ────────────────────────────────────────
        private readonly MemNode _root = new MemNode("", true);
        private readonly object _lock = new object();

        // ── Mount / unmount ───────────────────────────────────────────────────
        private FileSystemHost? _host;

        /// <summary>Drive letter the filesystem is mounted on, e.g. "Z:"</summary>
        public string MountPoint { get; private set; } = string.Empty;

        public string Storage
        {
            get;
            private set;
        } = string.Empty;

        /// <summary>
        /// True when WinFsp is installed on this machine.
        ///
        /// WinFsp registers under the 32-bit registry view even on x64, so the
        /// obvious Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WinFsp") reads the
        /// 64-bit view and always returns null. The base key must be opened with
        /// RegistryView.Registry32 — this is the same key winfsp.net itself reads
        /// to locate its native library, so if this fails Mount() would too.
        ///
        /// Reading it works from the MSIX package: the app declares runFullTrust,
        /// and MSIX virtualises registry writes, not reads.
        /// </summary>
        public bool IsInstalled
        {
            get
            {
                try
                {
                    using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                    using RegistryKey? key = baseKey.OpenSubKey(@"SOFTWARE\WinFsp");

                    if (key?.GetValue("InstallDir") is not string installDir || string.IsNullOrEmpty(installDir))
                    {
                        return false;
                    }

                    // The registry key can outlive an uninstall, so confirm the
                    // native library for this process architecture is really there.
                    string native = RuntimeInformation.ProcessArchitecture switch
                    {
                        Architecture.Arm64 => "winfsp-a64.dll",
                        Architecture.X86 => "winfsp-x86.dll",
                        _ => "winfsp-x64.dll",
                    };

                    // Fully qualified: this file has "using static System.Net.WebRequestMethods",
                    // which brings its own File into scope.
                    return System.IO.File.Exists(Path.Combine(installDir, "bin", native));
                }
                catch
                {
                    return false;
                }
            }
        }

        private static char FindFreeDriveLetter()
        {
            var used = DriveInfo.GetDrives()
                                .Select(d => d.Name[0])
                                .ToHashSet();

            for (char c = 'Z'; c >= 'D'; c--)
                if (!used.Contains(c))
                    return c;

            throw new InvalidOperationException("No free drive letters available.");
        }

        public void Mount()
        {
            // Idempotent: mounting is now tied to sign-in rather than process start,
            // so a second call would otherwise orphan the previous FileSystemHost
            // and leak its drive letter.
            if (_host != null)
            {
                return;
            }

            string driveLetter = $"{FindFreeDriveLetter()}:";
            this.Storage = driveLetter;
            _host = new FileSystemHost(this)
            {
                FileSystemName = "AxCrypt",
                SectorSize = 512,
                SectorsPerAllocationUnit = 1,
                MaxComponentLength = 255,
                CaseSensitiveSearch = false,
                CasePreservedNames = true,
                UnicodeOnDisk = true,
                PersistentAcls = false,
                VolumeCreationTime = (ulong)DateTime.UtcNow.ToFileTimeUtc(),
                VolumeSerialNumber = 0xC10DC10D,
            };

            // Mount() internally calls FspFileSystemStartDispatcher —
            // no separate dispatcher thread needed.
            int status = _host.Mount(driveLetter);
            if (status != 0)
                throw new InvalidOperationException(
                    $"WinFsp mount failed: NTSTATUS 0x{status:X8}");

            MountPoint = _host.MountPoint();
        }

        public void Unmount()
        {
            _host?.Unmount();
            _host?.Dispose();
            _host = null;

            // Drop the in-memory tree as well. Unmount happens on sign-out, and the
            // nodes hold decrypted file content — keeping them would carry one
            // user's plaintext into the next session.
            lock (_lock)
            {
                _root.Kids?.Clear();
            }

            Storage = string.Empty;
            MountPoint = string.Empty;
        }

        // ── Public helpers to push files into the virtual drive ───────────────

        public void WriteFile(string path, byte[] data)
        {
            lock (_lock)
            {
                var (parent, name) = Resolve(path, createDirs: true);
                if (parent == null) return;

                if (!parent.Kids!.TryGetValue(name, out var node))
                {
                    node = new MemNode(name, false);
                    parent.Kids[name] = node;
                }
                node.Data = data;
                node.MTime = (ulong)DateTime.UtcNow.ToFileTimeUtc();
            }
        }

        public void CreateDirectory(string path)
        {
            lock (_lock)
            {
                var (parent, name) = Resolve(path, createDirs: true);
                if (parent == null || parent.Kids!.ContainsKey(name)) return;
                parent.Kids[name] = new MemNode(name, true);
            }
        }

        // ── FileSystemBase overrides ──────────────────────────────────────────

        public override int GetVolumeInfo(out VolumeInfo volumeInfo)
        {
            volumeInfo = default;
            volumeInfo.TotalSize = 10UL * 1024 * 1024 * 1024;
            volumeInfo.FreeSize = 8UL * 1024 * 1024 * 1024;
            volumeInfo.SetVolumeLabel("AxCrypt");
            return STATUS_SUCCESS;
        }

        public override int GetSecurityByName(
            string fileName,
            out uint fileAttributes,
            ref byte[] securityDescriptor)
        {
            var node = Find(fileName);
            if (node == null)
            {
                fileAttributes = 0;
                securityDescriptor = null!;
                return STATUS_OBJECT_NAME_NOT_FOUND;
            }
            fileAttributes = node.Attrs;
            securityDescriptor = null; // DefaultSecurity();
            return STATUS_SUCCESS;
        }

        public override int Open(
            string fileName,
            uint createOptions,
            uint grantedAccess,
            out object fileNode,
            out object fileDesc,
            out Fsp.Interop.FileInfo fileInfo,
            out string normalizedName)
        {
            fileNode = null!;
            fileDesc = null!;
            fileInfo = default;
            normalizedName = fileName;

            var node = Find(fileName);
            if (node == null) return STATUS_OBJECT_NAME_NOT_FOUND;

            fileNode = fileDesc = node;
            Fill(node, ref fileInfo);
            return STATUS_SUCCESS;
        }

        public override int Create(
            string fileName,
            uint createOptions,
            uint grantedAccess,
            uint fileAttributes,
            byte[] securityDescriptor,
            ulong allocationSize,
            out object fileNode,
            out object fileDesc,
            out Fsp.Interop.FileInfo fileInfo,
            out string normalizedName)
        {
            fileNode = null!;
            fileDesc = null!;
            fileInfo = default;
            normalizedName = fileName;

            bool isDir = (createOptions & 0x00000001 /*FILE_DIRECTORY_FILE*/) != 0;

            lock (_lock)
            {
                var (parent, name) = Resolve(fileName, createDirs: false);
                if (parent == null) return STATUS_OBJECT_NAME_NOT_FOUND;

                var node = new MemNode(name, isDir);
                if (!parent.Kids!.TryAdd(name, node))
                    return STATUS_OBJECT_NAME_COLLISION;

                fileNode = fileDesc = node;
                Fill(node, ref fileInfo);
            }
            return STATUS_SUCCESS;
        }

        public override int GetFileInfo(
            object fileNode,
            object fileDesc,
            out Fsp.Interop.FileInfo fileInfo)
        {
            fileInfo = default;
            Fill((MemNode)fileNode, ref fileInfo);
            return STATUS_SUCCESS;
        }

        public override int Read(
            object fileNode,
            object fileDesc,
            IntPtr buffer,
            ulong offset,
            uint length,
            out uint bytesTransferred)
        {
            bytesTransferred = 0;
            var data = ((MemNode)fileNode).Data;

            if (offset >= (ulong)data.LongLength)
                return STATUS_END_OF_FILE;

            int toRead = (int)Math.Min((long)length, data.LongLength - (long)offset);
            Marshal.Copy(data, (int)offset, buffer, toRead);
            bytesTransferred = (uint)toRead;
            return STATUS_SUCCESS;
        }

        public override int Write(
            object fileNode,
            object fileDesc,
            IntPtr buffer,
            ulong offset,
            uint length,
            bool writeToEndOfFile,
            bool constrainedIo,
            out uint bytesTransferred,
            out Fsp.Interop.FileInfo fileInfo)
        {
            bytesTransferred = 0;
            fileInfo = default;
            var node = (MemNode)fileNode;

            lock (_lock)
            {
                long off = writeToEndOfFile ? node.Data.LongLength : (long)offset;
                long newSize = Math.Max(node.Data.LongLength, off + (long)length);

                if (newSize > node.Data.LongLength)
                    Array.Resize(ref node.Data, (int)newSize);

                Marshal.Copy(buffer, node.Data, (int)off, (int)length);
                node.MTime = node.ChangeTime = (ulong)DateTime.UtcNow.ToFileTimeUtc();
                bytesTransferred = length;
                Fill(node, ref fileInfo);
            }
            return STATUS_SUCCESS;
        }

        public override int Flush(
            object fileNode,
            object fileDesc,
            out Fsp.Interop.FileInfo fileInfo)
        {
            fileInfo = default;
            if (fileNode != null) Fill((MemNode)fileNode, ref fileInfo);
            return STATUS_SUCCESS;
        }

        public override int SetBasicInfo(
            object fileNode,
            object fileDesc,
            uint fileAttributes,
            ulong creationTime,
            ulong lastAccessTime,
            ulong lastWriteTime,
            ulong changeTime,
            out Fsp.Interop.FileInfo fileInfo)
        {
            var node = (MemNode)fileNode;
            lock (_lock)
            {
                if (fileAttributes != unchecked((uint)-1) && fileAttributes != 0)
                    node.Attrs = fileAttributes;
                if (creationTime != 0) node.CTime = creationTime;
                if (lastAccessTime != 0) node.ATime = lastAccessTime;
                if (lastWriteTime != 0) node.MTime = lastWriteTime;
                if (changeTime != 0) node.ChangeTime = changeTime;
            }
            fileInfo = default;
            Fill(node, ref fileInfo);
            return STATUS_SUCCESS;
        }

        public override int SetFileSize(
            object fileNode,
            object fileDesc,
            ulong newSize,
            bool setAllocationSize,
            out Fsp.Interop.FileInfo fileInfo)
        {
            fileInfo = default;
            var node = (MemNode)fileNode;
            if (!setAllocationSize)
                Array.Resize(ref node.Data, (int)newSize);
            Fill(node, ref fileInfo);
            return STATUS_SUCCESS;
        }

        public override int CanDelete(
            object fileNode,
            object fileDesc,
            string fileName)
        {
            var node = (MemNode)fileNode;
            if (node.IsDir && node.Kids != null && !node.Kids.IsEmpty)
                return STATUS_DIRECTORY_NOT_EMPTY;
            return STATUS_SUCCESS;
        }

        public override void Cleanup(
            object fileNode,
            object fileDesc,
            string fileName,
            uint flags)
        {
            const uint FspCleanupDelete = 0x01;
            if ((flags & FspCleanupDelete) == 0 || fileName == null) return;

            lock (_lock)
            {
                var (parent, name) = Resolve(fileName, createDirs: false);
                parent?.Kids?.TryRemove(name, out _);
            }
        }

        public override int Rename(
            object fileNode,
            object fileDesc,
            string fileName,
            string newFileName,
            bool replaceIfExists)
        {
            lock (_lock)
            {
                var (srcParent, srcName) = Resolve(fileName, createDirs: false);
                var (dstParent, dstName) = Resolve(newFileName, createDirs: false);

                if (srcParent == null || dstParent == null)
                    return STATUS_OBJECT_NAME_NOT_FOUND;

                if (dstParent.Kids!.ContainsKey(dstName) && !replaceIfExists)
                    return STATUS_OBJECT_NAME_COLLISION;

                if (!srcParent.Kids!.TryRemove(srcName, out var node))
                    return STATUS_OBJECT_NAME_NOT_FOUND;

                node.Name = dstName;
                dstParent.Kids[dstName] = node;
            }
            return STATUS_SUCCESS;
        }

        public override bool ReadDirectoryEntry(
            object fileNode,
            object fileDesc,
            string pattern,
            string marker,
            ref object context,
            out string fileName,
            out Fsp.Interop.FileInfo fileInfo)
        {
            fileName = null!;
            fileInfo = default;

            var dir = (MemNode)fileNode;
            if (dir.Kids == null || dir.Kids.Count == 0)
                return false;

            // 1. Build stable snapshot (IMPORTANT: deterministic order)
            var list = dir.Kids.Values
                .OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 2. Resolve starting index from marker
            int index = 0;

            if (marker is string m && !string.IsNullOrEmpty(m))
            {
                int found = list.FindIndex(x =>
                    string.Equals(x.Name, m, StringComparison.OrdinalIgnoreCase));

                index = (found >= 0) ? found + 1 : 0;
            }
            else if (context is int ctxIndex)
            {
                index = ctxIndex;
            }

            // 3. End condition
            if (index >= list.Count)
                return false;

            var child = list[index];

            // 4. Fill result
            fileName = child.Name;
            Fill(child, ref fileInfo);

            // 5. Save next state
            context = index + 1;

            return true;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private MemNode? Find(string path)
        {
            if (path == "\\") return _root;
            var parts = path.TrimStart('\\').Split('\\');
            var current = _root;
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                if (current.Kids == null || !current.Kids.TryGetValue(part, out current!))
                    return null;
            }
            return current;
        }

        /// <summary>
        /// Returns the parent node and the last segment of <paramref name="path"/>.
        /// If <paramref name="createDirs"/> is true, missing intermediate dirs are created.
        /// </summary>
        private (MemNode? parent, string name) Resolve(string path, bool createDirs)
        {
            var parts = path.TrimStart('\\').Split('\\');
            if (parts.Length == 0) return (null, string.Empty);

            var current = _root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var seg = parts[i];
                if (string.IsNullOrEmpty(seg)) continue;
                if (current.Kids == null) return (null, string.Empty);

                if (!current.Kids.TryGetValue(seg, out var next))
                {
                    if (!createDirs) return (null, string.Empty);
                    next = new MemNode(seg, true);
                    current.Kids[seg] = next;
                }
                current = next;
            }
            return (current, parts[^1]);
        }

        private static void Fill(MemNode node, ref Fsp.Interop.FileInfo fi)
        {
            fi.FileAttributes = node.Attrs;
            fi.FileSize = node.IsDir ? 0UL : (ulong)node.Data.LongLength;
            fi.AllocationSize = ((fi.FileSize + 511) / 512) * 512;
            fi.CreationTime = node.CTime;
            fi.LastAccessTime = node.ATime;
            fi.LastWriteTime = node.MTime;
            fi.ChangeTime = node.ChangeTime;
            fi.IndexNumber = 0;
            fi.HardLinks = 0;
        }

        private static byte[] DefaultSecurity()
        {
            var sd = new RawSecurityDescriptor(
                ControlFlags.DiscretionaryAclPresent |
                ControlFlags.OwnerDefaulted |
                ControlFlags.GroupDefaulted,
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                null,
                new RawAcl(GenericAcl.AclRevision, 1)
            );

            var buf = new byte[sd.BinaryLength];
            sd.GetBinaryForm(buf, 0);
            return buf;
        }

        // ── NTSTATUS constants ────────────────────────────────────────────────
        private const int STATUS_SUCCESS = 0x00000000;
        private const int STATUS_OBJECT_NAME_NOT_FOUND = unchecked((int)0xC0000034);
        private const int STATUS_OBJECT_NAME_COLLISION = unchecked((int)0xC0000035);
        private const int STATUS_END_OF_FILE = unchecked((int)0xC0000011);
        private const int STATUS_DIRECTORY_NOT_EMPTY = unchecked((int)0xC0000101);
    }
}
