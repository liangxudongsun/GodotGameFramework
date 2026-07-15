using Godot;
using System;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
namespace GodotGameFramework.Json;

public static class EasySave
{
    static readonly string user = ProjectSettings.GlobalizePath("user://");
    static readonly string project = ProjectSettings.GlobalizePath("res://");
    public static void Save<T>(T data, string fileName)
    {
        string json = JsonConvert.SerializeObject(data);
        StreamWriter writer = new StreamWriter(fileName);
        writer.Write(json);
        writer.Close();
    }
    public static Task SaveAsync<T>(T data, string fileName)
    {
        string json = JsonConvert.SerializeObject(data);
        return Task.Run(() =>
        {
            StreamWriter writer = new StreamWriter(fileName);
            writer.Write(json);
            writer.Close();
        });
    }
    public static Task<T> LoadAsync<T>(string fileName)
    {
        return Task.Run(() =>
        {
            if (File.Exists(fileName))
            {
                StreamReader reader = new StreamReader(fileName);
                string json = reader.ReadToEnd();
                reader.Close();
                return JsonConvert.DeserializeObject<T>(json);
            }
            else
            {
                return default(T);
            }
        });
    }

    public static T Load<T>(string fileName)
    {
        if (File.Exists(fileName))
        {
            StreamReader reader = new StreamReader(fileName);
            string json = reader.ReadToEnd();
            reader.Close();
            return JsonConvert.DeserializeObject<T>(json);
        }
        else
        {
            return default(T);
        }
    }

    public static void Delete(string fileName)
    {
        if (File.Exists(fileName))
        {
            File.Delete(fileName);
        }
    }

    public static void SaveInUser<T>(T data, string fileName)
    {
        string path = Path.Combine(user, fileName);
        Save(data, path);
    }
    public static T LoadInUser<T>(string fileName)
    {
        string path = Path.Combine(user, fileName);
        return Load<T>(path);
    }
    public static void DeleteInUser(string fileName)
    {
        string path = Path.Combine(user, fileName);
        Delete(path);
    }
    public static void SaveInProject<T>(T data, string fileName)
    {
        string path = Path.Combine(project, fileName);
        Save(data, path);
    }
    public static T LoadInProject<T>(string fileName)
    {
        string path = Path.Combine(project, fileName);
        return Load<T>(path);
    }
    public static void DeleteInProject(string fileName)
    {
        string path = Path.Combine(project, fileName);
        Delete(path);
    }

    public static async Task SaveInUserAsync<T>(T data, string fileName)
    {
        string path = Path.Combine(user, fileName);
        await SaveAsync(data, path);
    }
    public static async Task<T> LoadInUserAsync<T>(string fileName)
    {
        string path = Path.Combine(user, fileName);
        return await LoadAsync<T>(path);
    }
    public static async Task DeleteInUserAsync(string fileName)
    {
        string path = Path.Combine(user, fileName);
        await Task.Run(() => Delete(path));
    }
    public static async Task SaveInProjectAsync<T>(T data, string fileName)
    {
        string path = Path.Combine(project, fileName);
        await SaveAsync(data, path);
    }
    public static async Task<T> LoadInProjectAsync<T>(string fileName)
    {
        string path = Path.Combine(project, fileName);
        return await LoadAsync<T>(path);
    }
    public static async Task DeleteInProjectAsync(string fileName)
    {
        string path = Path.Combine(project, fileName);
        await Task.Run(() => Delete(path));
    }
}
