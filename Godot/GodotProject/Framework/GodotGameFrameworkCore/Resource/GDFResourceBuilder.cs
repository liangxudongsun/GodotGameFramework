//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Resource;
using Godot;
using System;
using System.Collections.Generic;
using SystemIO = System.IO;

namespace GodotGameFramework
{
    /// <summary>
    /// 资源版本列表构建工具。扫描 res:// 目录生成 GameFrameworkVersion.dat（V2 格式）。
    /// 排除：.godot/、.import、.uid、.cs、.gd、.meta、.csproj、.sln、.dll、.asmdef、隐藏文件。
    /// 编辑器模式下由 ResourceComponent 自动调用。
    /// </summary>
    public static class GDFResourceBuilder
    {
        private static readonly HashSet<string> ExcludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".godot",
        };

        private static readonly HashSet<string> ExcludedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".import", ".uid", ".cs", ".gd", ".meta", ".csproj", ".sln", ".dll", ".asmdef", ".tmp", ".log",
        };

        /// <summary>扫描项目资源目录，生成 PackageVersionList（V2 格式）二进制文件。</summary>
        public static bool BuildVersionList(string readOnlyPath, string outputPath,
            string gameVersion, int resourceVersion)
        {
            if (string.IsNullOrEmpty(readOnlyPath)) throw new GameFrameworkException("Read-only path is invalid.");
            if (string.IsNullOrEmpty(outputPath)) throw new GameFrameworkException("Output path is invalid.");

            List<string> files = new List<string>();
            ScanDirectory(readOnlyPath, files);
            if (files.Count == 0) Log.Warning("No resource files found in '{0}'.", readOnlyPath);
            files.Sort(StringComparer.Ordinal);

            PackageVersionList.Asset[] assets = new PackageVersionList.Asset[files.Count];
            PackageVersionList.Resource[] resources = new PackageVersionList.Resource[files.Count];

            for (int i = 0; i < files.Count; i++)
            {
                string fullPath = files[i];
                assets[i] = new PackageVersionList.Asset(fullPath, new int[0]);

                string relativePath = fullPath.Substring(readOnlyPath.Length);
                string extension = SystemIO.Path.GetExtension(relativePath).TrimStart('.');
                string name = SystemIO.Path.Combine(
                    SystemIO.Path.GetDirectoryName(relativePath) ?? "",
                    SystemIO.Path.GetFileNameWithoutExtension(relativePath)
                ).Replace('\\', '/');
                if (name.StartsWith("./")) name = name.Substring(2);

                int length = GetFileLength(fullPath);
                int hashCode = GetFileHashCode(fullPath);
                resources[i] = new PackageVersionList.Resource(name, null, extension, 0, length, hashCode, new int[] { i });
            }

            PackageVersionList versionList = new PackageVersionList(
                gameVersion, resourceVersion, assets, resources,
                new PackageVersionList.FileSystem[0],
                new PackageVersionList.ResourceGroup[0]);

            return SerializeVersionList(outputPath, versionList);
        }

        private static void ScanDirectory(string directoryPath, List<string> result)
        {
            using var dir = DirAccess.Open(directoryPath);
            if (dir == null) { Log.Warning("Can not open directory '{0}'.", directoryPath); return; }

            dir.ListDirBegin();
            string currentFile;
            while ((currentFile = dir.GetNext()) != string.Empty)
            {
                if (currentFile == "." || currentFile == "..") continue;
                if (currentFile.StartsWith(".")) continue;

                string fullPath = directoryPath + currentFile;
                if (dir.CurrentIsDir())
                {
                    if (ExcludedDirectories.Contains(currentFile)) continue;
                    ScanDirectory(fullPath + "/", result);
                }
                else
                {
                    string ext = SystemIO.Path.GetExtension(currentFile);
                    if (!string.IsNullOrEmpty(ext) && ExcludedExtensions.Contains(ext)) continue;
                    if (currentFile == VersionListFileName) continue;
                    result.Add(fullPath);
                }
            }
            dir.ListDirEnd();
        }

        private const string VersionListFileName = "GameFrameworkVersion.dat";

        private static int GetFileLength(string filePath)
        {
            if (!FileAccess.FileExists(filePath)) return 0;
            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            return file != null ? (int)file.GetLength() : 0;
        }

        private static int GetFileHashCode(string filePath)
        {
            if (!FileAccess.FileExists(filePath)) return 0;
            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            if (file == null) return 0;
            byte[] bytes = file.GetBuffer((long)file.GetLength());
            return Utility.Verifier.GetCrc32(bytes);
        }

        /// <summary>序列化 V2 格式并写入文件。内容未变化时跳过写入。</summary>
        private static bool SerializeVersionList(string outputPath, PackageVersionList versionList)
        {
            try
            {
                int lastSlash = outputPath.LastIndexOf('/');
                if (lastSlash > 0)
                {
                    string outputDir = outputPath.Substring(0, lastSlash);
                    if (outputDir != "res:" && outputDir != "res:/" && outputDir != "res://")
                        EnsureDirectoryExists(outputDir);
                }

                PackageVersionListSerializer serializer = new PackageVersionListSerializer();
                GDFBuiltinVersionListSerializer.RegisterPackageSerializeCallbacks(serializer);

                using SystemIO.MemoryStream memoryStream = new SystemIO.MemoryStream();
                serializer.Serialize(memoryStream, versionList);
                byte[] bytes = memoryStream.ToArray();

                // 内容未变化则跳过写入，避免触发不必要的资源重载
                if (FileAccess.FileExists(outputPath))
                {
                    using var existingFile = FileAccess.Open(outputPath, FileAccess.ModeFlags.Read);
                    if (existingFile != null)
                    {
                        long existingLength = (long)existingFile.GetLength();
                        byte[] existingBytes = existingFile.GetBuffer(existingLength);
                        if (existingBytes.Length == bytes.Length)
                        {
                            bool identical = true;
                            for (int i = 0; i < bytes.Length; i++) { if (bytes[i] != existingBytes[i]) { identical = false; break; } }
                            if (identical) { Log.Info("Version list '{0}' is up-to-date.", outputPath); return true; }
                        }
                    }
                }

                using var outputFile = FileAccess.Open(outputPath, FileAccess.ModeFlags.Write);
                if (outputFile == null) { Log.Error("Can not create version list file '{0}'.", outputPath); return false; }
                outputFile.StoreBuffer(bytes);
                Log.Info("Version list '{0}' generated. Assets: {1}, Resources: {2}",
                    outputPath, versionList.GetAssets().Length, versionList.GetResources().Length);
                return true;
            }
            catch (Exception e) { Log.Error("Serialize version list exception: {0}", e.Message); return false; }
        }

        private static void EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath)) return;
            if (DirAccess.DirExistsAbsolute(directoryPath)) return;
            if (directoryPath.StartsWith("res://"))
            {
                string relativePath = directoryPath.Substring(6);
                if (string.IsNullOrEmpty(relativePath)) return;
                using var dir = DirAccess.Open("res://");
                dir?.MakeDirRecursive(relativePath);
            }
            else
            {
                using var dir = DirAccess.Open(directoryPath);
                dir?.MakeDirRecursive("");
            }
        }
    }
}
