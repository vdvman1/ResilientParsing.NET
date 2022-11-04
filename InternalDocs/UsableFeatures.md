# C# 11 Features usable in .NET 6

This library is written in C# 11 to take advantage of the improved scoping rules for `ref struct`s, in particular the `scoped` keyword for guaranteeing to the compiler and the caller that the provided `ref struct` is never kept as a reference outside the function call.

Unfortunately, this means we have to be careful about what C# 11 features we use, as some features require runtime changes, namely `ref` fields in `ref struct`s.

## Known Usable
- `scoped` keyword

## Known unusable
- `ref` fields in `ref struct`s. Might be able to create a wrapper `ref struct` that stores a reference as a one element `Span` or `ReadOnlySpan` created using `MemoryMarshal.CreateSpan` or `MemoryMarshal.CreateReadOnlySpan` respectively (.NET 6 runtime doesn't have single element constructors for `Span` and `ReadOnlySpan`). This workaround doesn't work if the type to reference is a `ref struct`