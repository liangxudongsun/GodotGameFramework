using Godot;
using GodotGameFramework;
using System;
namespace GodotGameFrameworkCore.SingletonSystem
{
    public partial class UpdateDriver : GameFrameworkComponent, IUpdateDriver
    {
        private event Action<double> UpdateEvent;
        private event Action<double> FixedUpdateEvent;

        public override void OnUpdate(double delta)
        {
            base.OnUpdate(delta);
            UpdateEvent?.Invoke(delta);
        }

        public override void OnFixedUpdate(double delta)
        {
            base.OnFixedUpdate(delta);
            FixedUpdateEvent?.Invoke(delta);
        }

        public void AddUpdateListener(Action<double> action)
        {
            UpdateEvent += action;
        }

        public void AddFixedUpdateListener(Action<double> action)
        {
            FixedUpdateEvent += action;
        }

        public void RemoveUpdateListener(Action<double> action)
        {
            UpdateEvent -= action;
        }

        public void RemoveFixedUpdateListener(Action<double> action)
        {
            FixedUpdateEvent -= action;
        }
    }
}