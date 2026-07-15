using Godot;
using System;
[GlobalClass]
public partial class UpdateSettingRes : Resource
{
    public static class Parameters
    {
        public static string RemoteUrl = "RemoteUrl";
    }
    [Export]
    public string RemoteUrl = "http://127.0.0.1:8080";
}
