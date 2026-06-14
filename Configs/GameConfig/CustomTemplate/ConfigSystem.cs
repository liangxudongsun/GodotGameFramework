using System;
using Luban;
using GameConfig;
using Godot;
using GodotGameFramework.Runtime;


/// <summary>
/// 配置加载器。
/// </summary>
public class ConfigSystem
{
    private static ConfigSystem _instance;

    public static ConfigSystem Instance => _instance ??= new ConfigSystem();

    private bool _init = false;

    private Tables _tables;

    public Tables Tables
    {
        get
        {
            if (!_init)
            {
                Load();
            }

            return _tables;
        }
    }

    private ResourceComponent _resourceModule;

    /// <summary>
    /// 加载配置。
    /// </summary>
    public void Load()
    {
        _tables = new Tables(LoadByteBuf);
        _init = true;
    }

    /// <summary>
    /// 加载二进制配置。
    /// </summary>
    /// <param name="file">FileName</param>
    /// <returns>ByteBuf</returns>
    private ByteBuf LoadByteBuf(string file)
    {
        if (_resourceModule == null)
        {
            _resourceModule = GameEntry.GetComponent<ResourceComponent>();
        }

        byte[] bytes = _resourceModule.LoadBinaryFromFileSystem($"res://DataTables/{file}");
        if (bytes == null || bytes.Length == 0)
        {
            throw new Exception($"Failed to load config file: res://DataTables/{file}");
        }
        return new ByteBuf(bytes);
    }
}