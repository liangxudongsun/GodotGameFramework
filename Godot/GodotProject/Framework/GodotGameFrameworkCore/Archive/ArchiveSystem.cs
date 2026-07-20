using Godot;
using GodotGameFramework;
using GodotGameFramework.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GodotGameFrameworkCore.Archive;
/// <summary>
/// 存档目录
/// </summary> 
public class ArchiveCatalogue
{
    public long UnitId; // 单位ID
}
/// <summary>
/// 存档数据
/// </summary>
public class ArchiveData
{
    public long UnitId; // 单位ID
}
//一个简单的示例存储架构
public sealed class ArchiveSystem<T, U> where T : ArchiveCatalogue, new() where U : ArchiveData, new()
{
    const string ArchivePath = "GameData";
    public List<T> Catalogues { get; private set; } = new();
    public T CurrentCatalogue { get; private set; }
    public U CurrentData { get; private set; }
    public async void SaveAsync()
    {
        var catalogue = new T();
        catalogue.UnitId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Catalogues.Add(catalogue);
        var data = new U();
        data.UnitId = catalogue.UnitId;
        CurrentCatalogue = catalogue;
        CurrentData = data;
        await EasySave.SaveInUserAsync(Catalogues, $"{ArchivePath}/Catalogue.sav");
        await EasySave.SaveInUserAsync(data, $"{ArchivePath}/Data/{catalogue.UnitId}.sav");
        Log.Info("[ArchiveSystem]存档数据成功，单位ID{0}", catalogue.UnitId);
    }
    public async void SaveAsync(long unitId)
    {
        if (!Catalogues.Exists(x => x.UnitId == unitId))
        {
            Log.Error("[ArchiveSystem]存档目录中不存在该单位ID{0}", unitId);
        }
        else
        {
            CurrentCatalogue = Catalogues.Find(x => x.UnitId == unitId);
            CurrentData = await EasySave.LoadFromUserAsync<U>($"{ArchivePath}/{CurrentCatalogue.UnitId}.sav");
            if (CurrentData == null)
            {
                Log.Error("[ArchiveSystem]存档数据中不存在该单位ID{0}", unitId);
            }
            else
            {
                Log.Info("[ArchiveSystem]加载存档数据成功，单位ID{0}", unitId);
            }
        }
    }
    public async void LoadAsync()
    {
        var catalogues = await EasySave.LoadFromUserAsync<List<T>>($"{ArchivePath}/Catalogue.sav");
        if (catalogues == null)
        {
            SaveAsync();
        }
        else
        {
            Catalogues = catalogues;
            CurrentCatalogue = Catalogues[^1];
            CurrentData = await EasySave.LoadFromUserAsync<U>($"{ArchivePath}/{CurrentCatalogue.UnitId}.sav");
            Log.Info("[ArchiveSystem]加载存档数据成功，单位ID{0}", CurrentCatalogue.UnitId);
        }
    }

    public async void Delete(long unitId)
    {
        if (!Catalogues.Exists(x => x.UnitId == unitId))
        {
            Log.Error("[ArchiveSystem]存档目录中不存在该单位ID{0}", unitId);
        }
        else
        {
            Catalogues.Remove(Catalogues.Find(x => x.UnitId == unitId));
            await EasySave.DeleteInUserAsync($"{ArchivePath}/Data/{unitId}.sav");
            await EasySave.SaveInUserAsync(Catalogues, $"{ArchivePath}/Catalogue.sav");
            Log.Info("[ArchiveSystem]删除存档目录成功，单位ID{0}", unitId);
            Log.Info("[ArchiveSystem]删除存档数据成功，单位ID{0}", unitId);
        }
    }
}
