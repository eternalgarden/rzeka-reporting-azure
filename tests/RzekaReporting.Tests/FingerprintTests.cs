using System.Security.Cryptography;
using System.Text;
using RzekaReporting.Functions.Process;

namespace RzekaReporting.Tests;

public class FingerprintTests(ITestOutputHelper output)
{
    // A real stack trace from the CrashReportDemo, as the client sends it
    // (paths already scrubbed).
    const string DemoTrace =
        "   at Health.Apply(DamageTaken damage) in Program.cs:line 86\n"
        + "   at System.Reactive.Linq.ObservableImpl.Select`2.Selector._.OnNext(TSource value)\n";

    // The fields part every canonical string below starts with.
    const string Fields =
        "v1|Looming|Looming of DamageTaken into HealthChanged|Health|System.InvalidOperationException";

    static string Canonical(
        string school = "Looming",
        string title = "Looming of DamageTaken into HealthChanged",
        string owner = "Health",
        string type = "System.InvalidOperationException",
        string? trace = DemoTrace
    ) => Fingerprint.Canonical(school, title, owner, type, trace);

    // ── Canonical: fields ────────────────────────────────────────────────────

    [Fact]
    public void Starts_with_the_version_then_the_identifying_fields()
    {
        Assert.Equal(Fields, Canonical(trace: null));
    }

    [Fact]
    public void Empty_stack_trace_is_the_same_as_none()
    {
        Assert.Equal(Fields, Canonical(trace: ""));
    }

    [Fact]
    public void Fields_are_kept_apart_by_the_separator()
    {
        // Without a separator, "ab"+"c" and "a"+"bc" would read the same.
        Assert.NotEqual(
            Fingerprint.Canonical("ab", "c", "Owner", "T", null),
            Fingerprint.Canonical("a", "bc", "Owner", "T", null)
        );
    }

    // ── Canonical: frames ────────────────────────────────────────────────────

    [Fact]
    public void Frames_lose_at_file_name_and_line_number()
    {
        string x = $"{Fields}|Health.Apply(DamageTaken damage)";
        string y = Canonical();
        Assert.Equal(x, y);
    }

    [Fact]
    public void Frames_without_file_information_are_kept_as_they_are()
    {
        Assert.Equal(
            $"{Fields}|Health.Apply(DamageTaken damage)",
            Canonical(trace: "   at Health.Apply(DamageTaken damage)")
        );
    }

    [Fact]
    public void Framework_and_rzeka_frames_are_skipped()
    {
        const string trace =
            "   at Health.Apply(DamageTaken damage) in Program.cs:line 86\n"
            + "   at System.Reactive.Sink`1.ForwardOnNext(TTarget value)\n"
            + "   at Microsoft.Extensions.Something.Run()\n"
            + "   at Rzeka.ConjuringErrorBoundary.WhisperOnThrow()\n"
            + "   at Combat.Resolve() in Combat.cs:line 12";

        Assert.Equal(
            $"{Fields}|Health.Apply(DamageTaken damage)|Combat.Resolve()",
            Canonical(trace: trace)
        );
    }

    [Fact]
    public void Lines_that_are_not_frames_are_skipped()
    {
        const string trace =
            "   at Health.Apply(DamageTaken damage) in Program.cs:line 86\n"
            + "--- End of stack trace from previous location ---\n"
            + "   at Combat.Resolve() in Combat.cs:line 12";

        Assert.Equal(
            $"{Fields}|Health.Apply(DamageTaken damage)|Combat.Resolve()",
            Canonical(trace: trace)
        );
    }

    [Fact]
    public void Windows_line_endings_leave_no_trace()
    {
        Assert.Equal(
            $"{Fields}|Health.Apply(DamageTaken damage)",
            Canonical(trace: DemoTrace.Replace("\n", "\r\n"))
        );
    }

    [Fact]
    public void Only_the_first_three_app_frames_are_kept()
    {
        const string trace =
            "   at Game.A()\n   at System.Linq.Enumerable.First()\n   at Game.B()\n   at Game.C()\n   at Game.D()";

        Assert.Equal($"{Fields}|Game.A()|Game.B()|Game.C()", Canonical(trace: trace));
    }

    [Theory]
    [InlineData("Player.<>c.<_Ready>b__3_0(DamageTaken d)", "Player.<_Ready>b__(DamageTaken d)")]
    [InlineData("Player.<>c__DisplayClass5_0.<Attack>b__0(Ping p)", "Player.<Attack>b__(Ping p)")]
    public void Compiler_generated_numbering_is_removed(string frame, string expected)
    {
        Assert.Equal($"{Fields}|{expected}", Canonical(trace: $"   at {frame}"));
    }

    [Fact]
    public void Realistic_lambda_trace_survives_a_code_edit_and_an_rx_upgrade()
    {
        // The same failing lambda before and after: another lambda was added to Player
        // (numbering shifted), lines moved, and an Rx upgrade renamed its internal classes.
        const string before =
            "   at Player.<>c.<_Ready>b__3_0(DamageTaken d) in Player.cs:line 41\n"
            + "   at System.Reactive.Linq.ObservableImpl.Select`2.Selector._.OnNext(TSource value)\n"
            + "   at Player.<>c__DisplayClass5_0.<Attack>b__1(Ping p) in Player.cs:line 60";
        const string after =
            "   at Player.<>c.<_Ready>b__4_0(DamageTaken d) in Player.cs:line 44\n"
            + "   at System.Reactive.Linq.ObservableImpl.Select`2.SelectorImpl.OnNext(TSource value)\n"
            + "   at Player.<>c__DisplayClass6_0.<Attack>b__2(Ping p) in Player.cs:line 71";
        const string expected =
            $"{Fields}|Player.<_Ready>b__(DamageTaken d)|Player.<Attack>b__(Ping p)";

        Assert.Equal(expected, Canonical(trace: before));
        Assert.Equal(expected, Canonical(trace: after));
    }

