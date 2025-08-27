namespace _40K.Core;

public interface IDice
{
    public int D6();
    public int D3();
}

public sealed class RandomDice : IDice
{
    public int D6() => Random.Shared.Next(1, 7);
    public int D3() => Random.Shared.Next(1, 4);
}

public sealed class ScriptedDice : IDice
{
    private readonly Queue<int> _d6 = new();
    private readonly Queue<int> _d3 = new();

    public ScriptedDice(IEnumerable<int>? d6 = null, IEnumerable<int>? d3 = null)
    {
        if (d6 != null) foreach (var r in d6) { _d6.Enqueue(r); }
        if (d3 != null) foreach (var r in d3) { _d3.Enqueue(r); }
    }
    public int D6() => _d6.Count > 0 ? _d6.Dequeue() : 4; // default mid roll
    public int D3() => _d3.Count > 0 ? _d3.Dequeue() : 2; // default mid roll
}