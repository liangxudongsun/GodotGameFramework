using Godot;
using System;
namespace GameFramework.Resource
{
    [Serializable]
    public class PackVersionList
    {
        public string Version { get; set; }
        public Pack[] Packs { get; set; }
        public PackVersionList(string version, Pack[] packs)
        {
            Version = version;
            Packs = packs;
        }
    }
    [Serializable]
    public struct Pack
    {
        /// <summary>
        /// 资源包名称
        /// </summary> 
        public string Name { get; set; }
        /// <summary>
        /// 资源包大小
        /// </summary>
        public int Size { get; set; }
        /// <summary>
        /// 资源包哈希
        /// </summary>
        public int Hash { get; set; }
        /// <summary>
        /// 资源包下载地址
        /// </summary>
        public string Url { get; set; }
        public Pack(string name, int size, int hash, string url)
        {
            Name = name;
            Size = size;
            Hash = hash;
            Url = url;
        }
    }
}
