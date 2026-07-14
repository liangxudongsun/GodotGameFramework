using Godot;
using System;
namespace GodotGameFrameworkCore.SingletonSystem
{
    public partial class Singleton<T> : ISingleton where T : Singleton<T>, new()
    {
        private static T instance;
        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new T();
                    instance.Active();
                    SingletonSystem.Retain(instance);
                }
                return instance;
            }
        }

        public virtual void Active()
        {

        }

        public virtual void Release()
        {
            OnRelease();
            if (instance != null)
            {
                SingletonSystem.Release(instance);
                instance = null;
            }
        }
        protected virtual void OnRelease()
        {
        }
    }
}