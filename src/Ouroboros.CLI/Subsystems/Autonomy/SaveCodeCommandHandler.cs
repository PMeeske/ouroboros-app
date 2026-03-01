// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
namespace Ouroboros.CLI.Subsystems.Autonomy;

using System.Text;
using System.Text.Json;
using Ouroboros.Abstractions.Monads;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;

/// <summary>
/// Handles the "save code" command by parsing user input and invoking
/// the <c>modify_my_code</c> tool directly, bypassing the LLM.
/// </summary>
internal sealed class SaveCodeCommandHandler
{
    private readonly Func<string, Maybe<ITool>> _getToolFunc;

    /// <summary>
    /// Initializes a new instance of <see cref="SaveCodeCommandHandler"/>.
    /// </summary>
    /// <param name="getToolFunc">
    /// A delegate that resolves a tool by name from the tool registry.
    /// Typically <c>name => toolSubsystem.Tools.GetTool(name)</c>.
    /// </param>
    public SaveCodeCommandHandler(Func<string, Maybe<ITool>> getToolFunc)
    {
        _getToolFunc = getToolFunc ?? throw new ArgumentNullException(nameof(getToolFunc));
    }

    /// <summary>
    /// Executes the save-code command for the given user argument.
    /// </summary>
    public async Task<string> ExecuteAsync(string argument)
    {
        try
        {
            Maybe<ITool> toolOption = _getToolFunc("modify_my_code");
            if (!toolOption.HasValue)
            {
                return "modify_my_code tool is not registered. Please restart with proper tool initialization.";
            }

            ITool tool = toolOption.GetValueOrDefault(null!)!;

            if (string.IsNullOrWhiteSpace(argument))
            {
                return GetUsageText();
            }

            string jsonInput = ParseArgument(argument);

            AnsiConsole.MarkupLine(
                OuroborosTheme.Dim(
                    $"[SaveCode] Invoking modify_my_code with: {jsonInput[..Math.Min(100, jsonInput.Length)]}..."));

            Result<string, string> result = await tool.InvokeAsync(jsonInput);

            return result.IsSuccess
                ? $"Code Modified Successfully\n\n{result.Value}"
                : $"Modification Failed\n\n{result.Error}";
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return $"SaveCode command failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Parses the raw argument string into a JSON tool input.
    /// Accepts either raw JSON or positional <c>file "search" "replace"</c> format.
    /// </summary>
    internal static string ParseArgument(string argument)
    {
        if (argument.TrimStart().StartsWith('{'))
        {
            return argument;
        }

        string normalizedArg = NormalizeSmartQuotes(argument);

        (char quoteChar, int firstQuote) = DetectQuoteChar(normalizedArg);

        string filePart = normalizedArg[..firstQuote].Trim();
        string rest = normalizedArg[firstQuote..];

        List<string> quoted = ParseQuotedStrings(rest, quoteChar);

        if (quoted.Count < 2)
        {
            throw new FormatException(
                $"Could not parse search and replace strings. Found {quoted.Count} quoted section(s). " +
                "Use format: filename \"search\" \"replace\" (or single quotes).");
        }

        return BuildJsonInput(filePart, quoted[0], quoted[1]);
    }

    /// <summary>
    /// Replaces Unicode smart quotes and backticks with their standard ASCII equivalents.
    /// </summary>
    internal static string NormalizeSmartQuotes(string input)
    {
        return input
            .Replace('\u201C', '"')  // Left smart quote
            .Replace('\u201D', '"')  // Right smart quote
            .Replace('\u201E', '"')  // German low quote
            .Replace('\u201F', '"')  // Double high-reversed-9
            .Replace('\u2018', '\'') // Left single smart quote
            .Replace('\u2019', '\'') // Right single smart quote
            .Replace('`', '\'');     // Backtick to single quote
    }

    /// <summary>
    /// Extracts quoted segments from a string using the specified quote character.
    /// </summary>
    internal static List<string> ParseQuotedStrings(string rest, char quoteChar)
    {
        var quoted = new List<string>();
        bool inQuote = false;
        var current = new StringBuilder();

        for (int i = 0; i < rest.Length; i++)
        {
            char c = rest[i];
            if (c == quoteChar)
            {
                if (inQuote)
                {
                    quoted.Add(current.ToString());
                    current.Clear();
                    inQuote = false;
                }
                else
                {
                    inQuote = true;
                }
            }
            else if (inQuote)
            {
                current.Append(c);
            }
        }

        return quoted;
    }

    /// <summary>
    /// Builds the JSON input for the <c>modify_my_code</c> tool.
    /// </summary>
    internal static string BuildJsonInput(string filePath, string searchText, string replaceText)
    {
        return JsonSerializer.Serialize(new
        {
            file = filePath,
            search = searchText,
            replace = replaceText
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────

    private static (char quoteChar, int firstQuote) DetectQuoteChar(string normalizedArg)
    {
        int firstDoubleQuote = normalizedArg.IndexOf('"');
        int firstSingleQuote = normalizedArg.IndexOf('\'');

        if (firstDoubleQuote == -1 && firstSingleQuote == -1)
        {
            throw new FormatException(
                "Invalid format. Use JSON or: filename \"search text\" \"replace text\". " +
                "You can use double quotes (\") or single quotes (').");
        }

        if (firstDoubleQuote == -1)
            return ('\'', firstSingleQuote);

        if (firstSingleQuote == -1)
            return ('"', firstDoubleQuote);

        return firstDoubleQuote < firstSingleQuote
            ? ('"', firstDoubleQuote)
            : ('\'', firstSingleQuote);
    }

    private static string GetUsageText()
    {
        return """
            Save Code - Direct Tool Invocation

            Usage: save {"file":"path/to/file.cs","search":"exact text to find","replace":"replacement text"}

            Or use the interactive format:
              save file.cs "old text" "new text"

            Examples:
              save {"file":"src/Ouroboros.CLI/Commands/OuroborosAgent.cs","search":"old code","replace":"new code"}
              save MyClass.cs "public void Old()" "public void New()"

            This command directly invokes the modify_my_code tool, bypassing the LLM.
            """;
    }
}
