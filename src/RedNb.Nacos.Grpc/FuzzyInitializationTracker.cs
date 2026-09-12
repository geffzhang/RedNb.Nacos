using System.Collections.Concurrent;
using System.Text.Json;

namespace RedNb.Nacos.Grpc;

internal sealed class FuzzyInitializationTracker
{
    private sealed class State
    {
        public HashSet<string> Keys { get; } = [];
        public TaskCompletionSource<HashSet<string>> Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private readonly ConcurrentDictionary<string, State> _states = new();
    public Task<HashSet<string>> Begin(string pattern) => _states.GetOrAdd(pattern, _ => new()).Done.Task;
    public void Remove(string pattern) => _states.TryRemove(pattern, out _);
    public void Process(JsonElement root, string keyName)
    {
        if (!root.TryGetProperty("groupKeyPattern", out var pattern)) return;
        if (!_states.TryGetValue(pattern.GetString()!, out var state)) return;
        lock (state.Keys)
        {
            if (root.TryGetProperty("contexts", out var contexts))
                foreach (var item in contexts.EnumerateArray())
                    if (item.TryGetProperty(keyName, out var key)) state.Keys.Add(key.GetString()!);
            var type = root.TryGetProperty("syncType", out var sync) ? sync.GetString()
                : root.TryGetProperty("notifyType", out var notify) ? notify.GetString() : null;
            if (type == "FINISH_FUZZY_WATCH_INIT_NOTIFY") state.Done.TrySetResult(new(state.Keys));
        }
    }
}
