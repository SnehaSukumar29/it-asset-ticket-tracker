using System.Reflection;

namespace AssetTracker.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute;

public sealed class AssertionException(string message) : Exception(message);

/// <summary>
/// A tiny dependency-free test runner: finds every [Test] method in this assembly,
/// runs it, and reports pass/fail. Exit code 1 if anything fails (so CI fails too).
/// </summary>
public static class TestRunner
{
    public static int RunAll()
    {
        var tests = Assembly.GetExecutingAssembly().GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<TestAttribute>() is not null)
            .OrderBy(m => m.DeclaringType!.Name).ThenBy(m => m.Name)
            .ToList();

        var failures = 0;
        foreach (var test in tests)
        {
            var name = $"{test.DeclaringType!.Name}.{test.Name}";
            try
            {
                test.Invoke(null, null);
                Console.WriteLine($"  PASS  {name}");
            }
            catch (TargetInvocationException ex)
            {
                failures++;
                Console.WriteLine($"  FAIL  {name}\n        {ex.InnerException?.GetType().Name}: {ex.InnerException?.Message}");
            }
        }
        Console.WriteLine($"\n{tests.Count - failures} passed, {failures} failed, {tests.Count} total.");
        return failures == 0 ? 0 : 1;
    }
}

public static class Assert
{
    public static void True(bool condition, string message = "Expected true")
    {
        if (!condition) throw new AssertionException(message);
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new AssertionException($"Expected <{expected}> but was <{actual}>");
    }

    public static TException Throws<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException ex) { return ex; }
        catch (Exception ex) { throw new AssertionException($"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}"); }
        throw new AssertionException($"Expected {typeof(TException).Name} but nothing was thrown");
    }
}