    [Fact]
    public void The_method_a_lambda_belongs_to_is_kept()
    {
        // <_Ready> and <Attack> are different methods: different bugs.
        Assert.Equal(
            $"{Fields}|Player.<Attack>b__(DamageTaken d)",
            Canonical(trace: "   at Player.<>c.<Attack>b__3_0(DamageTaken d) in Player.cs:line 41")
        );
    }

    // ── Canonical: edge cases (from the review) ──────────────────────────────

    [Theory]
    [InlineData("Combat.Heal(Stat stat)")]
    [InlineData("Harbor.Moor(Boat boat, Int32 at)")]
    public void The_word_at_inside_a_frame_is_kept(string frame)
    {
        Assert.Equal($"{Fields}|{frame}", Canonical(trace: $"   at {frame} in Combat.cs:line 3"));
    }

    [Theory]
    // Captures nothing: a method on the shared <>c class.
    [InlineData("Player.<>c.<_Ready>b__3_0(DamageTaken d)")]
    // Captures only `this`: a private method on Player itself.
    [InlineData("Player.<_Ready>b__3_0(DamageTaken d)")]
    // Captures a local: a method on a DisplayClass.
    [InlineData("Player.<>c__DisplayClass3_0.<_Ready>b__0(DamageTaken d)")]
    public void A_lambda_is_the_same_whatever_it_captures(string frame)
    {
        // Editing a lambda to capture a variable moves it to another generated class.
        // It's still the same lambda in the same place in the code: the same bug.
        Assert.Equal(
            $"{Fields}|Player.<_Ready>b__(DamageTaken d)",
            Canonical(trace: $"   at {frame}")
        );
    }

    [Fact]
    public void Local_function_keeps_its_name_but_loses_its_numbering()
    {
        // `Helper` is a name the developer chose, so it's stable; `|3_0` is a counter.
        Assert.Equal(
            $"{Fields}|Player.<_Ready>g__Helper(Ping p)",
            Canonical(trace: "   at Player.<_Ready>g__Helper|3_0(Ping p) in Player.cs:line 50")
        );
    }

    [Fact]
    public void No_separator_leaks_out_of_a_frame()
    {
        string canonical = Canonical(trace: "   at Player.<_Ready>g__Helper|3_0(Ping p)");

        // 5 fields + 1 frame: every | is a real separator.
        Assert.Equal(6, canonical.Split('|').Length);
    }

    [Fact]
    public void Async_state_machine_numbering_is_removed()
    {
        Assert.Equal(
            $"{Fields}|Player.<LoadAsync>d__.MoveNext()",
            Canonical(trace: "   at Player.<LoadAsync>d__5.MoveNext() in Player.cs:line 88")
        );
    }

    [Fact]
    public void An_empty_last_field_is_not_lost()
    {
        // Real reports always have an exception type, but the canonical string must not
        // depend on that: an empty field is still a field.
        Assert.Equal("v1|S|T|O|", Fingerprint.Canonical("S", "T", "O", "", null));
    }

    // ── Canonical: frame shapes found by probing ─────────────────────────────

    [Theory]
    // A digit right before the dot: common for game classes like Level2 or Stage3Boss.
    [InlineData("Level2.Load(Int32 id)")]
    // A generic class: the runtime writes Pool<T> as Pool`1.
    [InlineData("Pool`1.Get()")]
    // A user method that only looks generated: no '>' before the d__, so its digit stays.
    [InlineData("Level2.Load__2()")]
    public void App_frames_of_any_class_name_are_kept(string frame)
    {
        Assert.Equal($"{Fields}|{frame}", Canonical(trace: $"   at {frame} in Level2.cs:line 7"));
    }

    [Theory]
    [InlineData("Player.<_Ready>g__Retry2|3_0(Ping p)", "Player.<_Ready>g__Retry2(Ping p)")]
    [InlineData("Player.<_Ready>g__load_file|3_0(Ping p)", "Player.<_Ready>g__load_file(Ping p)")]
    public void Local_function_names_of_any_shape_are_kept_whole(string frame, string expected)
    {
        Assert.Equal($"{Fields}|{expected}", Canonical(trace: $"   at {frame}"));
    }

    [Theory]
    // Top-level statements (like samples/CrashReportDemo/Program.cs) live in a generated
    // method called <Main>$, so its lambdas and local functions are named <<Main>$>…
    [InlineData(
        "Program.<<Main>$>g__RunCombat|0_1(IRzeka river)",
        "Program.<<Main>$>g__RunCombat(IRzeka river)"
    )]
    [InlineData(
        "Program.<>c.<<Main>$>b__0_4(GoblinAttacked attack)",
        "Program.<<Main>$>b__(GoblinAttacked attack)"
    )]
    public void Top_level_statements_are_normalized_too(string frame, string expected)
    {
        Assert.Equal($"{Fields}|{expected}", Canonical(trace: $"   at {frame}"));
    }

    // ── Compute: the hash of Canonical ───────────────────────────────────────

    [Fact]
    public void Fingerprint_is_the_sha256_of_the_canonical_string_as_lowercase_hex()
    {
        string canonical = Canonical();
        string expected = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical))
        );

        Assert.Equal(
            expected,
            Fingerprint.Compute(
                "Looming",
                "Looming of DamageTaken into HealthChanged",
                "Health",
                "System.InvalidOperationException",
                DemoTrace
            )
        );
    }

    [Fact]
    public void Fingerprint_is_64_lowercase_hex_characters()
    {
        Assert.Matches("^[0-9a-f]{64}$", Fingerprint.Compute("a", "b", "c", "d", null));
    }
}
