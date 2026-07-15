using GameFramework;
using GameFramework.Event;
using Godot;
using System;
namespace GodotGameFramework.Web;


public partial class WebRequestCompleteEventArgs : GameEventArgs
{
    public static int EventId => typeof(WebRequestCompleteEventArgs).GetHashCode();
    public override int Id => EventId;
    public string Url;
    public long Result;
    public long ResponseCode;
    public string[] Headers;
    public byte[] Body;
    public WebRequestCompleteEventArgs()
    {

    }
    public WebRequestCompleteEventArgs(string url, long result, long responseCode, string[] headers, byte[] body)
    {
        Url = url;
        Result = result;
        ResponseCode = responseCode;
        Headers = headers;
        Body = body;
    }

    public static WebRequestCompleteEventArgs Create(string url, long result, long responseCode, string[] headers, byte[] body)
    {
        var args = ReferencePool.Acquire<WebRequestCompleteEventArgs>();
        args.Url = url;
        args.Result = result;
        args.ResponseCode = responseCode;
        args.Headers = headers;
        args.Body = body;
        return args;
    }


    public override void Clear()
    {
        Url = null;
        Result = 0;
        ResponseCode = 0;
        Headers = null;
        Body = null;
    }

}
