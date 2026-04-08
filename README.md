<p align="center">
  <img src="assets/Icon.jpg" alt="TedToolkit Logo" width="180" />
</p>

<h1 align="center">TedToolkit</h1>

<p align="center">
  A collection of open-source .NET libraries focused on <strong>source generation</strong>, <strong>type safety</strong>, and <strong>zero-allocation patterns</strong>.
</p>

<p align="center">
  <a href="COPYING.LESSER"><img src="https://img.shields.io/badge/license-LGPL--3.0-blue.svg" alt="License: LGPL-3.0" /></a>
</p>

---

## Libraries

### TedToolkit.Assertions

Fluent, extensible assertion library with source-generated extension methods, multi-level severity (`Must` / `Should` / `Could`), and localization support.

- Source-generated fluent API from `IAssertionItem<T>` definitions
- Assertion scoping to collect and report multiple failures together
- Built-in assertions for equality, nullability, comparisons, collections, types, regex, and enums

### TedToolkit.Quantities

Type-safe, source-generated quantities and units library backed by the [QUDT](https://qudt.org/) ontology.

- Declare a `partial struct` with attributes and the source generator handles the rest
- Unit conversion, arithmetic operators, and cross-quantity operators (e.g. `Angle * Length`)
- Fluent creation via extension properties (`10.0.Metre`, `45.0.Degree`)
- SI, CGS, Imperial, US Customary, ISQ, and Planck unit systems

### TedToolkit.InterpolatedParser

Parse strings using C# interpolated string syntax -- extract typed values by writing the pattern as if you were formatting it.

- Intuitive `$"Name: {name}, Age: {age}"` pattern matching in reverse
- Type-safe extraction into `int`, `double`, `string`, enums, arrays, lists, and `IParsable<T>` types
- Regex support via `ParseRegex()` / `TryParseRegex()`
- High performance using `ref struct` handlers and `ReadOnlySpan<char>`

### TedToolkit.Localizations

Compile-time localization framework -- define translations in JSON, get strongly-typed C# code via Roslyn incremental source generator.

- JSON translation files with `{{param}}` placeholders become methods, nested objects become nested classes
- No reflection, no runtime cost, no magic strings
- Runtime culture switching with fallback to default translation

### TedToolkit.Scopes

Lightweight, high-performance ambient context (scope) library for synchronous and asynchronous call chains.

- `Scope<T>` for async-safe contexts using `AsyncLocal<T>`
- `FastScope<T>` for zero-allocation, sync-only scopes via `ref struct`
- `ScopeBase<T>` / `ScopeRecord<T>` base classes with auto-enter on construction
- Bundled Roslyn analyzers with code fixes for performance guidance

### TedToolkit.RoslynHelper

Fluent API for programmatically generating C# source code, designed for Roslyn incremental source generators.

- Chainable builder for classes, structs, records, interfaces, enums, delegates, and all member types
- Expression and statement builders for control flow, invocations, casts, and more
- XML documentation generation (`///` comments)

### TedToolkit.Hexa.Raii

RAII-style scope wrappers for [Hexa.NET](https://github.com/HexaEngine) ImGui / ImPlot / ImPlot3D / ImNodes bindings.

- Replace manual `Begin`/`End` and `Push`/`Pop` pairs with C# `using` statements
- Zero-allocation `readonly ref struct` wrappers
- `Succeed` property for failable `Begin` calls
- Packages: `TedToolkit.Hexa.Raii.ImGui`, `.ImPlot`, `.ImPlot3D`, `.ImNodes`

### TedToolkit.Refly

Lightweight wrapper for value types (structs) in heap-allocated classes, enabling mutable struct references across async boundaries.

- `Ref<T>` and `DisposableRef<T>` wrappers with `.ToRef()` extension methods
- Use structs where `ref struct` limitations (async, collections, fields) would otherwise block

### TedToolkit.CodeAnalysis

Meta-package bundling Roslyn analyzers (Roslynator, StyleCop, SonarAnalyzer) for consistent code quality across all TedToolkit projects.

---

## Compatibility

All libraries target a wide range of frameworks:

| Frameworks | Versions |
|---|---|
| .NET | 6.0, 7.0, 8.0, 9.0, 10.0 |
| .NET Framework | 4.7.2, 4.8 |
| .NET Standard | 2.0, 2.1 |

> **Note:** `TedToolkit.RoslynHelper` targets `netstandard2.0` only (Roslyn analyzer requirement). `TedToolkit.Hexa.Raii.*` does not target .NET Framework 4.x.

## License

All TedToolkit libraries are licensed under the [GNU Lesser General Public License v3.0 (LGPL-3.0)](COPYING.LESSER). You are free to use them in proprietary applications as long as the libraries themselves remain under the same license.
