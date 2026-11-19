namespace Backend;

/// <summary>A package: where it is picked up and where it is delivered.</summary>
public readonly record struct Package(int Id, string Pickup, string Delivery);
