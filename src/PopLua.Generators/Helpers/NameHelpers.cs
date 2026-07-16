using System.Text;

namespace PopLua.Generators.Helpers;

internal static class NameHelpers
{
    public static string ToSnakeCase(string name)
    {
        if (name.Length == 0)
            return name;

        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])
                    || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                    builder.Append('_');

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    public static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
