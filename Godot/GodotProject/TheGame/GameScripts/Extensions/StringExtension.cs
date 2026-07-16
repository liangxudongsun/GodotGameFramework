using Godot;
using System;
using System.Text;
namespace GodotGameFramework.Extensions
{
    public static class StringExtension
    {
        public static string ColorString(this string str, string color)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<color=");
            builder.Append(color);
            builder.Append(">");
            builder.Append(str);
            builder.Append("</color>");
            return builder.ToString();
        }
        public static string ColorString(this string str, Color color)
        {
            return str.ColorString(color.ToHtml());
        }

    }
}

