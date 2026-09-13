public abstract class Curse {
	public abstract string Key { get; }
	public abstract string Label { get; }
	public abstract string Description { get; }

	public abstract void Apply();
}
