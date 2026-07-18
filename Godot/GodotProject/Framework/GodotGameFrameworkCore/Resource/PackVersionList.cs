using System;

namespace GameFramework.Resource
{
    /// <summary>
    /// 子包类型。Config最先加载，Resource和Script按顺序加载。
    /// </summary>
    public enum PackType : byte
    {
        /// <summary>资源包（场景、贴图、音频、字体等）</summary>
        Resource = 0,

        /// <summary>配置包（Luban 二进制、本地化文本）</summary>
        Config = 1,

        /// <summary>脚本包（GDScript 等解释型脚本）</summary>
        Script = 2,
    }

    /// <summary>
    /// 版本清单，描述一次热更包含的所有子包。
    /// </summary>
    [Serializable]
    public class PackVersionList
    {
        /// <summary>版本号，如 "2.3.0"</summary>
        public string Version { get; set; }

        /// <summary>所有子包列表</summary>
        public Pack[] Packs { get; set; }

        /// <summary>最低兼容的 App 版本号（低于此版本的 App 必须先去商店更新）</summary>
        public string MinAppVersion { get; set; }

        /// <summary>是否强制更新（不可跳过）</summary>
        public bool ForceUpdate { get; set; }

        /// <summary>JSON 反序列化用，请使用带参构造函数。</summary>
        public PackVersionList() { }

        public PackVersionList(string version, Pack[] packs)
        {
            Version = version;
            Packs = packs;
        }

        /// <summary>检查版本清单是否包含任何有效的包数据。</summary>
        public bool IsValid() =>
            !string.IsNullOrEmpty(Version) && Packs != null && Packs.Length > 0;
    }

    /// <summary>
    /// 单个子包的描述信息。
    /// </summary>
    [Serializable]
    public struct Pack
    {
        /// <summary>包名称（不含扩展名）</summary>
        public string Name { get; set; }

        /// <summary>包大小（字节）</summary>
        public long Size { get; set; }

        /// <summary>SHA256 哈希（hex 字符串，64 字符）</summary>
        public string Hash { get; set; }

        /// <summary>下载地址</summary>
        public string Url { get; set; }

        /// <summary>包类型</summary>
        public PackType Type { get; set; }

        public Pack(string name, long size, string hash, string url, PackType type = PackType.Resource)
        {
            Name = name;
            Size = size;
            Hash = hash;
            Url = url;
            Type = type;
        }

        /// <summary>检查此 Pack 是否有效（名称、大小、SHA256 均合法）。</summary>
        public readonly bool IsValid() =>
            !string.IsNullOrEmpty(Name) && Size > 0 &&
            !string.IsNullOrEmpty(Hash) && Hash.Length == 64;
    }
}
