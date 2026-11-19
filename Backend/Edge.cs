namespace Backend;

/// <summary>A directed, weighted edge: it can be traversed only from From to To.</summary>
public readonly record struct Edge<T>(T From, T To, int Weight);
