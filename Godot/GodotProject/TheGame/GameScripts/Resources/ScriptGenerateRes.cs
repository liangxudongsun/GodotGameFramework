using Godot;
using System;
[GlobalClass]
public partial class ScriptGenerateRes : Resource
{
    public static class Parameters
    {
        public static readonly string NameSpace = "NameSpace";
        public static readonly string OutPutPathGe = "OutPutPathGe";
        public static readonly string OutPutPathLogic = "OutPutPathLogic";
    }
    [Export]
    public string NameSpace = "GameLogic";
    [Export(PropertyHint.Dir)]
    public string OutPutPathGe = "res://TheGame/";
    [Export(PropertyHint.Dir)]
    public string OutPutPathLogic = "res://TheGame/";
}
