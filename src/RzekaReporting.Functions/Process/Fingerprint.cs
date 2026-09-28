using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;

namespace RzekaReporting.Functions.Process;

/*
 * Personal notes. TODO move to notes not a code comment
 * Group reports into issues: same fingerprint = same bug.
 * Fingerprint does not have to be easily readable for a human.
 * Yes i wrote those regex spells myself, am i insane? But regex is fun and i love regex101.com
 *
 * partial is required because [GeneratedRegex] uses a source generator, it runs during compilation
 * and generates a partial for Fingerprint that contains an optimized regex matcher
 * it also has the useful property that an bug in the pattern becomes a compile error instead of
 * a runtime exception
*/
internal static partial class Fingerprint
{
    const string RULES_VERSION = "v1";
    const int TRACE_DEPTH = 3;
    static readonly string[] discardedPrefixes = ["System.", "Microsoft.", "Rzeka."];

    /*
     * in
     * at Health.Apply(DamageTaken damage) in Program.cs:line 86
     * to
     * Health.Apply(DamageTaken damage)
     *
     * select "at "
     * ^ is necessary because we could accidentally select another 'at' like ins 'Stats'
     */
    [GeneratedRegex(@"^at\s+")]
    private static partial Regex AtPrefix();

    /*
     *  in
     *  at Health.Apply(DamageTaken damage) in Program.cs:line 86
     *  to
     *  at Health.Apply(DamageTaken damage)
     *
     *  select what follows after ")"
     *  assumes a that a single trace line contains only one closing bracket
    */
    [GeneratedRegex(@"(?<=\))\sin.+")]
    private static partial Regex FileAndLine();

    /*
     * in
     * Player.<_Ready>b__3_0(DamageTaken d)
     * to
     * Player.<_Ready>b__(DamageTaken d)
     *
     * or
     * Player.<Attack>b__1(Ping p)
     * to
     * Player.<Attack>b__(Ping p)
     *
     * will check for > sign, one of [bgd] and two underscores
     * followed by a digit and optional \w characters
     * stops at '('
     * b is lambda, g is local function, d is async state machine
    */
    [GeneratedRegex(@"(?<=>[bgd]__)\d\w*")]
    private static partial Regex GeneratedCounter();

    /*
     * from
     * Player.<_Ready>g__Helper|3_0(Ping p)
     * to
     * Player.<_Ready>g__Helper(Ping p)
     */
    [GeneratedRegex(@"\|\d+_\d+(?=\()")]
    private static partial Regex GeneratedLocalMethodCounter();

    /*
     * in
     * Player.<>c.<_Ready>b(DamageTaken d)
     * to
     * Player.<_Ready>b(DamageTaken d)
     *
     * or
     * Player.<>c__DisplayClass5_0.<Attack>b(Ping p)
     * to
     * Player.<Attack>b(Ping p)
     *
     * claude found that beautiful pattern used here, earlier i split into two separate selectors:
     *
     * 1. <>c\.
     * 2. (?<=(?:DisplayClass))\w+ (stops at <)
     *
     * but notice the bigger picture here, first format is just <>c., but DisplayClass one
     * also ends with a dot, so we can greatly simplify the pattern,
     * by optionally selecting for DisplayClass squeezed between that <>c and a dot.
     */
    [GeneratedRegex(@"<>c(__DisplayClass\d+_\d+)?\.")]
    private static partial Regex GeneratedClass();

    public static string Compute(
        string school,
        string spellTitle,
        string ownerType,
        string exceptionType,
        string? stackTrace
    ) =>
        Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    Canonical(school, spellTitle, ownerType, exceptionType, stackTrace)
                )
            )
        );

    public static string Canonical(
        string school,
        string spellTitle,
        string ownerType,
        string exceptionType,
        string? stackTrace
    )
    {
        IEnumerable<string> parts = new[]
        {
            RULES_VERSION,
            school,
            spellTitle,
            ownerType,
            exceptionType,
        }.Concat(stackTrace is null ? [] : ProcessTrace(stackTrace));
        return string.Join('|', parts);
    }

    static string[] ProcessTrace(string stackTrace)
    {
        IEnumerable<string> lines = stackTrace
            .Split(
                ["\r\n", "\r", "\n"],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .Where(l => AtPrefix().IsMatch(l))
            .Select(l => AtPrefix().Replace(l, ""))
            .Where(l => !discardedPrefixes.Any(p => l.StartsWith(p)))
            // .Where(l => StackFrame().IsMatch(l))
            .Select(l => FileAndLine().Replace(l, ""))
            .Select(l => GeneratedCounter().Replace(l, ""))
            .Select(l => GeneratedLocalMethodCounter().Replace(l, ""))
            .Select(l => GeneratedClass().Replace(l, ""))
            .Take(TRACE_DEPTH);

        // TODO, roslyn simplified lines.ToArray() to this, learn more
        return [.. lines];
    }

    static void AppendSeparated(this StringBuilder builder, string text)
    {
        builder.Append(text);
        builder.Append('|');
    }
}
