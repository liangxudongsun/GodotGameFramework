//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using Godot;
using System;
using System.Collections.Generic;

namespace GodotGameFramework
{
    public partial class GameEntry : GodotComponent
    {
        private static readonly GameFrameworkLinkedList<GameFrameworkComponent> m_Components =
            new GameFrameworkLinkedList<GameFrameworkComponent>();

        private static BaseComponent m_BaseComponent = null;
        private static bool m_Shutdown = false;

        /// <summary>子节点按子→父顺序完成 _Ready 和注册后，触发 GameFrameworkEntry.Update</summary>
        public override void _Ready()
        {
        }

        public override void _Process(double delta)
        {
            if (m_Shutdown) return;

            float elapseSeconds = (float)delta;
            float realElapseSeconds = (float)Engine.TimeScale > 0f
                ? elapseSeconds / (float)Engine.TimeScale
                : 0f;

            GameFrameworkEntry.Update(elapseSeconds, realElapseSeconds);
        }

        public static T GetComponent<T>() where T : GameFrameworkComponent
        {
            return (T)GetComponent(typeof(T));
        }

        public static GameFrameworkComponent GetComponent(Type type)
        {
            LinkedListNode<GameFrameworkComponent> current = m_Components.First;
            while (current != null)
            {
                if (current.Value.GetType() == type)
                    return current.Value;
                current = current.Next;
            }
            return null;
        }

        public static GameFrameworkComponent GetComponent(string typeName)
        {
            LinkedListNode<GameFrameworkComponent> current = m_Components.First;
            while (current != null)
            {
                Type type = current.Value.GetType();
                if (type.FullName == typeName || type.Name == typeName)
                    return current.Value;
                current = current.Next;
            }
            return null;
        }

        public static void Shutdown(ShutdownType shutdownType)
        {
            m_Shutdown = true;
            GD.Print($"[GGF] Shutdown Game Framework ({shutdownType})...");

            if (m_BaseComponent != null)
            {
                m_BaseComponent.Shutdown();
                m_BaseComponent = null;
            }
            m_Components.Clear();

            if (shutdownType == ShutdownType.None) return;

            var sceneTree = (SceneTree)Engine.GetMainLoop();
            if (shutdownType == ShutdownType.Restart)
                sceneTree.ReloadCurrentScene();
            else if (shutdownType == ShutdownType.Quit)
                sceneTree.Quit();
        }

        internal static void RegisterComponent(GameFrameworkComponent component)
        {
            if (component == null)
            {
                GD.PrintErr("[GGF] Game Framework component is invalid.");
                return;
            }

            Type type = component.GetType();
            LinkedListNode<GameFrameworkComponent> current = m_Components.First;
            while (current != null)
            {
                if (current.Value.GetType() == type)
                {
                    GD.PrintErr($"[GGF] Game Framework component type '{type.FullName}' is already exist.");
                    return;
                }
                current = current.Next;
            }

            m_Components.AddLast(component);

            if (component is BaseComponent baseComponent)
                m_BaseComponent = baseComponent;
        }
    }
}
