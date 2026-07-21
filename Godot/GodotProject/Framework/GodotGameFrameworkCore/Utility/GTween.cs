using Godot;
using System;

namespace GodotGameFramework.DoTween
{
    /// <summary>
    /// 基于 Godot Tween 的扩展方法
    /// </summary>
    public static class GTween
    {
        private const string PropScale = "scale";
        private const string PropPosition = "position";
        private const string PropRotation = "rotation";
        private const string PropModulate = "modulate";

        #region Scale
        /// <summary>
        /// 单向缩放到目标值
        /// </summary>
        public static Tween DoScale(this Node2D node, Vector2 targetScale, float duration = 1f)
        {
            var tween = node.CreateTween();
            // 默认设置缓动，让动作更自然，类似 DOTween 的默认行为
            tween.SetTrans(Tween.TransitionType.Expo);
            tween.SetEase(Tween.EaseType.Out);

            tween.TweenProperty(node, PropScale, targetScale, duration);
            return tween;
        }

        /// <summary>
        /// 单向缩放重载 
        /// </summary>
        public static Tween DoScale(this Node2D node, float targetScale, float duration = 1f)
        {
            return node.DoScale(new Vector2(targetScale, targetScale), duration);
        }

        /// <summary>
        /// 脉冲效果：缩放到目标值再缩回来 
        /// </summary>
        public static Tween DOPunchScale(this Node2D node, Vector2 punchScale, float duration = 1f)
        {
            var tween = node.CreateTween();
            Vector2 originalScale = node.Scale;

            // 也可以设置为 Elastic 过渡效果
            tween.SetTrans(Tween.TransitionType.Cubic);
            tween.SetEase(Tween.EaseType.InOut);

            // 并行执行两个动画是不对的，这里应该是序列
            // Godot 4 默认 CreateTween 就是序列执行的

            tween.TweenProperty(node, PropScale, punchScale, duration / 2f);
            tween.TweenProperty(node, PropScale, originalScale, duration / 2f);

            return tween;
        }

        public static Tween DOPunchScale(this Node2D node, float punchScale, float duration = 1f)
        {
            return node.DOPunchScale(new Vector2(punchScale, punchScale), duration);
        }
        /// <summary>
        /// 单向缩放到目标值
        /// </summary>
        public static Tween DoScale(this Control node, Vector2 targetScale, float duration = 1f)
        {
            var tween = node.CreateTween();
            // 默认设置缓动，让动作更自然，类似 DOTween 的默认行为
            tween.SetTrans(Tween.TransitionType.Expo);
            tween.SetEase(Tween.EaseType.Out);

            tween.TweenProperty(node, PropScale, targetScale, duration);
            return tween;
        }

        /// <summary>
        /// 单向缩放重载 
        /// </summary>
        public static Tween DoScale(this Control node, float targetScale, float duration = 1f)
        {
            return node.DoScale(new Vector2(targetScale, targetScale), duration);
        }

        /// <summary>
        /// 脉冲效果：缩放到目标值再缩回来 
        /// </summary>
        public static Tween DOPunchScale(this Control node, Vector2 punchScale, float duration = 1f)
        {
            var tween = node.CreateTween();
            Vector2 originalScale = node.Scale;

            // 也可以设置为 Elastic 过渡效果
            tween.SetTrans(Tween.TransitionType.Cubic);
            tween.SetEase(Tween.EaseType.InOut);


            tween.TweenProperty(node, PropScale, punchScale, duration / 2f);
            tween.TweenProperty(node, PropScale, originalScale, duration / 2f);

            return tween;
        }

        public static Tween DOPunchScale(this Control node, float punchScale, float duration = 1f)
        {
            return node.DOPunchScale(new Vector2(punchScale, punchScale), duration);
        }

        #endregion

        #region Position

        /// <summary>
        /// 移动到目标位置
        /// </summary>
        public static Tween DOLocalMove(this Node2D node, Vector2 targetPos, float duration = 1f)
        {
            var tween = node.CreateTween();
            tween.SetTrans(Tween.TransitionType.Expo);
            tween.SetEase(Tween.EaseType.Out);
            tween.TweenProperty(node, PropPosition, targetPos, duration);
            return tween;
        }
        public static Tween DOMove(this Node2D node, Vector2 targetPos, float duration = 1f)
        {
            var tween = node.CreateTween();
            tween.SetTrans(Tween.TransitionType.Expo);
            tween.SetEase(Tween.EaseType.Out);
            tween.TweenProperty(node, $"{Node2D.PropertyName.GlobalPosition}", targetPos, duration);
            return tween;
        }

        /// <summary>
        /// 旋转到目标角度
        /// </summary>
        public static Tween DORotate(this Node2D node, float targetRotation, float duration = 1f)
        {
            var tween = node.CreateTween();
            tween.SetTrans(Tween.TransitionType.Back); // Back 效果很有动感
            tween.SetEase(Tween.EaseType.Out);
            tween.TweenProperty(node, PropRotation, targetRotation, duration);
            return tween;
        }

        /// <summary>
        /// 颜色闪烁/渐变
        /// </summary>
        public static Tween DOColor(this Node2D node, Color targetColor, float duration = 1f)
        {
            var tween = node.CreateTween();
            tween.SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(node, PropModulate, targetColor, duration);
            return tween;
        }

        /// <summary>
        /// 延时执行回调 
        /// </summary>
        public static Tween Delay(this Node node, float delay, Action callback = null)
        {
            var tween = node.CreateTween();
            if (callback != null)
            {
                tween.TweenInterval(delay);
                tween.TweenCallback(Callable.From(callback));
            }
            else
            {
                tween.TweenInterval(delay);
            }
            return tween;
        }

        #endregion
    }
}
