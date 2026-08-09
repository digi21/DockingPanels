# Contributing

Thanks for taking an interest in Digi21.WinUI.Docking. Issues and pull requests are welcome.

The library is still before its first release, so the public API can still change. If you are
about to build something sizeable on top of it, or your change touches the public API, please
open an issue first: it is cheaper to agree on the shape of an API than to redo a pull request.

## Building and running

You need Windows, the .NET 8 SDK or later, and the Windows App SDK 1.8 workload. Visual Studio
2022 is optional; everything below works from the command line.

```
dotnet build
dotnet test
dotnet run --project samples/DockingGallery
```

The repository holds three projects:

- `src/Digi21.WinUI.Docking` — the library, and the only thing that ships.
- `samples/DockingGallery` — an unpackaged WinUI app exercising every feature. It is the fastest
  way to try a change by hand, and the place to reproduce a bug.
- `tests/Digi21.WinUI.Docking.Tests` — xUnit tests.

## Reporting bugs

A docking bug is almost always a sequence of gestures, so describe them step by step ("float the
Output window, dock it back on the right guide, then drag it over…"). Please include the Windows
build, the Windows App SDK version, and whether the app is packaged or unpackaged. If you can
reproduce it in `samples/DockingGallery`, say how: that turns a report into a fix much faster.

## Tests

The tests cover the logic that does not need a XAML runtime: the layout XML format, and anything
that can be exercised without creating controls. There is no UI test harness, so changes to the
interactive behaviour (dragging, guides, auto-hide, floating windows) are validated by running the
gallery and trying them. Please say in the pull request what you tried by hand.

If you add or change the layout XML format, add a round-trip test for it, and keep reading the
older format versions: a layout saved by an earlier version must still load.

## Code style

`.editorconfig` carries the formatting rules, and the build treats warnings as errors, including
missing XML documentation on public members. Beyond that:

- Public types and members need XML documentation that says what they are for, not what they are
  called.
- Comments explain *why*. The docking code is full of decisions that look arbitrary until you know
  the constraint behind them (a WinUI quirk, an invariant of the layout tree), and those are worth
  a sentence. Comments restating the code are not.
- Keep the layout invariants in `LayoutManager`. Every structural mutation goes through it, so that
  empty containers are removed, single-pane splits collapse, and elements are detached before being
  attached elsewhere, in exactly one place.
- Match the surrounding code: file-scoped namespaces, nullable enabled, no abbreviations in names.

## Commits and pull requests

Commit messages are in English and follow the conventional style used in the history
(`feat:`, `fix:`, `chore:`, `docs:`), with a body explaining the reasoning when the subject is not
self-explanatory. Keep one topic per pull request; unrelated cleanups are easier to review on their
own.

When your change affects the public API or the behaviour a user can see, update `README.md` and add
an entry to `CHANGELOG.md` under `Unreleased`.

CI builds the solution, runs the tests and packs the library on every pull request; it has to pass.

## License

By contributing you agree that your contributions are licensed under the
[MIT License](LICENSE), like the rest of the project.
