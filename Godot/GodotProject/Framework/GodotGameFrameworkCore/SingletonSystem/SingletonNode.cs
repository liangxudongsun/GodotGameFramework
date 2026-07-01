using Godot;
using System;

public partial class SingletonNode<T> : Node where T : Node, new()
{
    private static T m_Instance;

    public static T Instance
    {
        get
        {
            if (m_Instance == null)
            {
                m_Instance = new T();
            }
            return m_Instance;
        }
    }

    public override void _Ready()
    {
        base._Ready();
        if (m_Instance == null)
        {
            m_Instance = this as T;
        }
        else if (m_Instance != this)
        {
            QueueFree();
        }
    }
}
