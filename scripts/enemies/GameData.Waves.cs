using System.Collections.Generic;

public readonly record struct WaveGroup(string Type, int Count, float Interval);

public static partial class GameData {
	public const int BossWaveInterval = 5;

	public static readonly List<List<WaveGroup>> Waves = new List<List<WaveGroup>> {
		new() { new("grunt", 6, 0.8f) },
		new() { new("grunt", 8, 0.7f), new("runner", 3, 0.6f) },
		new() { new("runner", 8, 0.5f), new("eel", 4, 0.6f) },
		new() { new("grunt", 10, 0.6f), new("brute", 2, 1.2f) },
		new() { new("brute", 4, 1.0f), new("runner", 6, 0.4f), new("eel", 6, 0.5f) },
		new() { new("grunt", 12, 0.5f), new("runner", 8, 0.4f) },
		new() { new("brute", 6, 0.9f), new("grunt", 10, 0.4f), new("eel", 6, 0.45f) },
		new() { new("brute", 8, 0.7f), new("runner", 12, 0.3f), new("grunt", 10, 0.3f), new("golem", 2, 1.3f) },
		new() { new("brute", 10, 0.6f), new("runner", 10, 0.3f), new("eel", 8, 0.4f), new("golem", 3, 1.2f) },
		new() { new("grunt", 16, 0.35f), new("brute", 6, 0.7f), new("golem", 3, 1.1f), new("wisp", 3, 0.5f) },
		new() { new("runner", 18, 0.25f), new("brute", 8, 0.6f), new("eel", 8, 0.35f), new("golem", 4, 1.05f), new("wisp", 4, 0.45f) },
		new() { new("brute", 12, 0.5f), new("grunt", 14, 0.3f), new("golem", 4, 1.0f), new("wisp", 4, 0.42f) },
		new() { new("runner", 14, 0.25f), new("brute", 10, 0.45f), new("grunt", 10, 0.3f), new("eel", 10, 0.32f), new("golem", 5, 0.95f), new("wisp", 5, 0.4f) },
		new() { new("brute", 14, 0.4f), new("runner", 16, 0.22f), new("golem", 5, 0.9f), new("wisp", 5, 0.38f) },
		new() { new("brute", 16, 0.35f), new("runner", 20, 0.2f), new("grunt", 16, 0.25f), new("eel", 10, 0.3f), new("golem", 6, 0.85f), new("wisp", 6, 0.35f) },
		new() { new("grunt", 18, 0.3f), new("runner", 16, 0.22f), new("brute", 10, 0.4f), new("golem", 6, 0.8f), new("wisp", 6, 0.33f) },
		new() { new("brute", 18, 0.32f), new("grunt", 16, 0.28f), new("eel", 12, 0.28f), new("golem", 7, 0.78f), new("wisp", 7, 0.3f) },
		new() { new("runner", 22, 0.18f), new("brute", 12, 0.38f), new("grunt", 14, 0.25f), new("golem", 7, 0.75f), new("wisp", 7, 0.28f) },
		new() { new("brute", 20, 0.3f), new("runner", 18, 0.2f), new("eel", 12, 0.25f), new("golem", 8, 0.72f), new("wisp", 8, 0.26f) },
		new() { new("grunt", 20, 0.22f), new("brute", 16, 0.32f), new("runner", 18, 0.18f), new("golem", 8, 0.7f), new("wisp", 8, 0.25f) },
		new() { new("runner", 24, 0.16f), new("brute", 14, 0.3f), new("eel", 14, 0.22f), new("golem", 9, 0.68f), new("wisp", 9, 0.24f) },
		new() { new("brute", 22, 0.28f), new("grunt", 18, 0.22f), new("golem", 9, 0.65f), new("wisp", 9, 0.22f) },
		new() { new("runner", 26, 0.15f), new("brute", 16, 0.28f), new("grunt", 16, 0.2f), new("eel", 14, 0.2f), new("golem", 10, 0.62f), new("wisp", 10, 0.2f) },
		new() { new("brute", 24, 0.26f), new("runner", 22, 0.16f), new("golem", 10, 0.6f), new("wisp", 10, 0.19f) },
		new() { new("grunt", 22, 0.18f), new("brute", 26, 0.24f), new("runner", 24, 0.15f), new("eel", 16, 0.18f), new("golem", 12, 0.58f), new("wisp", 12, 0.18f) },
	};
}
