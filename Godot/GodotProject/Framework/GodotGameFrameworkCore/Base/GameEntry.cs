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
        private bool m_StartProcedure = false;

        public override void OnUpdate(double delta)
        {
            base.OnUpdate(delta);
            if (m_Shutdown) return;
            try
            {
                CheckProcedure();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GameEntry] Startup error: {ex.Message}");
                if (!m_StartProcedure)
                {
                    m_StartProcedure = true;
                }
            }

            float elapseSeconds = (float)delta;
            float realElapseSeconds = (float)Engine.TimeScale > 0f
                ? elapseSeconds / (float)Engine.TimeScale
                : 0f;

            GameFrameworkEntry.Update(elapseSeconds, realElapseSeconds);
        }
        /// <summary>
        /// 验证流程组件是否注册完成，如果完成则启动流程组件
        /// </summary>
        private void CheckProcedure()
        {
            if (!m_StartProcedure)
            {
                var procedureComponent = GetComponent<ProcedureComponent>();
                if (procedureComponent != null)
                {
                    procedureComponent.StartProcedure();
                    m_StartProcedure = true;
                }
            }
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
