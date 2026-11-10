using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Core.Services;
using FluentAssertions;
using System.Collections;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class DumpSerializerTests
{
    private static DebugConfiguration Config(int? depth = null, int? items = null, int? size = null) => new()
    {
        DumpMaxDepth = depth ?? 6,
        DumpMaxItems = items ?? 100,
        DumpMaxSize = size ?? 64 * 1024,
    };

    private class Node
    {
        public string Name { get; set; } = "";
        public Node? Next { get; set; }
    }

    private class ThrowingProps
    {
        public string Ok { get; set; } = "fine";
        public string Boom => throw new InvalidOperationException("nope");
    }

    private class Credentials
    {
        public string Username { get; set; } = "elad";
        public string Password { get; set; } = "hunter2";
        public string ApiKey { get; set; } = "sk-secret";
    }

    private sealed class FakeQueryable<T> : IQueryable<T>
    {
        private readonly List<T> _items;
        public int EnumerationCount;

        public FakeQueryable(List<T> items)
        {
            _items = items;
            Expression = System.Linq.Expressions.Expression.Constant(this);
        }

        public Type ElementType => typeof(T);
        public System.Linq.Expressions.Expression Expression { get; }
        public IQueryProvider Provider => throw new NotSupportedException("query should never execute");

        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static IEnumerable<int> InfiniteSequence()
    {
        var i = 0;
        while (true) yield return i++;
    }

    [Fact]
    public void Self_referencing_object_does_not_throw()
    {
        var a = new Node { Name = "a" };
        a.Next = a;

        var act = () => DumpSerializer.Serialize(a, Config());

        act.Should().NotThrow();
        var entry = DumpSerializer.Serialize(a, Config());
        entry.Json.Should().Contain("circular reference");
    }

    [Fact]
    public void Cycle_between_two_objects_is_caught()
    {
        var a = new Node { Name = "a" };
        var b = new Node { Name = "b", Next = a };
        a.Next = b;

        var entry = DumpSerializer.Serialize(a, Config());

        entry.Json.Should().Contain("circular reference");
    }

    [Fact]
    public void Depth_beyond_limit_is_truncated()
    {
        var chain = new Node { Name = "0" };
        var current = chain;
        for (var i = 1; i < 10; i++)
        {
            current.Next = new Node { Name = i.ToString() };
            current = current.Next;
        }

        var entry = DumpSerializer.Serialize(chain, Config(depth: 2));

        entry.Truncated.Should().BeTrue();
        entry.Json.Should().Contain("max depth reached");
    }

    [Fact]
    public void Item_count_beyond_limit_is_truncated()
    {
        var list = Enumerable.Range(0, 20).ToList();

        var entry = DumpSerializer.Serialize(list, Config(items: 5));

        entry.Truncated.Should().BeTrue();
        var arr = JsonNode.Parse(entry.Json)!.AsArray();
        arr.Count.Should().Be(5);
    }

    [Fact]
    public void Oversized_json_is_cut_to_max_size_and_flagged()
    {
        var entry = DumpSerializer.Serialize(new string('x', 5000), Config(size: 100));

        entry.Truncated.Should().BeTrue();
        entry.Json.Length.Should().Be(100);
    }

    [Fact]
    public void Redacted_field_names_are_masked()
    {
        var entry = DumpSerializer.Serialize(new Credentials(), Config());

        var obj = JsonNode.Parse(entry.Json)!.AsObject();
        obj["Username"]!.GetValue<string>().Should().Be("elad");
        obj["Password"]!.GetValue<string>().Should().Be("***");
        obj["ApiKey"]!.GetValue<string>().Should().Be("***");
    }

    [Fact]
    public void Getter_exception_is_captured_as_string_not_thrown()
    {
        var act = () => DumpSerializer.Serialize(new ThrowingProps(), Config());

        act.Should().NotThrow();
        var entry = DumpSerializer.Serialize(new ThrowingProps(), Config());
        var obj = JsonNode.Parse(entry.Json)!.AsObject();
        obj["Ok"]!.GetValue<string>().Should().Be("fine");
        obj["Boom"]!.GetValue<string>().Should().Contain("InvalidOperationException").And.Contain("nope");
    }

    [Fact]
    public void Primitives_and_common_types_serialize_directly()
    {
        DumpSerializer.Serialize(42, Config()).Json.Should().Be("42");
        DumpSerializer.Serialize(true, Config()).Json.Should().Be("true");
        DumpSerializer.Serialize("hi", Config()).Json.Should().Be("\"hi\"");
        DumpSerializer.Serialize((object?)null, Config()).Json.Should().Be("null");
    }

    [Fact]
    public void TypeName_reflects_the_runtime_type()
    {
        DumpSerializer.Serialize(new Credentials(), Config()).TypeName.Should().Contain("Credentials");
        DumpSerializer.Serialize((object?)null, Config()).TypeName.Should().Be("null");
    }

    [Fact]
    public void Never_completing_task_is_dumped_as_a_leaf_without_blocking()
    {
        var tcs = new TaskCompletionSource<int>();

        var entry = DumpSerializer.Serialize(tcs.Task, Config());

        var text = JsonNode.Parse(entry.Json)!.GetValue<string>();
        text.Should().StartWith("Task<Int32>");
        text.Should().NotContain("RanToCompletion");
    }

    [Fact]
    public void Task_property_on_an_object_does_not_block_the_dump()
    {
        var holder = new { Background = new TaskCompletionSource<int>().Task };

        var act = () => DumpSerializer.Serialize(holder, Config());

        act.Should().NotThrow();
        var entry = act();
        var text = JsonNode.Parse(entry.Json)!["Background"]!.GetValue<string>();
        text.Should().StartWith("Task<Int32>");
    }

    [Fact]
    public void Completed_task_shows_its_status()
    {
        var entry = DumpSerializer.Serialize(Task.FromResult(42), Config());

        var text = JsonNode.Parse(entry.Json)!.GetValue<string>();
        text.Should().Be("Task<Int32> (RanToCompletion)");
    }

    [Fact]
    public void IQueryable_is_rendered_as_a_leaf_without_enumerating()
    {
        var query = new FakeQueryable<int>(new List<int> { 1, 2, 3 });

        var entry = DumpSerializer.Serialize(query, Config());

        var text = JsonNode.Parse(entry.Json)!.GetValue<string>();
        text.Should().Be("IQueryable<Int32> (not enumerated)");
        query.EnumerationCount.Should().Be(0);
    }

    [Fact]
    public void Infinite_enumerable_is_bounded_by_max_items()
    {
        var entry = DumpSerializer.Serialize(InfiniteSequence(), Config(items: 5));

        entry.Truncated.Should().BeTrue();
        var arr = JsonNode.Parse(entry.Json)!.AsArray();
        arr.Count.Should().Be(5);
    }

    [Fact]
    public void Type_and_delegate_values_serialize_as_leaves()
    {
        var typeEntry = DumpSerializer.Serialize(typeof(Credentials), Config());
        typeEntry.Json.Should().Contain("Credentials");

        Action del = Type_and_delegate_values_serialize_as_leaves;
        var delEntry = DumpSerializer.Serialize(del, Config());
        delEntry.Json.Should().Contain("Type_and_delegate_values_serialize_as_leaves");
    }
}
