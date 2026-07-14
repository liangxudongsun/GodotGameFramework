using Godot;
using GodotGameFramework;
using System;
namespace GodotGameFrameworkCore.SingletonSystem
{
    public partial class SingletonNode<T> : Node, ISingleton where T : SingletonNode<T>, new()
    {
        private static T m_Instance;

        public static T Instance
        {
            get
            {
                if (m_Instance == null)
                {
                    System.Type thisType = typeof(T);
                    string instName = thisType.Name;
                    Node node = SingletonSystem.GetNode(instName);
                    if (node == null)
                    {
                        node = new T();
                        node.Name = instName;
                        m_Instance = (T)node;
                        m_Instance.Active();
                    }
                    SingletonSystem.Retain(node, m_Instance);
                }

                return m_Instance;
            }
        }

        public virtual void Active()
        {

        }

        public virtual void Release()
        {
            OnRelease();
            if (m_Instance != null)
            {
                SingletonSystem.Release(m_Instance, this);
                m_Instance = null;
            }
        }
        protected virtual void OnRelease()
        {

        }


        public override void _Ready()
        {
            base._Ready();
            if (CheckInstance())
            {
                OnLoad();
            }
        }
        private bool CheckInstance()
        {
            if (this == Instance)
            {
                return true;
            }

            this.QueueFree(); //删除重复的实例
            return false;
        }
        protected virtual void OnLoad()
        {
        }
        public override void _ExitTree()
        {
            base._ExitTree();
            if (this == Instance)
            {
                Release();
            }
        }


    }
}