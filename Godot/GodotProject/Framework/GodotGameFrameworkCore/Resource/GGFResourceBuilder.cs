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
    /// GGF 资源版本列表构建工具。
    ///
    /// 负责扫描项目资源目录（res://），生成 PackageVersionList 二进制文件（GameFrameworkVersion.dat）。
    /// 该文件是核心框架 ResourceManager 管道的入口，包含所有资源的索引信息。
    ///
    /// 工作原理：
    /// 1. 递归扫描 res:// 目录下的所有资源文件
    /// 2. 为每个文件创建 1:1 的 Asset + Resource 映射
    ///    - Asset.Name = 完整 Godot 路径（如 "res://Scenes/Hero.tscn"），用于 LoadAsset 查找
    ///    - Resource.Name = 相对路径去掉扩展名（如 "Scenes/Hero"），用于文件定位
    /// 3. 使用 V2 格式序列化版本列表
    ///
    /// 使用场景：
    /// - 编辑器模式下：由 ResourceComponent._Ready() 自动调用，保持版本列表与项目同步
    /// - 导出后：GameFrameworkVersion.dat 已包含在 PCK 中，无需再次构建
    /// - 构建工具：可手动调用生成版本列表
    ///
    /// 扫描排除规则：
    /// - 目录：.godot/
    /// - 扩展名：.import, .uid, .cs, .gd, .meta, .csproj, .sln, .dll, .asmdef
    /// - 以 . 开头的文件/目录（隐藏文件）
    /// </summary>
    public static class GGFResourceBuilder
    {
        /// <summary>
        /// 需要排除的目录名称集合。
        /// 这些目录下的文件不会被纳入版本列表。
        /// </summary>
        private static readonly HashSet<string> ExcludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".godot",
        };

        /// <summary>
        /// 需要排除的文件扩展名集合。
        /// 这些扩展名的文件不会被纳入版本列表。
        /// </summary>
        private static readonly HashSet<string> ExcludedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".import",
            ".uid",
            ".cs",
            ".gd",
            ".meta",
            ".csproj",
            ".sln",
            ".dll",
            ".asmdef",
            ".tmp",
            ".log",
        };

        /// <summary>
        /// 版本列表文件名。
        /// 与核心框架 ResourceManager 中的 RemoteVersionListFileName 保持一致。
        /// </summary>
        private const string VersionListFileName = "GameFrameworkVersion.dat";

        /// <summary>
        /// 扫描项目资源目录，生成 PackageVersionList 二进制文件。
        ///
        /// 在编辑器模式下由 ResourceComponent 自动调用。
        /// 生成的版本列表采用 V2 格式（当前最新版本）。
        /// </summary>
        /// <param name="readOnlyPath">只读资源路径（通常为 "res://"）。</param>
        /// <param name="outputPath">版本列表输出文件路径。</param>
        /// <param name="gameVersion">游戏版本号。</param>
        /// <param name="resourceVersion">内部资源版本号。</param>
        /// <returns>是否构建成功。</returns>
        public static bool BuildVersionList(string readOnlyPath, string outputPath,
            string gameVersion, int resourceVersion)
        {
            if (string.IsNullOrEmpty(readOnlyPath))
            {
                throw new GameFrameworkException("Read-only path is invalid.");
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                throw new GameFrameworkException("Output path is invalid.");
            }

            // 1. 递归扫描资源文件
            List<string> files = new List<string>();
            ScanDirectory(readOnlyPath, files);

            if (files.Count == 0)
            {
                Log.Warning("No resource files found in '{0}'.", readOnlyPath);
                // 仍然生成空版本列表，避免后续初始化失败
            }

            // 2. 排序文件列表（确保版本列表的确定性）
            files.Sort(StringComparer.Ordinal);

            // 3. 构建 Asset[] + Resource[] 数组
            PackageVersionList.Asset[] assets = new PackageVersionList.Asset[files.Count];
            PackageVersionList.Resource[] resources = new PackageVersionList.Resource[files.Count];

            for (int i = 0; i < files.Count; i++)
            {
                string fullPath = files[i]; // e.g., "res://Scenes/Hero.tscn"

                // Asset: Name = 完整 Godot 路径，无依赖（Godot 引擎内部处理资源依赖）
                assets[i] = new PackageVersionList.Asset(fullPath, new int[0]);

                // Resource: Name = 去掉 readOnlyPath 前缀和扩展名
                // Extension = 文件扩展名
                string relativePath = fullPath.Substring(readOnlyPath.Length); // e.g., "Scenes/Hero.tscn"
                string extension = SystemIO.Path.GetExtension(relativePath).TrimStart('.'); // e.g., "tscn"
                string name = SystemIO.Path.Combine(
                    SystemIO.Path.GetDirectoryName(relativePath) ?? "",
                    SystemIO.Path.GetFileNameWithoutExtension(relativePath)
                ).Replace('\\', '/'); // e.g., "Scenes/Hero"

                // 去除开头的 ./ 或空路径
                if (name.StartsWith("./"))
                {
                    name = name.Substring(2);
                }

                // 获取文件大小和 CRC32 哈希值
                int length = GetFileLength(fullPath);
                int hashCode = GetFileHashCode(fullPath);

                // 1:1 映射：每个 Resource 只包含一个 Asset
                // LoadType = LoadFromFile (0)：Godot 使用文件方式加载
                resources[i] = new PackageVersionList.Resource(
                    name, null, extension, 0, length, hashCode, new int[] { i });
            }

            // 4. 构建版本列表（不含 FileSystem 和 ResourceGroup）
            PackageVersionList versionList = new PackageVersionList(
                gameVersion, resourceVersion, assets, resources,
                new PackageVersionList.FileSystem[0],
                new PackageVersionList.ResourceGroup[0]);

            // 5. 序列化并写入文件
            return SerializeVersionList(outputPath, versionList);
        }

        /// <summary>
        /// 递归扫描指定目录下的所有资源文件。
        ///
        /// 使用 Godot 的 DirAccess API 遍历目录树。
        /// 自动跳过排除列表中的目录和文件。
        /// </summary>
        /// <param name="directoryPath">要扫描的目录路径。</param>
        /// <param name="result">收集到的文件路径列表。</param>
        private static void ScanDirectory(string directoryPath, List<string> result)
        {
            using var dir = DirAccess.Open(directoryPath);
            if (dir == null)
            {
                Log.Warning("Can not open directory '{0}'.", directoryPath);
                return;
            }

            dir.ListDirBegin();

            string currentFile;
            while ((currentFile = dir.GetNext()) != string.Empty)
            {
                // 跳过当前目录和上级目录
                if (currentFile == "." || currentFile == "..")
                {
                    continue;
                }

                // 跳过隐藏文件/目录（以 . 开头）
                if (currentFile.StartsWith("."))
                {
                    continue;
                }

                string fullPath = directoryPath + currentFile;

                if (dir.CurrentIsDir())
                {
                    // 跳过排除的目录
                    if (ExcludedDirectories.Contains(currentFile))
                    {
                        continue;
                    }

                    // 递归扫描子目录
                    ScanDirectory(fullPath + "/", result);
                }
                else
                {
                    // 跳过排除的扩展名
                    string extension = SystemIO.Path.GetExtension(currentFile);
                    if (!string.IsNullOrEmpty(extension) && ExcludedExtensions.Contains(extension))
                    {
                        continue;
                    }

                    // 跳过版本列表文件本身
                    if (currentFile == VersionListFileName)
                    {
                        continue;
                    }

                    result.Add(fullPath);
                }
            }

            dir.ListDirEnd();
        }

        /// <summary>
        /// 获取文件大小（字节数）。
        /// </summary>
        /// <param name="filePath">文件路径。</param>
        /// <returns>文件大小。如果无法获取返回 0。</returns>
        private static int GetFileLength(string filePath)
        {
            if (!Godot.FileAccess.FileExists(filePath))
            {
                return 0;
            }

            using var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
            {
                return 0;
            }

            return (int)file.GetLength();
        }

        /// <summary>
        /// 计算文件的 CRC32 哈希值。
        ///
        /// 使用核心框架的 Utility.Verifier.GetCrc32 方法。
        /// </summary>
        /// <param name="filePath">文件路径。</param>
        /// <returns>CRC32 哈希值。如果无法计算返回 0。</returns>
        private static int GetFileHashCode(string filePath)
        {
            if (!Godot.FileAccess.FileExists(filePath))
            {
                return 0;
            }

            using var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
            {
                return 0;
            }

            long length = (long)file.GetLength();
            byte[] bytes = file.GetBuffer(length);
            return Utility.Verifier.GetCrc32(bytes);
        }

        /// <summary>
        /// 将版本列表序列化并写入文件。
        ///
        /// 使用 PackageVersionListSerializer 的 V2 格式序列化。
        /// 文件头为 3 字节标识 'G','F','P' + 1 字节版本号。
        /// </summary>
        /// <param name="outputPath">输出文件路径。</param>
        /// <param name="versionList">要序列化的版本列表。</param>
        /// <returns>是否序列化并写入成功。</returns>
        private static bool SerializeVersionList(string outputPath, PackageVersionList versionList)
        {
            try
            {
                // 确保输出目录存在（跳过 res:// 根目录，它始终存在）
                int lastSlash = outputPath.LastIndexOf('/');
                if (lastSlash > 0)
                {
                    string outputDir = outputPath.Substring(0, lastSlash);
                    // 跳过 "res:" / "res:/" / "res://" 等虚拟根路径
                    if (outputDir != "res:" && outputDir != "res:/" && outputDir != "res://")
                    {
                        EnsureDirectoryExists(outputDir);
                    }
                }

                // 创建序列化器并注册 V2 序列化回调
                PackageVersionListSerializer serializer = new PackageVersionListSerializer();
                GGFBuiltinVersionListSerializer.RegisterPackageSerializeCallbacks(serializer);

                // 序列化到内存流
                using SystemIO.MemoryStream memoryStream = new SystemIO.MemoryStream();
                serializer.Serialize(memoryStream, versionList);

                // 将序列化结果写入文件
                byte[] bytes = memoryStream.ToArray();

                // 检查是否需要覆盖已存在的文件
                if (Godot.FileAccess.FileExists(outputPath))
                {
                    // 读取现有文件内容，比较是否相同
                    using var existingFile = Godot.FileAccess.Open(outputPath, Godot.FileAccess.ModeFlags.Read);
                    if (existingFile != null)
                    {
                        long existingLength = (long)existingFile.GetLength();
                        byte[] existingBytes = existingFile.GetBuffer(existingLength);

                        // 如果内容完全相同，跳过写入（避免触发不必要的资源重载）
                        if (existingBytes.Length == bytes.Length)
                        {
                            bool identical = true;
                            for (int i = 0; i < bytes.Length; i++)
                            {
                                if (bytes[i] != existingBytes[i])
                                {
                                    identical = false;
                                    break;
                                }
                            }

                            if (identical)
                            {
                                Log.Info("Version list '{0}' is up-to-date, skip writing.", outputPath);
                                return true;
                            }
                        }
                    }
                }

                // 写入文件
                using var outputFile = Godot.FileAccess.Open(outputPath, Godot.FileAccess.ModeFlags.Write);
                if (outputFile == null)
                {
                    Log.Error("Can not create version list file '{0}', error: {1}.",
                        outputPath, Godot.FileAccess.GetOpenError());
                    return false;
                }

                outputFile.StoreBuffer(bytes);
                Log.Info("Version list '{0}' generated. Assets: {1}, Resources: {2}",
                    outputPath, versionList.GetAssets().Length, versionList.GetResources().Length);

                return true;
            }
            catch (Exception e)
            {
                Log.Error("Serialize version list exception: {0}", e.Message);
                return false;
            }
        }

        /// <summary>
        /// 确保目录存在，如果不存在则创建。
        /// </summary>
        /// <param name="directoryPath">目录路径。</param>
        private static void EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                return;
            }

            if (DirAccess.DirExistsAbsolute(directoryPath))
            {
                return;
            }

            // 对于 res:// 路径，需要使用 DirAccess 打开 res:// 后用相对路径创建
            if (directoryPath.StartsWith("res://"))
            {
                string relativePath = directoryPath.Substring(6);
                if (string.IsNullOrEmpty(relativePath))
                {
                    return;
                }

                using var dir = DirAccess.Open("res://");
                if (dir != null)
                {
                    dir.MakeDirRecursive(relativePath);
                }
            }
            else
            {
                // 非 res:// 路径直接创建
                using var dir = DirAccess.Open(directoryPath);
                if (dir != null)
                {
                    dir.MakeDirRecursive("");
                }
            }
        }
    }
}
