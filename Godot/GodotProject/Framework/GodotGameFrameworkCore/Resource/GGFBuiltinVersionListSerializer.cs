//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Resource;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GodotGameFramework
{
    /// <summary>
    /// GGF 内置版本资源列表序列化器。
    ///
    /// 从 UGF 的 BuiltinVersionListSerializer 适配而来。
    /// 提供 Package 模式版本列表的序列化和反序列化回调。
    ///
    /// 版本列表格式说明：
    /// - 文件头：3 字节标识 'G','F','P' + 1 字节版本号
    /// - 版本 0 (V0)：旧格式，资源内嵌 Asset 名称
    /// - 版本 1 (V1)：独立 Asset/Resource 数组，无 FileSystem 节
    /// - 版本 2 (V2)：完整格式，包含 Assets、Resources、FileSystems、ResourceGroups
    ///
    /// GGF 的版本列表构建工具（GGFResourceBuilder）始终生成 V2 格式。
    /// 注册 V0/V1 反序列化回调仅为兼容旧版版本列表文件。
    ///
    /// 使用方式：
    /// <code>
    /// // 在 ResourceComponent._Ready() 中注册回调
    /// GGFBuiltinVersionListSerializer.RegisterPackageDeserializeCallbacks(
    ///     m_ResourceManager.PackageVersionListSerializer);
    ///
    /// // 编辑器模式下还需注册序列化回调
    /// GGFBuiltinVersionListSerializer.RegisterPackageSerializeCallbacks(
    ///     m_ResourceManager.PackageVersionListSerializer);
    /// </code>
    /// </summary>
    public static class GGFBuiltinVersionListSerializer
    {
        private const string DefaultExtension = "dat";
        private const int CachedHashBytesLength = 4;
        private static readonly byte[] s_CachedHashBytes = new byte[CachedHashBytesLength];

        /// <summary>
        /// 注册 Package 模式版本列表的反序列化回调。
        ///
        /// 必须在调用 ResourceManager.InitResources() 之前注册。
        /// </summary>
        /// <param name="serializer">Package 版本列表序列化器实例。</param>
        public static void RegisterPackageDeserializeCallbacks(PackageVersionListSerializer serializer)
        {
            if (serializer == null)
            {
                throw new GameFrameworkException("Serializer is invalid.");
            }

            serializer.RegisterDeserializeCallback(0, PackageVersionListDeserializeCallback_V0);
            serializer.RegisterDeserializeCallback(1, PackageVersionListDeserializeCallback_V1);
            serializer.RegisterDeserializeCallback(2, PackageVersionListDeserializeCallback_V2);
        }

        /// <summary>
        /// 注册 Package 模式版本列表的序列化回调。
        ///
        /// 仅在编辑器模式下需要（构建工具生成版本列表时使用）。
        /// </summary>
        /// <param name="serializer">Package 版本列表序列化器实例。</param>
        public static void RegisterPackageSerializeCallbacks(PackageVersionListSerializer serializer)
        {
            if (serializer == null)
            {
                throw new GameFrameworkException("Serializer is invalid.");
            }

            serializer.RegisterSerializeCallback(2, PackageVersionListSerializeCallback_V2);
        }

        // ================================================================
        //  反序列化回调
        // ================================================================

        /// <summary>
        /// 反序列化单机模式版本资源列表（版本 0）回调函数。
        ///
        /// V0 格式特点：Asset 名称内嵌在 Resource 中，需要排序后二分查找来建立索引。
        /// 这是最旧的格式，仅为向后兼容而保留。
        /// </summary>
        /// <param name="stream">指定流。</param>
        /// <returns>反序列化的单机模式版本资源列表。</returns>
        public static PackageVersionList PackageVersionListDeserializeCallback_V0(Stream stream)
        {
            using (BinaryReader binaryReader = new BinaryReader(stream, Encoding.UTF8))
            {
                byte[] encryptBytes = binaryReader.ReadBytes(CachedHashBytesLength);
                string applicableGameVersion = binaryReader.ReadEncryptedString(encryptBytes);
                int internalResourceVersion = binaryReader.ReadInt32();
                int assetCount = binaryReader.ReadInt32();
                PackageVersionList.Asset[] assets = assetCount > 0 ? new PackageVersionList.Asset[assetCount] : null;
                int resourceCount = binaryReader.ReadInt32();
                PackageVersionList.Resource[] resources = resourceCount > 0 ? new PackageVersionList.Resource[resourceCount] : null;
                string[][] resourceToAssetNames = new string[resourceCount][];
                List<KeyValuePair<string, string[]>> assetNameToDependencyAssetNames = new List<KeyValuePair<string, string[]>>(assetCount);
                for (int i = 0; i < resourceCount; i++)
                {
                    string name = binaryReader.ReadEncryptedString(encryptBytes);
                    string variant = binaryReader.ReadEncryptedString(encryptBytes);
                    byte loadType = binaryReader.ReadByte();
                    int length = binaryReader.ReadInt32();
                    int hashCode = binaryReader.ReadInt32();
                    Utility.Converter.GetBytes(hashCode, s_CachedHashBytes);

                    int assetNameCount = binaryReader.ReadInt32();
                    string[] assetNames = new string[assetNameCount];
                    for (int j = 0; j < assetNameCount; j++)
                    {
                        assetNames[j] = binaryReader.ReadEncryptedString(s_CachedHashBytes);
                        int dependencyAssetNameCount = binaryReader.ReadInt32();
                        string[] dependencyAssetNames = dependencyAssetNameCount > 0 ? new string[dependencyAssetNameCount] : null;
                        for (int k = 0; k < dependencyAssetNameCount; k++)
                        {
                            dependencyAssetNames[k] = binaryReader.ReadEncryptedString(s_CachedHashBytes);
                        }

                        assetNameToDependencyAssetNames.Add(new KeyValuePair<string, string[]>(assetNames[j], dependencyAssetNames));
                    }

                    resourceToAssetNames[i] = assetNames;
                    resources[i] = new PackageVersionList.Resource(name, variant, null, loadType, length, hashCode, assetNameCount > 0 ? new int[assetNameCount] : null);
                }

                assetNameToDependencyAssetNames.Sort(AssetNameToDependencyAssetNamesComparer);
                Array.Clear(s_CachedHashBytes, 0, CachedHashBytesLength);
                int index = 0;
                foreach (KeyValuePair<string, string[]> i in assetNameToDependencyAssetNames)
                {
                    if (i.Value != null)
                    {
                        int[] dependencyAssetIndexes = new int[i.Value.Length];
                        for (int j = 0; j < i.Value.Length; j++)
                        {
                            dependencyAssetIndexes[j] = GetAssetNameIndex(assetNameToDependencyAssetNames, i.Value[j]);
                        }

                        assets[index++] = new PackageVersionList.Asset(i.Key, dependencyAssetIndexes);
                    }
                    else
                    {
                        assets[index++] = new PackageVersionList.Asset(i.Key, null);
                    }
                }

                for (int i = 0; i < resources.Length; i++)
                {
                    int[] assetIndexes = resources[i].GetAssetIndexes();
                    for (int j = 0; j < assetIndexes.Length; j++)
                    {
                        assetIndexes[j] = GetAssetNameIndex(assetNameToDependencyAssetNames, resourceToAssetNames[i][j]);
                    }
                }

                int resourceGroupCount = binaryReader.ReadInt32();
                PackageVersionList.ResourceGroup[] resourceGroups = resourceGroupCount > 0 ? new PackageVersionList.ResourceGroup[resourceGroupCount] : null;
                for (int i = 0; i < resourceGroupCount; i++)
                {
                    string name = binaryReader.ReadEncryptedString(encryptBytes);
                    int resourceIndexCount = binaryReader.ReadInt32();
                    int[] resourceIndexes = resourceIndexCount > 0 ? new int[resourceIndexCount] : null;
                    for (int j = 0; j < resourceIndexCount; j++)
                    {
                        resourceIndexes[j] = binaryReader.ReadUInt16();
                    }

                    resourceGroups[i] = new PackageVersionList.ResourceGroup(name, resourceIndexes);
                }

                return new PackageVersionList(applicableGameVersion, internalResourceVersion, assets, resources, null, resourceGroups);
            }
        }

        /// <summary>
        /// 反序列化单机模式版本资源列表（版本 1）回调函数。
        ///
        /// V1 格式特点：Asset 和 Resource 分离，使用 7 位编码整数，无 FileSystem 节。
        /// </summary>
        /// <param name="stream">指定流。</param>
        /// <returns>反序列化的单机模式版本资源列表。</returns>
        public static PackageVersionList PackageVersionListDeserializeCallback_V1(Stream stream)
        {
            using (BinaryReader binaryReader = new BinaryReader(stream, Encoding.UTF8))
            {
                byte[] encryptBytes = binaryReader.ReadBytes(CachedHashBytesLength);
                string applicableGameVersion = binaryReader.ReadEncryptedString(encryptBytes);
                int internalResourceVersion = binaryReader.Read7BitEncodedInt32();
                int assetCount = binaryReader.Read7BitEncodedInt32();
                PackageVersionList.Asset[] assets = assetCount > 0 ? new PackageVersionList.Asset[assetCount] : null;
                for (int i = 0; i < assetCount; i++)
                {
                    string name = binaryReader.ReadEncryptedString(encryptBytes);
                    int dependencyAssetCount = binaryReader.Read7BitEncodedInt32();
                    int[] dependencyAssetIndexes = dependencyAssetCount > 0 ? new int[dependencyAssetCount] : null;
                    for (int j = 0; j < dependencyAssetCount; j++)
                    {
                        dependencyAssetIndexes[j] = binaryReader.Read7BitEncodedInt32();
                    }

                    assets[i] = new PackageVersionList.Asset(name, dependencyAssetIndexes);
                }

                int resourceCount = binaryReader.Read7BitEncodedInt32();
                PackageVersionList.Resource[] resources = resourceCount > 0 ? new PackageVersionList.Resource[resourceCount] : null;
                for (int i = 0; i < resourceCount; i++)
                {
                    string name = binaryReader.ReadEncryptedString(encryptBytes);
                    string variant = binaryReader.ReadEncryptedString(encryptBytes);
                    string extension = binaryReader.ReadEncryptedString(encryptBytes) ?? DefaultExtension;
                    byte loadType = binaryReader.ReadByte();
                    int length = binaryReader.Read7BitEncodedInt32();
                    int hashCode = binaryReader.ReadInt32();
                    int assetIndexCount = binaryReader.Read7BitEncodedInt32();
                    int[] assetIndexes = assetIndexCount > 0 ? new int[assetIndexCount] : null;
                    for (int j = 0; j < assetIndexCount; j++)
                    {
                        assetIndexes[j] = binaryReader.Read7BitEncodedInt32();
                    }

                    resources[i] = new PackageVersionList.Resource(name, variant, extension, loadType, length, hashCode, assetIndexes);
                }

                int resourceGroupCount = binaryReader.Read7BitEncodedInt32();
                PackageVersionList.ResourceGroup[] resourceGroups = resourceGroupCount > 0 ? new PackageVersionList.ResourceGroup[resourceGroupCount] : null;
                for (int i = 0; i < resourceGroupCount; i++)
                {
                    string name = binaryReader.ReadEncryptedString(encryptBytes);
                    int resourceIndexCount = binaryReader.Read7BitEncodedInt32();
                    int[] resourceIndexes = resourceIndexCount > 0 ? new int[resourceIndexCount] : null;
                    for (int j = 0; j < resourceIndexCount; j++)
                    {
                        resourceIndexes[j] = binaryReader.Read7BitEncodedInt32();
                    }

                    resourceGroups[i] = new PackageVersionList.ResourceGroup(name, resourceIndexes);
                }

                return new PackageVersionList(applicableGameVersion, internalResourceVersion, assets, resources, null, resourceGroups);
            }
        }

        /// <summary>
        /// 反序列化单机模式版本资源列表（版本 2）回调函数。
        ///
        /// V2 是当前使用的格式，包含完整的 Assets、Resources、FileSystems、ResourceGroups 四个数组。
        /// GGF 的 GGFResourceBuilder 始终生成此格式的版本列表。
        /// </summary>
        /// <param name="stream">指定流。</param>
        /// <returns>反序列化的单机模式版本资源列表。</returns>
        public static PackageVersionList PackageVersionListDeserializeCallback_V2(Stream stream)
        {
            using (BinaryReader binaryReader = new BinaryReader(stream, Encoding.UTF8))
            {
                byte[] encryptBytes = binaryReader.ReadBytes(CachedHashBytesLength);
                string applicableGameVersion = binaryReader.ReadEncryptedString(encryptBytes);
                int internalResourceVersion = binaryReader.Read7BitEncodedInt32();
                int assetCount = binaryReader.Read7BitEncodedInt32();
                PackageVersionList.Asset[] assets = assetCount > 0 ? new PackageVersionList.Asset[assetCount] : null;
                for (int i = 0; i < assetCount; i++)
                {
                    string name = binaryReader.ReadEncryptedString(encryptBytes);
                    int dependencyAssetCount = binaryReader.Read7BitEncodedInt32();
                    int[] dependencyAssetIndexes = dependencyAssetCount > 0 ? new int[dependencyAssetCount] : null;
                    for (int j = 0; j < dependencyAssetCount; j++)
                    {
                        dependencyAssetIndexes[j] = binaryReader.Read7BitEncodedInt32();
                    }

                    assets[i] = new PackageVersionList.Asset(name, dependencyAssetIndexes);
                }

                int resourceCount = binaryReader.Read7BitEncodedInt32();
                PackageVersionList.Resource[] resources = resourceCount > 0 ? new PackageVersionList.Resource[resourceCount] : null;
                for (int i = 0; i < resourceCount; i++)
                {
                    string name = binaryReader.ReadEncryptedString(encryptBytes);
                    string variant = binaryReader.ReadEncryptedString(encryptBytes);
                    string extension = binaryReader.ReadEncryptedString(encryptBytes) ?? DefaultExtension;
                    byte loadType = binaryReader.ReadByte();
                    int length = binaryReader.Read7BitEncodedInt32();
                    int hashCode = binaryReader.ReadInt32();
                    int assetIndexCount = binaryReader.Read7BitEncodedInt32();
                    int[] assetIndexes = assetIndexCount > 0 ? new int[assetIndexCount] : null;
                    for (int j = 0; j < assetIndexCount; j++)
                    {
                        assetIndexes[j] = binaryReader.Read7BitEncodedInt32();
                    }

                    resources[i] = new PackageVersionList.Resource(name, variant, extension, loadType, length, hashCode, assetIndexes);
                }

                int fileSystemCount = binaryReader.Read7BitEncodedInt32();
                PackageVersionList.FileSystem[] fileSystems = fileSystemCount > 0 ? new PackageVersionList.FileSystem[fileSystemCount] : null;
                for (int i = 0; i < fileSystemCount; i++)
                {
                    string name = binaryReader.ReadEncryptedString(encryptBytes);
                    int resourceIndexCount = binaryReader.Read7BitEncodedInt32();
                    int[] resourceIndexes = resourceIndexCount > 0 ? new int[resourceIndexCount] : null;
                    for (int j = 0; j < resourceIndexCount; j++)
                    {
                        resourceIndexes[j] = binaryReader.Read7BitEncodedInt32();
                    }

                    fileSystems[i] = new PackageVersionList.FileSystem(name, resourceIndexes);
                }

                int resourceGroupCount = binaryReader.Read7BitEncodedInt32();
                PackageVersionList.ResourceGroup[] resourceGroups = resourceGroupCount > 0 ? new PackageVersionList.ResourceGroup[resourceGroupCount] : null;
                for (int i = 0; i < resourceGroupCount; i++)
                {
                    string name = binaryReader.ReadEncryptedString(encryptBytes);
                    int resourceIndexCount = binaryReader.Read7BitEncodedInt32();
                    int[] resourceIndexes = resourceIndexCount > 0 ? new int[resourceIndexCount] : null;
                    for (int j = 0; j < resourceIndexCount; j++)
                    {
                        resourceIndexes[j] = binaryReader.Read7BitEncodedInt32();
                    }

                    resourceGroups[i] = new PackageVersionList.ResourceGroup(name, resourceIndexes);
                }

                return new PackageVersionList(applicableGameVersion, internalResourceVersion, assets, resources, fileSystems, resourceGroups);
            }
        }

        // ================================================================
        //  序列化回调
        // ================================================================

        /// <summary>
        /// 序列化单机模式版本资源列表（版本 2）回调函数。
        ///
        /// GGF 的 GGFResourceBuilder 使用此方法将版本列表写入 GameFrameworkVersion.dat。
        /// V2 格式包含完整的 Assets、Resources、FileSystems、ResourceGroups 四个数组。
        /// </summary>
        /// <param name="stream">目标流。</param>
        /// <param name="versionList">要序列化的单机模式版本资源列表。</param>
        /// <returns>是否序列化成功。</returns>
        public static bool PackageVersionListSerializeCallback_V2(Stream stream, PackageVersionList versionList)
        {
            if (!versionList.IsValid)
            {
                return false;
            }

            Utility.Random.GetRandomBytes(s_CachedHashBytes);
            using (BinaryWriter binaryWriter = new BinaryWriter(stream, Encoding.UTF8))
            {
                binaryWriter.Write(s_CachedHashBytes);
                binaryWriter.WriteEncryptedString(versionList.ApplicableGameVersion, s_CachedHashBytes);
                binaryWriter.Write7BitEncodedInt32(versionList.InternalResourceVersion);
                PackageVersionList.Asset[] assets = versionList.GetAssets();
                binaryWriter.Write7BitEncodedInt32(assets.Length);
                foreach (PackageVersionList.Asset asset in assets)
                {
                    binaryWriter.WriteEncryptedString(asset.Name, s_CachedHashBytes);
                    int[] dependencyAssetIndexes = asset.GetDependencyAssetIndexes();
                    binaryWriter.Write7BitEncodedInt32(dependencyAssetIndexes.Length);
                    foreach (int dependencyAssetIndex in dependencyAssetIndexes)
                    {
                        binaryWriter.Write7BitEncodedInt32(dependencyAssetIndex);
                    }
                }

                PackageVersionList.Resource[] resources = versionList.GetResources();
                binaryWriter.Write7BitEncodedInt32(resources.Length);
                foreach (PackageVersionList.Resource resource in resources)
                {
                    binaryWriter.WriteEncryptedString(resource.Name, s_CachedHashBytes);
                    binaryWriter.WriteEncryptedString(resource.Variant, s_CachedHashBytes);
                    binaryWriter.WriteEncryptedString(resource.Extension != DefaultExtension ? resource.Extension : null, s_CachedHashBytes);
                    binaryWriter.Write(resource.LoadType);
                    binaryWriter.Write7BitEncodedInt32(resource.Length);
                    binaryWriter.Write(resource.HashCode);
                    int[] assetIndexes = resource.GetAssetIndexes();
                    binaryWriter.Write7BitEncodedInt32(assetIndexes.Length);
                    foreach (int assetIndex in assetIndexes)
                    {
                        binaryWriter.Write7BitEncodedInt32(assetIndex);
                    }
                }

                PackageVersionList.FileSystem[] fileSystems = versionList.GetFileSystems();
                binaryWriter.Write7BitEncodedInt32(fileSystems.Length);
                foreach (PackageVersionList.FileSystem fileSystem in fileSystems)
                {
                    binaryWriter.WriteEncryptedString(fileSystem.Name, s_CachedHashBytes);
                    int[] resourceIndexes = fileSystem.GetResourceIndexes();
                    binaryWriter.Write7BitEncodedInt32(resourceIndexes.Length);
                    foreach (int resourceIndex in resourceIndexes)
                    {
                        binaryWriter.Write7BitEncodedInt32(resourceIndex);
                    }
                }

                PackageVersionList.ResourceGroup[] resourceGroups = versionList.GetResourceGroups();
                binaryWriter.Write7BitEncodedInt32(resourceGroups.Length);
                foreach (PackageVersionList.ResourceGroup resourceGroup in resourceGroups)
                {
                    binaryWriter.WriteEncryptedString(resourceGroup.Name, s_CachedHashBytes);
                    int[] resourceIndexes = resourceGroup.GetResourceIndexes();
                    binaryWriter.Write7BitEncodedInt32(resourceIndexes.Length);
                    foreach (int resourceIndex in resourceIndexes)
                    {
                        binaryWriter.Write7BitEncodedInt32(resourceIndex);
                    }
                }
            }

            Array.Clear(s_CachedHashBytes, 0, CachedHashBytesLength);
            return true;
        }

        // ================================================================
        //  辅助方法（V0 反序列化专用）
        // ================================================================

        private static int AssetNameToDependencyAssetNamesComparer(KeyValuePair<string, string[]> a, KeyValuePair<string, string[]> b)
        {
            return a.Key.CompareTo(b.Key);
        }

        private static int GetAssetNameIndex(List<KeyValuePair<string, string[]>> assetNameToDependencyAssetNames, string assetName)
        {
            return GetAssetNameIndexWithBinarySearch(assetNameToDependencyAssetNames, assetName, 0, assetNameToDependencyAssetNames.Count - 1);
        }

        private static int GetAssetNameIndexWithBinarySearch(List<KeyValuePair<string, string[]>> assetNameToDependencyAssetNames, string assetName, int leftIndex, int rightIndex)
        {
            if (leftIndex > rightIndex)
            {
                return -1;
            }

            int middleIndex = (leftIndex + rightIndex) / 2;
            if (assetNameToDependencyAssetNames[middleIndex].Key == assetName)
            {
                return middleIndex;
            }

            if (assetNameToDependencyAssetNames[middleIndex].Key.CompareTo(assetName) > 0)
            {
                return GetAssetNameIndexWithBinarySearch(assetNameToDependencyAssetNames, assetName, leftIndex, middleIndex - 1);
            }
            else
            {
                return GetAssetNameIndexWithBinarySearch(assetNameToDependencyAssetNames, assetName, middleIndex + 1, rightIndex);
            }
        }
    }
}
