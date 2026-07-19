using GameFramework.Download;
using Godot;
using System;
namespace GodotGameFramework.Download;

public abstract partial class DownloadAgentHelperBase : GodotComponent, IDownloadAgentHelper
{
    public abstract event EventHandler<DownloadAgentHelperUpdateBytesEventArgs> DownloadAgentHelperUpdateBytes;
    public abstract event EventHandler<DownloadAgentHelperUpdateLengthEventArgs> DownloadAgentHelperUpdateLength;
    public abstract event EventHandler<DownloadAgentHelperCompleteEventArgs> DownloadAgentHelperComplete;
    public abstract event EventHandler<DownloadAgentHelperErrorEventArgs> DownloadAgentHelperError;

    public abstract void Download(string downloadUri, object userData);

    public abstract void Download(string downloadUri, long fromPosition, object userData);

    public abstract void Download(string downloadUri, long fromPosition, long toPosition, object userData);

    public abstract void Reset();

}
