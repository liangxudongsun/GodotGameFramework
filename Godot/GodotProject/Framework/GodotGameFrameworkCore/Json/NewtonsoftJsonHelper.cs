using Godot;
using System;
using static GameFramework.Utility.Json;
using Newtonsoft.Json;
namespace GodotGameFramework
{
    /// <summary>
    /// 默认的Json序列化工具
    /// </summary>
    public partial class NewtonsoftJsonHelper : IJsonHelper
    {
        public string ToJson(object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        public T ToObject<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        public object ToObject(Type objectType, string json)
        {
            return JsonConvert.DeserializeObject(json, objectType);
        }

    }
}

