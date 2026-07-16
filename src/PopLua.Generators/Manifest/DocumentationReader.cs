using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PopLua.Generators.Manifest;

internal static class DocumentationReader
{
    public static Documentation FromSymbol(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml(expandIncludes: true);
        if (!string.IsNullOrWhiteSpace(xml))
            return FromXml(xml!);

        foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not CSharpSyntaxNode syntax)
                continue;

            var documentation = syntax.GetLeadingTrivia()
                .Select(trivia => trivia.GetStructure())
                .OfType<DocumentationCommentTriviaSyntax>()
                .FirstOrDefault();

            if (documentation is null)
            {
                var raw = FromRawTrivia(syntax.GetLeadingTrivia().ToFullString());
                if (!raw.IsEmpty)
                    return raw;

                continue;
            }

            var result = FromXml("<member>" + documentation.ToFullString() + "</member>");
            if (!result.IsEmpty)
                return result;
        }

        return Documentation.Empty;
    }

    private static Documentation FromRawTrivia(string trivia)
    {
        if (string.IsNullOrWhiteSpace(trivia) || !trivia.Contains("///"))
            return Documentation.Empty;

        var builder = new System.Text.StringBuilder();
        foreach (var line in trivia.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("///", StringComparison.Ordinal))
                continue;

            builder.AppendLine(trimmed.Substring(3).TrimStart());
        }

        return builder.Length == 0
            ? Documentation.Empty
            : FromXml("<member>" + builder + "</member>");
    }

    private static Documentation FromXml(string xml)
    {
        try
        {
            var root = XElement.Parse(xml);
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var element in root.Elements("param"))
            {
                var name = element.Attribute("name")?.Value;
                var text = Normalize(element.Value);
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(text))
                    parameters[name!] = text!;
            }

            var examples = root.Elements("example")
                .Select(element => NormalizeBlock(element.Value))
                .Where(value => value is not null)
                .Select(value => value!)
                .ToArray();

            var exceptions = root.Elements("exception")
                .Select(element => new
                {
                    Cref = NormalizeCref(element.Attribute("cref")?.Value),
                    Text = Normalize(element.Value),
                })
                .Where(exception => exception.Text is not null)
                .Select(exception => new ExceptionDocumentation(exception.Cref, exception.Text!))
                .ToArray();

            return new Documentation(
                Normalize(root.Element("summary")?.Value),
                Normalize(root.Element("remarks")?.Value),
                Normalize(root.Element("returns")?.Value),
                parameters,
                examples,
                exceptions);
        }
        catch
        {
            return Documentation.Empty;
        }
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value!;
        var parts = text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => part.Length > 0);

        var normalized = string.Join(" ", parts);
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? NormalizeBlock(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var lines = value!
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        return lines.Length == 0 ? null : string.Join("\n", lines);
    }

    private static string? NormalizeCref(string? cref)
    {
        if (string.IsNullOrWhiteSpace(cref))
            return null;

        var value = cref!;
        return value.Length > 2 && value[1] == ':'
            ? value.Substring(2)
            : value;
    }
}
